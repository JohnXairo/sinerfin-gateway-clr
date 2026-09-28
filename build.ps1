# build.ps1 — Compila SinerfinGatewayCLR sin Visual Studio
# Uso: .\build.ps1
# Uso release: .\build.ps1 -Configuration Release

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ── 1. Verificar nuget.exe ───────────────────────────────────────────────────
$nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
if (-not $nuget) {
    Write-Host "nuget.exe no encontrado en PATH. Descargando..." -ForegroundColor Yellow
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" `
        -OutFile "$root\nuget.exe"
    $nuget = "$root\nuget.exe"
} else {
    $nuget = $nuget.Source
}

# ── 2. Restaurar paquetes (con -PackagesDirectory para no necesitar .sln) ────
Write-Host "`n[1/3] Restaurando paquetes NuGet..." -ForegroundColor Cyan
& $nuget restore "$root\SinerfinGatewayCLR.csproj" -PackagesDirectory "$root\packages"
if ($LASTEXITCODE -ne 0) { throw "nuget restore fallo" }

# ── 3. Localizar MSBuild ─────────────────────────────────────────────────────
$msbuildPaths = @(
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
)
$msbuild = $msbuildPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild no encontrado. Instala Build Tools for Visual Studio 2022." }
Write-Host "MSBuild: $msbuild" -ForegroundColor DarkGray

# ── 4. Compilar ──────────────────────────────────────────────────────────────
Write-Host "`n[2/3] Compilando ($Configuration)..." -ForegroundColor Cyan
& $msbuild "$root\SinerfinGatewayCLR.csproj" `
    /p:Configuration=$Configuration `
    /p:Platform=AnyCPU `
    /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild fallo" }

# ── 5. Resultado ─────────────────────────────────────────────────────────────
$exe = "$root\bin\$Configuration\SinerfinGatewayCLR.exe"
if (Test-Path $exe) {
    Write-Host "`n[3/3] Build exitoso:" -ForegroundColor Green
    Write-Host "  $exe" -ForegroundColor White
    Write-Host "`nPara instalar como servicio Windows (como Administrador):"
    Write-Host "  cd bin\$Configuration"
    Write-Host "  .\SinerfinGatewayCLR.exe install"
    Write-Host "  .\SinerfinGatewayCLR.exe start"
} else {
    throw "El ejecutable no fue generado: $exe"
}
