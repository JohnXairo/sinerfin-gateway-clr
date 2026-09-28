using System.Web.Http;
using System.Web.Http.Cors;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;
using SinerfinGatewayCLR.Infrastructure;
using SinerfinGatewayCLR.Services;

[assembly: OwinStartup(typeof(SinerfinGatewayCLR.OwinStartup))]

namespace SinerfinGatewayCLR
{
    /// <summary>
    /// Clase de arranque OWIN — equivalente a WebApiConfig + Program.cs de .NET Core.
    /// Es invocada por Microsoft.Owin.Hosting.WebApp.Start() desde GatewayService.
    /// </summary>
    public class OwinStartup
    {
        public void Configuration(IAppBuilder app)
        {
            // ── CORS (igual que el .NET Core: AllowAny*) ─────────────────
            app.UseCors(CorsOptions.AllowAll);

            // ── Web API 2 config ─────────────────────────────────────────
            var config = new HttpConfiguration();

            // JSON camelCase + UTC (Newtonsoft.Json)
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver     = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            json.SerializerSettings.NullValueHandling    = NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // Rutas por atributo + fallback convencional
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Tracing — Instana CLR agent hookea System.Diagnostics.Trace
            config.EnableSystemDiagnosticsTracing();

            // ── Inyectar servicios singleton ─────────────────────────────
            var baseUrl = System.Configuration.ConfigurationManager
                              .AppSettings["Sinerfin:BaseUrl"]
                          ?? "http://192.168.1.190:8080/sinerfin2-1.0/";

            var kafka     = new KafkaService();
            var mq        = new MqService();
            var validator = new TarjetaValidator();
            var sinerfin  = new SinerfinClient(baseUrl);

            config.DependencyResolver =
                new SimpleDependencyResolver(kafka, mq, validator, sinerfin);

            app.UseWebApi(config);
        }
    }
}
