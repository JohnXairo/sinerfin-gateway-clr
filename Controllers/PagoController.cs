using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;
using SinerfinGatewayCLR.Models;
using SinerfinGatewayCLR.Services;

namespace SinerfinGatewayCLR.Controllers
{
    /// <summary>
    /// Equivalente al PagoController de .NET Core.
    /// Mismo flujo: validar tarjeta -> consultar saldo -> registrar retiro.
    /// Usa ApiController (Web API 2) en lugar de ControllerBase (ASP.NET Core).
    ///
    /// Instana instrumenta automaticamente:
    ///   - El entry span HTTP al recibir POST /api/pago
    ///   - Los exit spans de SinerfinClient (HttpClient) y MqService (HttpClient MQ)
    ///   - Los exit spans de KafkaService (Confluent.Kafka)
    /// </summary>
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/pago")]
    public class PagoController : ApiController
    {
        private readonly SinerfinClient   _sinerfin;
        private readonly TarjetaValidator _validator;
        private readonly KafkaService     _kafka;
        private readonly MqService        _mq;

        public PagoController(
            SinerfinClient sinerfin, TarjetaValidator validator,
            KafkaService kafka, MqService mq)
        {
            _sinerfin  = sinerfin;
            _validator = validator;
            _kafka     = kafka;
            _mq        = mq;
        }

        // POST /api/pago
        [HttpPost, Route("")]
        public IHttpActionResult ProcesarPago([FromBody] PagoRequest req)
        {
            if (req == null)
                return BadRequest("Body requerido");

            System.Diagnostics.Trace.TraceInformation(
                "[PagoController] Procesando pago cedula={0} monto={1}",
                req.Cedula, req.Monto);

            // ── 1. Validar tarjeta ──────────────────────────────────────────
            if (!_validator.ValidarLuhn(req.NumeroTarjeta))
            {
                _kafka.PublicarAuditoria("PAGO_RECHAZADO_TARJETA",
                    "Luhn invalido cedula=" + req.Cedula);
                return BadRequest(Rechazado("Numero de tarjeta invalido",
                    req.NumeroTarjeta, req.Monto));
            }

            if (!_validator.ValidarFecha(req.FechaExpiracion))
            {
                _kafka.PublicarAuditoria("PAGO_RECHAZADO_TARJETA",
                    "Tarjeta vencida cedula=" + req.Cedula);
                return BadRequest(Rechazado("Tarjeta vencida o fecha invalida",
                    req.NumeroTarjeta, req.Monto));
            }

            if (!_validator.ValidarCvv(req.Cvv, req.NumeroTarjeta))
            {
                _kafka.PublicarAuditoria("PAGO_RECHAZADO_TARJETA",
                    "CVV invalido cedula=" + req.Cedula);
                return BadRequest(Rechazado("CVV invalido",
                    req.NumeroTarjeta, req.Monto));
            }

            if (req.Monto <= 0)
                return BadRequest(Rechazado("El monto debe ser mayor a 0",
                    req.NumeroTarjeta, req.Monto));

            var banco    = _validator.DetectarBanco(req.NumeroTarjeta);
            var numSinEsp = req.NumeroTarjeta.Replace(" ", "");
            var ultimos4 = numSinEsp.Length >= 4
                ? numSinEsp.Substring(numSinEsp.Length - 4) : "????";

            // ── 2. Consultar saldo en sinerfin2 ──────────────────────────
            var saldoInfo = _sinerfin.ObtenerSaldo(req.Cedula);
            if (saldoInfo == null || !saldoInfo.Encontrado)
            {
                _kafka.PublicarAuditoria("PAGO_RECHAZADO_CLIENTE",
                    "Cliente no encontrado cedula=" + req.Cedula);
                return Content(HttpStatusCode.NotFound,
                    Rechazado("Cliente no encontrado en el sistema bancario",
                        req.NumeroTarjeta, req.Monto));
            }

            if (saldoInfo.Saldo < req.Monto)
            {
                _kafka.PublicarAuditoria("PAGO_RECHAZADO_SALDO",
                    string.Format("Saldo insuficiente cedula={0} saldo={1} monto={2}",
                        req.Cedula, saldoInfo.Saldo, req.Monto));
                return Content(HttpStatusCode.UnprocessableEntity,
                    Rechazado(
                        string.Format("Saldo insuficiente. Disponible: ${0:F2}", saldoInfo.Saldo),
                        req.NumeroTarjeta, req.Monto));
            }

            // ── 3. Registrar retiro en sinerfin2 ─────────────────────────
            var txResp = _sinerfin.RegistrarPago(
                req.Cedula, req.NombreTitular, req.Monto);

            if (txResp == null || !txResp.Ok)
            {
                var motivo = txResp != null ? txResp.Mensaje : "Error de conexion con sinerfin2";
                System.Diagnostics.Trace.TraceWarning(
                    "[PagoController] Transaccion rechazada: {0}", motivo);
                _kafka.PublicarAuditoria("PAGO_ERROR_TRANSACCION",
                    "cedula=" + req.Cedula + " motivo=" + motivo);

                var statusCode = txResp == null
                    ? HttpStatusCode.BadGateway
                    : (HttpStatusCode)422;
                return Content(statusCode,
                    Rechazado(motivo, req.NumeroTarjeta, req.Monto));
            }

            // ── 4. Pago aprobado ───────────────────────────────────────────
            var codigo = _validator.GenerarCodigoAutorizacion();

            _kafka.PublicarPago(req.Cedula, banco, ultimos4,
                req.Monto, true, codigo, "sinerfin2");

            // Exit span: .NET CLR -> IBM MQ -> Java (trace distribuido en Instana)
            _mq.PublicarPagoRequest(
                req.Cedula, req.NombreTitular, req.Monto,
                "POSTGRES", codigo);

            System.Diagnostics.Trace.TraceInformation(
                "[PagoController] Pago APROBADO cedula={0} monto={1} codigo={2}",
                req.Cedula, req.Monto, codigo);

            return Ok(new PagoResponse
            {
                Aprobado           = true,
                CodigoAutorizacion = codigo,
                Mensaje            = "Pago aprobado exitosamente",
                UltimosDigitos     = ultimos4,
                BancoEmisor        = banco,
                MontoAprobado      = req.Monto,
                SaldoRestante      = txResp.SaldoNuevo,
                Timestamp          = DateTime.UtcNow
            });
        }

        // GET /api/pago/tarjetas?cedula=X
        [HttpGet, Route("tarjetas")]
        public IHttpActionResult ObtenerTarjetas([FromUri] string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
                return BadRequest("cedula requerida");

            try
            {
                var json = _sinerfin.ObtenerTarjetas(cedula);
                var resultado = json ?? "{\"tarjetas\":[]}";
                return ResponseMessage(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resultado,
                        System.Text.Encoding.UTF8, "application/json")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[PagoController] Error tarjetas: {0}", ex.Message);
                return Ok(new { tarjetas = new object[0] });
            }
        }

        // GET /api/pago/saldo?cedula=X
        [HttpGet, Route("saldo")]
        public IHttpActionResult ConsultarSaldo([FromUri] string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
                return BadRequest("cedula requerida");

            var saldo = _sinerfin.ObtenerSaldo(cedula);
            if (saldo == null || !saldo.Encontrado)
                return Content(HttpStatusCode.NotFound, new { encontrado = false });

            return Ok(saldo);
        }

        // Helper respuesta de rechazo
        private PagoResponse Rechazado(string mensaje, string numero, decimal monto)
        {
            var n = numero.Replace(" ", "");
            return new PagoResponse
            {
                Aprobado           = false,
                CodigoAutorizacion = "",
                Mensaje            = mensaje,
                UltimosDigitos     = n.Length >= 4 ? n.Substring(n.Length - 4) : "????",
                BancoEmisor        = _validator.DetectarBanco(numero),
                MontoAprobado      = 0,
                SaldoRestante      = 0,
                Timestamp          = DateTime.UtcNow
            };
        }
    }
}
