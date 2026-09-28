using System;
using SinerfinGatewayCLR.Services;
using Topshelf;

namespace SinerfinGatewayCLR
{
    /// <summary>
    /// Punto de entrada del ejecutable.
    ///
    /// Modos de uso:
    ///   SinerfinGatewayCLR.exe           -> corre en consola (debug interactivo)
    ///   SinerfinGatewayCLR.exe install   -> instala como Windows Service
    ///   SinerfinGatewayCLR.exe uninstall -> desinstala el servicio
    ///   SinerfinGatewayCLR.exe start     -> inicia el servicio instalado
    ///   SinerfinGatewayCLR.exe stop      -> detiene el servicio
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var exitCode = HostFactory.Run(host =>
            {
                host.Service<GatewayService>(svc =>
                {
                    svc.ConstructUsing(() => new GatewayService());
                    svc.WhenStarted(s => s.Start());
                    svc.WhenStopped(s => s.Stop());
                });

                host.RunAsLocalSystem();
                host.StartAutomatically();

                host.SetServiceName("SinerfinGatewayCLR");
                host.SetDisplayName("Sinerfin Gateway CLR (.NET 4.8)");
                host.SetDescription(
                    "Gateway de pagos .NET Framework 4.8 para pruebas de instrumentacion Instana");

                // UseTraceLogger requiere Topshelf.NLog o Topshelf.Log4Net.
                // En su lugar usamos el logger interno de TopShelf (consola + event log).
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.Exit(exitCodeValue);
        }
    }
}
