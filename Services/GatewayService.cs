using System;
using System.Configuration;
using Microsoft.Owin.Hosting;

namespace SinerfinGatewayCLR.Services
{
    public class GatewayService
    {
        private IDisposable _webApp;

        public bool Start()
        {
            var url = ConfigurationManager.AppSettings["Gateway:Url"] ?? "http://+:9080";

            System.Diagnostics.Trace.TraceInformation(
                "[GatewayService] Iniciando OWIN en {0}", url);

            try
            {
                _webApp = WebApp.Start<OwinStartup>(url);
                System.Diagnostics.Trace.TraceInformation(
                    "[GatewayService] Escuchando en {0}", url);
                return true;
            }
            catch (Exception ex)
            {
                // Imprimir cadena completa de excepciones internas
                Console.WriteLine("=== ERROR COMPLETO ===");
                var e = ex;
                int nivel = 0;
                while (e != null)
                {
                    Console.WriteLine("[{0}] {1}: {2}", nivel, e.GetType().FullName, e.Message);
                    Console.WriteLine("StackTrace: {0}", e.StackTrace);
                    Console.WriteLine("---");
                    e = e.InnerException;
                    nivel++;
                }
                Console.WriteLine("=== FIN ERROR ===");
                return false;
            }
        }

        public bool Stop()
        {
            System.Diagnostics.Trace.TraceInformation("[GatewayService] Deteniendo...");
            _webApp?.Dispose();
            return true;
        }
    }
}
