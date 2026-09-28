using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;

namespace SinerfinGatewayCLR.Services
{
    /// <summary>
    /// Publica mensajes a IBM MQ via REST API (puerto 9443).
    /// Identico al MqService .NET Core: sin libreria nativa IBM.WMQ,
    /// usa HttpClient directo al MQ REST endpoint.
    /// Instana traza el HttpClient saliente como exit span hacia IBM MQ.
    /// </summary>
    public class MqService : IDisposable
    {
        private readonly HttpClient _http;

        private static string Cfg(string key, string def) =>
            ConfigurationManager.AppSettings[key] ?? def;

        public MqService()
        {
            // Deshabilitar validacion SSL (cert self-signed de MQ)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (msg, cert, chain, errors) => true
            };
            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var user = Cfg("MQ:User", "");
            var pass = Cfg("MQ:Password", "");
            if (!string.IsNullOrWhiteSpace(user))
            {
                var creds = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(user + ":" + pass));
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", creds);
            }

            _http.DefaultRequestHeaders.Add("ibm-mq-rest-csrf-token", "");
        }

        public Task PublicarPagoRequest(string cedula, string nombre,
            decimal monto, string motor, string codigoAutorizacion)
        {
            var montoStr = monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var json = "{\"cedula\":\"" + cedula + "\","
                     + "\"nombre\":\"" + nombre + "\","
                     + "\"tipo\":\"PAGO_TARJETA\","
                     + "\"valor\":" + montoStr + ","
                     + "\"motor\":\"" + motor + "\","
                     + "\"codigo_autorizacion\":\"" + codigoAutorizacion + "\","
                     + "\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\","
                     + "\"origen\":\"sinerfin-gateway-clr\"}";

            PublicarFireAndForget(
                Cfg("MQ:QueuePagos", "SINERFIN.PAGOS.REQUEST"), json, cedula);
            return Task.FromResult(0);
        }

        public Task PublicarAuditoria(string evento, string detalle)
        {
            var json = "{\"evento\":\"" + evento + "\","
                     + "\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\","
                     + "\"servicio\":\"sinerfin-gateway-clr\","
                     + "\"detalle\":\"" + detalle.Replace("\"", "'") + "\"}";

            PublicarFireAndForget(
                Cfg("MQ:QueueTransacciones", "SINERFIN.TRANSACCIONES"), json, "gateway");
            return Task.FromResult(0);
        }

        private void PublicarFireAndForget(string queueName, string mensaje, string key)
        {
            Task.Run(async () =>
            {
                try
                {
                    var host = Cfg("MQ:Host",         "bus.sinergy.local");
                    var port = Cfg("MQ:RestPort",     "9443");
                    var qm   = Cfg("MQ:QueueManager", "SINERFIN");

                    var url = string.Format(
                        "https://{0}:{1}/ibmmq/rest/v2/messaging/qmgr/{2}/queue/{3}/message",
                        host, port, qm, queueName);

                    var content  = new StringContent(mensaje, Encoding.UTF8, "text/plain");
                    var response = await _http.PostAsync(url, content);

                    if (response.StatusCode == HttpStatusCode.Created ||
                        response.StatusCode == HttpStatusCode.NoContent)
                    {
                        System.Diagnostics.Trace.TraceInformation(
                            "[MqService] Publicado [{0}] key={1}", queueName, key);
                    }
                    else
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Trace.TraceWarning(
                            "[MqService] REST error [{0}] HTTP {1}: {2}",
                            queueName, (int)response.StatusCode, body);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "[MqService] Error [{0}]: {1}", queueName, ex.Message);
                }
            });
        }

        public void Dispose() => _http.Dispose();
    }
}
