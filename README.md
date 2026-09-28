# sinerfin-gateway-clr

Clon de **sinerfin-gateway** en **.NET Framework 4.8 (CLR)** para pruebas de instrumentación con **IBM Instana**.

> El proyecto original corre en .NET 10. Este clon replica el mismo flujo de pagos  
> usando ASP.NET Web API 2 sobre .NET Framework 4.8, para validar la auto-instrumentación  
> del agente Instana en aplicaciones CLR legacy del cliente.

---

## Estructura

```
sinerfin-gateway-clr/
├── App_Start/
│   └── WebApiConfig.cs          # Rutas, JSON formatter, CORS, tracing
├── Controllers/
│   ├── HealthController.cs      # GET /api/health
│   └── PagoController.cs        # POST /api/pago, GET /api/pago/saldo, GET /api/pago/tarjetas
├── Infrastructure/
│   └── SimpleDependencyResolver.cs  # DI manual (singleton services)
├── Models/
│   └── PagoModels.cs            # Request/Response (Newtonsoft.Json)
├── Services/
│   ├── KafkaService.cs          # Confluent.Kafka 2.4.0 (mismo NuGet)
│   ├── MqService.cs             # IBM MQ via REST API (HttpClient)
│   ├── SinerfinClient.cs        # HttpClient hacia sinerfin2 backend
│   └── TarjetaValidator.cs      # Luhn, BIN, CVV, fecha expiración
├── Global.asax / Global.asax.cs
├── Web.config                   # AppSettings (equivalente a appsettings.json)
├── packages.config
└── SinerfinGatewayCLR.csproj
```

---

## Diferencias vs sinerfin-gateway (.NET 10)

| Aspecto | .NET 10 (original) | .NET Framework 4.8 (este repo) |
|---|---|---|
| Host | `WebApplication.CreateBuilder()` | `Global.asax` + `HttpApplication` |
| Controller base | `ControllerBase` | `ApiController` (Web API 2) |
| Rutas | `[ApiController]` + `[Route]` | `[RoutePrefix]` + `[Route]` |
| DI | Built-in `IServiceCollection` | `SimpleDependencyResolver` manual |
| JSON | `System.Text.Json` | `Newtonsoft.Json 13` |
| Config | `appsettings.json` | `Web.config` AppSettings |
| `HttpClient` | `IHttpClientFactory` | Singleton en `Global.asax` |
| Kafka | `Confluent.Kafka 2.4.0` | **Mismo NuGet** (net462+) |
| IBM MQ | REST API via `HttpClient` | **Mismo enfoque** |
| Logging | `ILogger<T>` | `System.Diagnostics.Trace` |
| Async | `async/await` nativo | `Task.Result` (sync over async — válido para demo CLR) |

---

## Requisitos

- **Visual Studio 2019 / 2022** con workload _ASP.NET and web development_
- **.NET Framework 4.8 Developer Pack**
- **IIS / IIS Express** para hostear el Web API
- NuGet packages (se restauran automáticamente):
  - `Microsoft.AspNet.WebApi` 5.3.0
  - `Microsoft.AspNet.WebApi.Cors` 5.3.0
  - `Newtonsoft.Json` 13.0.3
  - `Confluent.Kafka` 2.4.0
  - `BCrypt.Net-Next` 4.0.3

---

## Build y ejecución

```bash
# Restaurar paquetes
nuget restore SinerfinGatewayCLR.csproj

# Compilar
msbuild SinerfinGatewayCLR.csproj /p:Configuration=Release

# Publicar en IIS — apuntar el App Pool a .NET Framework 4.0 (Integrated pipeline)
```

O directamente desde Visual Studio: **F5** con IIS Express.

---

## Configuración (Web.config)

```xml
<appSettings>
  <add key="Sinerfin:BaseUrl"  value="http://192.168.1.190:8080/sinerfin2-1.0/" />
  <add key="Kafka:BootstrapServers" value="192.168.1.190:9092" />
  <add key="MQ:Host"           value="bus.sinergy.local" />
  <add key="MQ:RestPort"       value="9443" />
  <add key="MQ:QueueManager"   value="SINERFIN" />
</appSettings>
```

---

## Instrumentación Instana (CLR)

El agente Instana para .NET Framework se instala como **profiler CLR** via variables de entorno:

```powershell
# IIS — agregar en applicationHost.config o via inetmgr
$env:COR_ENABLE_PROFILING   = "1"
$env:COR_PROFILER            = "{FA8F1D88-0E79-422F-8CBF-C9E2D5C0780F}"
$env:COR_PROFILER_PATH       = "C:\Program Files\Instana\agent\bin\Instana.CLRProfiler.dll"
$env:INSTANA_AGENT_HOST      = "localhost"
$env:INSTANA_AGENT_PORT      = "42699"
```

### Qué captura Instana automáticamente en este proyecto

| Span | Tipo | Tecnología |
|---|---|---|
| `POST /api/pago` | **entry** | ASP.NET Web API 2 |
| `GET api/saldo` → sinerfin2 | **exit** | `HttpClient` |
| `POST api/transaccion` → sinerfin2 | **exit** | `HttpClient` |
| Kafka `sinerfin.pagos` | **exit** | Confluent.Kafka |
| IBM MQ REST `SINERFIN.PAGOS.REQUEST` | **exit** | `HttpClient` |

El trace distribuido `.NET CLR → Kafka → Java` y `.NET CLR → IBM MQ → Java`  
queda visible en el mapa de dependencias de Instana como en el proyecto .NET 10.

---

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/health` | Health check — runtime, versión, timestamp |
| `POST` | `/api/pago` | Procesar pago con tarjeta |
| `GET` | `/api/pago/saldo?cedula=X` | Consultar saldo del cliente |
| `GET` | `/api/pago/tarjetas?cedula=X` | Tarjetas guardadas del cliente |
