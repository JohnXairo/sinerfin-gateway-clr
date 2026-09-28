using System;
using Newtonsoft.Json;

namespace SinerfinGatewayCLR.Models
{
    // ── REQUEST ──────────────────────────────────────────────────────
    public class PagoRequest
    {
        public string  NumeroTarjeta   { get; set; } // 16 digitos
        public string  FechaExpiracion { get; set; } // MM/YY
        public string  Cvv             { get; set; } // 3-4 digitos
        public string  NombreTitular   { get; set; }
        public string  Cedula          { get; set; } // cedula del cliente en sinerfin
        public decimal Monto           { get; set; }
        public string  Concepto        { get; set; }
    }

    // ── RESPONSE ────────────────────────────────────────────────────
    public class PagoResponse
    {
        public bool     Aprobado           { get; set; }
        public string   CodigoAutorizacion { get; set; }
        public string   Mensaje            { get; set; }
        public string   UltimosDigitos     { get; set; }
        public string   BancoEmisor        { get; set; }
        public decimal  MontoAprobado      { get; set; }
        public decimal  SaldoRestante      { get; set; }
        public DateTime Timestamp          { get; set; }
    }

    // ── SALDO desde sinerfin2 API ───────────────────────────────
    public class SaldoResponse
    {
        public decimal Saldo     { get; set; }
        public long    Cuenta    { get; set; }
        public string  Nombre    { get; set; }
        public bool    Encontrado { get; set; }
    }

    // ── TRANSACCION a sinerfin2 API ────────────────────────────
    public class TransaccionRequest
    {
        public string  Cedula { get; set; }
        public string  Nombre { get; set; }
        public string  Tipo   { get; set; }
        public decimal Valor  { get; set; }
    }

    public class TransaccionResponse
    {
        public bool    Ok         { get; set; }
        // sinerfin2 devuelve "saldo_nuevo" en snake_case
        [JsonProperty("saldo_nuevo")]
        public decimal SaldoNuevo { get; set; }
        public string  Mensaje    { get; set; }
    }
}
