using System;
using System.Threading.Tasks;
using System.Configuration;
using Confluent.Kafka;

namespace SinerfinGatewayCLR.Services
{
    /// <summary>
    /// Publica eventos a Kafka usando el mismo Confluent.Kafka 2.4.0 del proyecto .NET Core.
    /// Confluent.Kafka soporta net462+ por lo que funciona en .NET Framework 4.8 sin cambios.
    /// Instana captura las llamadas Kafka como exit spans cuando el agente CLR esta activo.
    /// </summary>
    public class KafkaService : IDisposable
    {
        private readonly IProducer<string, string> _producer;

        private static string Cfg(string key, string def) =>
            ConfigurationManager.AppSettings[key] ?? def;

        public KafkaService()
        {
            var cfg = new ProducerConfig
            {
                BootstrapServers      = Cfg("Kafka:BootstrapServers", "localhost:9092"),
                Acks                  = Acks.None,
                MessageTimeoutMs      = 1000,
                SocketTimeoutMs       = 1000,
                RequestTimeoutMs      = 1000,
                MessageSendMaxRetries = 0
            };

            try
            {
                _producer = new ProducerBuilder<string, string>(cfg).Build();
                System.Diagnostics.Trace.TraceInformation(
                    "[KafkaService] Producer inicializado: {0}", cfg.BootstrapServers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[KafkaService] Kafka no disponible al iniciar: {0}", ex.Message);
                _producer = null;
            }
        }

        public Task PublicarPago(string cedula, string banco, string ultimos4,
            decimal monto, bool aprobado, string codigo, string motor)
        {
            var topic = Cfg("Kafka:TopicPagos", "sinerfin.pagos");
            var json  = "{\"evento\":\"PAGO_TARJETA\",\"timestamp\":\""
                      + DateTime.UtcNow.ToString("O") + "\","
                      + "\"cedula\":\"" + cedula + "\","
                      + "\"banco_emisor\":\"" + banco + "\","
                      + "\"ultimos_digitos\":\"" + ultimos4 + "\","
                      + "\"monto\":" + monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ","
                      + "\"aprobado\":" + aprobado.ToString().ToLower() + ","
                      + "\"codigo_autorizacion\":\"" + codigo + "\","
                      + "\"motor_bd\":\"" + motor + "\"}";

            PublicarFireAndForget(topic, cedula, json);
            return Task.FromResult(0);
        }

        public Task PublicarAuditoria(string evento, string detalle)
        {
            var topic = Cfg("Kafka:TopicAuditoria", "sinerfin.auditoria");
            var json  = "{\"evento\":\"" + evento + "\","
                      + "\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\","
                      + "\"servicio\":\"sinerfin-gateway-clr\","
                      + "\"detalle\":\"" + detalle.Replace("\"", "'") + "\"}";

            PublicarFireAndForget(topic, "gateway", json);
            return Task.FromResult(0);
        }

        private void PublicarFireAndForget(string topic, string key, string value)
        {
            if (_producer == null) return;

            Task.Run(async () =>
            {
                try
                {
                    await _producer.ProduceAsync(topic,
                        new Message<string, string> { Key = key, Value = value });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "[KafkaService] Publish error [{0}]: {1}", topic, ex.Message);
                }
            });
        }

        public void Dispose() => _producer?.Dispose();
    }
}
