using System;
using System.Configuration;
using Microsoft.Owin.Hosting;

namespace SinerfinGatewayCLR.Services
{
    /// <summary>
    /// Logica del servicio Windows: arranca/para el listener OWIN.
    /// TopShelf llama a Start() y Stop() automaticamente.
    /// </summary>
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
                // WebApp.Start<T> arranca el HttpListener y enruta todo a OwinStartup
                _webApp = WebApp.Start<OwinStartup>(url);
                System.Diagnostics.Trace.TraceInformation(
                    "[GatewayService] Escuchando en {0}", url);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[GatewayService] Error al iniciar: {0}", ex.Message);
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
