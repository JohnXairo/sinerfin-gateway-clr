using System;
using SinerfinGatewayCLR.Services;
using Topshelf;

namespace SinerfinGatewayCLR
{
    /// <summary>
    /// Punto de entrada del ejecutable.
    ///
    /// Modos de uso:
    ///   SinerfinGatewayCLR.exe              -> corre en consola (debug)
    ///   SinerfinGatewayCLR.exe install      -> instala como Windows Service
    ///   SinerfinGatewayCLR.exe uninstall    -> desinstala el servicio
    ///   SinerfinGatewayCLR.exe start        -> inicia el servicio instalado
    ///   SinerfinGatewayCLR.exe stop         -> detiene el servicio
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // netsh para permitir el puerto sin admin en debug:
            // netsh http add urlacl url=http://+:9080/ user=EVERYONE

            var exitCode = HostFactory.Run(host =>
            {
                host.Service<GatewayService>(svc =>
                {
                    svc.ConstructUsing(() => new GatewayService());
                    svc.WhenStarted(s  => s.Start());
                    svc.WhenStopped(s  => s.Stop());
                });

                host.RunAsLocalSystem();          // corre como SYSTEM (igual que IIS)
                host.StartAutomatically();        // arranca con Windows

                host.SetServiceName("SinerfinGatewayCLR");
                host.SetDisplayName("Sinerfin Gateway CLR (.NET 4.8)");
                host.SetDescription(
                    "Gateway de pagos .NET Framework 4.8 para pruebas de instrumentacion Instana");

                // Log de TopShelf a System.Diagnostics.Trace (Instana lo captura)
                host.UseTraceLogger();
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.Exit(exitCodeValue);
        }
    }
}
