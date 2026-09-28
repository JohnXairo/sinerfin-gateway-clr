using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using SinerfinGatewayCLR.Models;

namespace SinerfinGatewayCLR.Services
{
    /// <summary>
    /// Cliente HTTP hacia sinerfin2 (backend bancario Java).
    /// Equivalente al SinerfinClient de .NET Core, pero sin IHttpClientFactory.
    /// HttpClient es instanciado como singleton desde Global.asax (pattern correcto
    /// en .NET Framework para evitar socket exhaustion).
    /// Instana captura automaticamente las llamadas salientes HTTP como exit spans.
    /// </summary>
    public class SinerfinClient : IDisposable
    {
        private readonly HttpClient _http;

        public SinerfinClient(string baseUrl)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout     = TimeSpan.FromSeconds(10)
            };
            // Header que Instana usa para correlacionar el servicio destino
            // (identico al .NET Core: X-Service-Name: sinerfin2)
            _http.DefaultRequestHeaders.Add("X-Service-Name", "sinerfin2");
            _http.DefaultRequestHeaders.Accept
                 .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public string ObtenerTarjetas(string cedula)
        {
            try
            {
                var url  = "api/tarjetas?cedula=" + Uri.EscapeDataString(cedula);
                var resp = _http.GetAsync(url).Result;
                var body = resp.Content.ReadAsStringAsync().Result;
                return resp.IsSuccessStatusCode ? body : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[SinerfinClient] Error obteniendo tarjetas cedula={0}: {1}", cedula, ex.Message);
                return null;
            }
        }

        public SaldoResponse ObtenerSaldo(string cedula)
        {
            try
            {
                var url  = "api/saldo?cedula=" + Uri.EscapeDataString(cedula);
                var resp = _http.GetAsync(url).Result;
                var body = resp.Content.ReadAsStringAsync().Result;

                System.Diagnostics.Trace.TraceInformation(
                    "[SinerfinClient] GET {0} -> [{1}] {2}", url, (int)resp.StatusCode, body);

                if (!resp.IsSuccessStatusCode) return null;
                return JsonConvert.DeserializeObject<SaldoResponse>(body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[SinerfinClient] Error saldo cedula={0} base={1}: {2}",
                    cedula, _http.BaseAddress, ex.Message);
                return null;
            }
        }

        public TransaccionResponse RegistrarPago(string cedula, string nombre, decimal monto)
        {
            try
            {
                var nombreNormalizado = CultureInfo.InvariantCulture.TextInfo
                    .ToTitleCase(nombre.ToLower());

                // Construir JSON con InvariantCulture para evitar coma decimal
                var montoStr = monto.ToString("F2", CultureInfo.InvariantCulture);
                var payload  = "{\"cedula\":\"" + cedula + "\","
                             + "\"nombre\":\"" + nombreNormalizado + "\","
                             + "\"tipo\":\"PAGO_TARJETA\","
                             + "\"valor\":" + montoStr + "}";

                System.Diagnostics.Trace.TraceInformation(
                    "[SinerfinClient] POST api/transaccion payload={0}", payload);

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var resp    = _http.PostAsync("api/transaccion", content).Result;
                var body    = resp.Content.ReadAsStringAsync().Result;

                System.Diagnostics.Trace.TraceInformation(
                    "[SinerfinClient] POST api/transaccion -> [{0}] {1}",
                    (int)resp.StatusCode, body);

                if (resp.IsSuccessStatusCode)
                    return JsonConvert.DeserializeObject<TransaccionResponse>(body);

                if ((int)resp.StatusCode == 422)
                {
                    var err = JsonConvert.DeserializeObject<TransaccionResponse>(body);
                    return err ?? new TransaccionResponse { Ok = false, Mensaje = body };
                }

                return new TransaccionResponse
                {
                    Ok     = false,
                    Mensaje = "sinerfin2 error " + (int)resp.StatusCode + ": " + body
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[SinerfinClient] Error pago cedula={0} monto={1}: {2}",
                    cedula, monto, ex.Message);
                return new TransaccionResponse
                {
                    Ok     = false,
                    Mensaje = "Error de conexion con sinerfin2: " + ex.Message
                };
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
