using System.Web.Http;
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
    public class OwinStartup
    {
        public void Configuration(IAppBuilder app)
        {
            // ── CORS ──────────────────────────────────────────────────────
            app.UseCors(CorsOptions.AllowAll);

            // ── Web API 2 ─────────────────────────────────────────────────
            var config = new HttpConfiguration();

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver     = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            json.SerializerSettings.NullValueHandling    = NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // NOTA: EnableSystemDiagnosticsTracing() es solo para IIS-hosted.
            // En OWIN self-host Instana instrumenta directamente el HttpListener
            // y el pipeline de Web API sin necesidad de este metodo.

            // ── Servicios singleton ───────────────────────────────────────
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
