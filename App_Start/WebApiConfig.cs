using System.Web.Http;
using System.Web.Http.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SinerfinGatewayCLR
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // ── JSON: camelCase + fechas UTC (equivalente al .NET Core) ──
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver     = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            json.SerializerSettings.NullValueHandling    = NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // ── CORS: igual al .NET Core (AllowAny*) ─────────────────────
            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            // ── Rutas con atributos ([Route]) + ruta por defecto ─────────
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // ── Tracing para Instana ─────────────────────────────────────
            // Instana .NET CLR agent captura spans HTTP a traves de
            // System.Web.Http pipeline. El tracing systemdiag escribe
            // al trace listener que el agente Instana hookea.
            config.EnableSystemDiagnosticsTracing();
        }
    }
}
