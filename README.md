# sinerfin-gateway-clr

Clon de **sinerfin-gateway** en **.NET Framework 4.8 (CLR)** para pruebas de instrumentación con **IBM Instana**.

> Replica el mismo flujo de pagos (HTTP → Kafka → IBM MQ) usando **ASP.NET Web API 2 + OWIN self-host**,  
> desplegado como **Windows Service** sin IIS ni Visual Studio.

---

## Arquitectura del proyecto

```
sinerfin-gateway-clr/
├── Program.cs                       # Entry point — TopShelf registra el Windows Service
├── App.config                       # Config (equiv. appsettings.json)
├── App_Start/
│   └── OwinStartup.cs               # Startup OWIN: rutas, JSON, CORS, DI
├── Controllers/
│   ├── HealthController.cs          # GET /api/health
│   └── PagoController.cs            # POST /api/pago, saldo, tarjetas
├── Infrastructure/
│   └── SimpleDependencyResolver.cs  # DI singleton manual
├── Models/
│   └── PagoModels.cs                # Request / Response (Newtonsoft.Json)
├── Services/
│   ├── GatewayService.cs            # Start/Stop del WebApp OWIN
│   ├── KafkaService.cs              # Confluent.Kafka 2.4.0
│   ├── MqService.cs                 # IBM MQ via REST API
│   ├── SinerfinClient.cs            # HttpClient → sinerfin2
│   └── TarjetaValidator.cs          # Luhn, BIN, CVV, fecha
├── packages.config
└── SinerfinGatewayCLR.csproj        # OutputType Exe, net48
```

---

## Diferencias vs sinerfin-gateway (.NET 10)

| Aspecto | .NET 10 (original) | .NET Framework 4.8 (este repo) |
|---|---|---|
| Host | `WebApplication.CreateBuilder()` | OWIN `WebApp.Start<OwinStartup>()` |
| Windows Service | `UseWindowsService()` | **TopShelf** 4.3 |
| Controller base | `ControllerBase` | `ApiController` (Web API 2) |
| DI | Built-in `IServiceCollection` | `SimpleDependencyResolver` manual |
| JSON | `System.Text.Json` | `Newtonsoft.Json 13` |
| Config | `appsettings.json` | `App.config` AppSettings |
| Kafka | `Confluent.Kafka 2.4.0` | **Mismo NuGet** (net462+) |
| IBM MQ | REST API via `HttpClient` | **Mismo enfoque** |
| Logging | `ILogger<T>` | `System.Diagnostics.Trace` |
| IIS requerido | No | **No** — OWIN HttpListener |

---

## Requisitos del servidor

- **Windows Server 2016+** (o Windows 10/11 para pruebas)
- **.NET Framework 4.8 Runtime** — ya incluido en Windows 10 1903+ y Server 2019+  
  Verificar: `reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release`
- **Build tools** (solo para compilar, no para ejecutar):
  - [Build Tools for Visual Studio 2022](https://aka.ms/vs/17/release/vs_BuildTools.exe) *(sin Visual Studio)*
  - O NuGet CLI + MSBuild desde `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe`

---

## Build sin Visual Studio

```powershell
# 1. Clonar
git clone https://github.com/JohnXairo/sinerfin-gateway-clr.git
cd sinerfin-gateway-clr

# 2. Descargar NuGet CLI (una sola vez)
Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile nuget.exe

# 3. Restaurar paquetes
.\nuget.exe restore SinerfinGatewayCLR.csproj

# 4. Compilar en Release
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
    SinerfinGatewayCLR.csproj /p:Configuration=Release /p:Platform=AnyCPU

# El ejecutable queda en: bin\Release\SinerfinGatewayCLR.exe
```

---

## Desplegar como Windows Service

TopShelf maneja todo — no necesitas `sc.exe` ni el SCM manualmente:

```powershell
# Instalar (requiere PowerShell como Administrador)
cd bin\Release
.\SinerfinGatewayCLR.exe install
.\SinerfinGatewayCLR.exe start

# Verificar
Get-Service SinerfinGatewayCLR

# Probar endpoint
Invoke-RestMethod http://localhost:9080/api/health

# Desinstalar
.\SinerfinGatewayCLR.exe stop
.\SinerfinGatewayCLR.exe uninstall
```

> **Puerto**: por defecto `9080` (igual que el proyecto .NET 10).  
> Cambiarlo en `App.config` → `Gateway:Url`.

### Permitir el puerto sin UAC (si corre como usuario no-admin en pruebas)

```cmd
netsh http add urlacl url=http://+:9080/ user=EVERYONE
```

---

## Configuración (App.config)

```xml
<appSettings>
  <add key="Gateway:Url"            value="http://+:9080" />
  <add key="Sinerfin:BaseUrl"       value="http://192.168.1.190:8080/sinerfin2-1.0/" />
  <add key="Kafka:BootstrapServers" value="192.168.1.190:9092" />
  <add key="MQ:Host"                value="bus.sinergy.local" />
  <add key="MQ:RestPort"            value="9443" />
  <add key="MQ:QueueManager"        value="SINERFIN" />
</appSettings>
```

---

## Instrumentación Instana (CLR Profiler)

El agente Instana se inyecta como **CLR Profiler** via variables de entorno del servicio:

```powershell
# Configurar en las propiedades del servicio Windows (o via registro)
[System.Environment]::SetEnvironmentVariable(
    "COR_ENABLE_PROFILING", "1", "Machine")
[System.Environment]::SetEnvironmentVariable(
    "COR_PROFILER", "{FA8F1D88-0E79-422F-8CBF-C9E2D5C0780F}", "Machine")
[System.Environment]::SetEnvironmentVariable(
    "COR_PROFILER_PATH",
    "C:\Program Files\Instana\agent\bin\Instana.CLRProfiler.dll", "Machine")
[System.Environment]::SetEnvironmentVariable(
    "INSTANA_AGENT_HOST", "localhost", "Machine")
[System.Environment]::SetEnvironmentVariable(
    "INSTANA_AGENT_PORT", "42699", "Machine")

# Reiniciar el servicio para que tome las variables
Restart-Service SinerfinGatewayCLR
```

### Spans capturados automáticamente

| Span | Tipo | Tecnología |
|---|---|---|
| `POST /api/pago` | **entry** | ASP.NET Web API 2 (OWIN) |
| `GET api/saldo` → sinerfin2 | **exit** | `HttpClient` |
| `POST api/transaccion` → sinerfin2 | **exit** | `HttpClient` |
| Kafka `sinerfin.pagos` | **exit** | Confluent.Kafka |
| IBM MQ REST `SINERFIN.PAGOS.REQUEST` | **exit** | `HttpClient` → MQ REST |

---

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/health` | Health check — runtime `netframework48`, versión, timestamp |
| `POST` | `/api/pago` | Procesar pago con tarjeta |
| `GET` | `/api/pago/saldo?cedula=X` | Consultar saldo del cliente |
| `GET` | `/api/pago/tarjetas?cedula=X` | Tarjetas guardadas del cliente |
