using System;
using System.Web;
using System.Web.Http;
using System.Configuration;
using SinerfinGatewayCLR.Services;
using SinerfinGatewayCLR.Infrastructure;

namespace SinerfinGatewayCLR
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);

            var baseUrl = ConfigurationManager.AppSettings["Sinerfin:BaseUrl"]
                          ?? "http://192.168.1.190:8080/sinerfin2-1.0/";

            var kafka     = new KafkaService();
            var mq        = new MqService();
            var validator = new TarjetaValidator();
            var sinerfin  = new SinerfinClient(baseUrl);

            GlobalConfiguration.Configuration.DependencyResolver =
                new SimpleDependencyResolver(kafka, mq, validator, sinerfin);
        }

        protected void Application_End()
        {
            var resolver = GlobalConfiguration.Configuration.DependencyResolver;
            (resolver?.GetService(typeof(KafkaService)) as IDisposable)?.Dispose();
            (resolver?.GetService(typeof(MqService))    as IDisposable)?.Dispose();
        }
    }
}
