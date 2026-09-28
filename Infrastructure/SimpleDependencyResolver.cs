using System;
using System.Collections.Generic;
using System.Web.Http.Dependencies;
using SinerfinGatewayCLR.Services;

namespace SinerfinGatewayCLR.Infrastructure
{
    /// <summary>
    /// Contenedor DI minimalista para Web API 2.
    /// Sustituye el AddSingleton / IHttpClientFactory de .NET Core.
    /// Para produccion real usar Autofac.WebApi2.
    /// </summary>
    public class SimpleDependencyResolver : IDependencyResolver
    {
        private readonly KafkaService     _kafka;
        private readonly MqService        _mq;
        private readonly TarjetaValidator _validator;
        private readonly SinerfinClient   _sinerfin;

        public SimpleDependencyResolver(
            KafkaService kafka, MqService mq,
            TarjetaValidator validator, SinerfinClient sinerfin)
        {
            _kafka     = kafka;
            _mq        = mq;
            _validator = validator;
            _sinerfin  = sinerfin;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(KafkaService))     return _kafka;
            if (serviceType == typeof(MqService))        return _mq;
            if (serviceType == typeof(TarjetaValidator)) return _validator;
            if (serviceType == typeof(SinerfinClient))   return _sinerfin;
            return null;
        }

        public IEnumerable<object> GetServices(Type serviceType) =>
            new List<object>();

        public IDependencyScope BeginScope() => this;
        public void Dispose() { /* singletons — no se liberan por scope */ }
    }
}
