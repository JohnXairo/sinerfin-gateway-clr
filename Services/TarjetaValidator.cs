using System;
using System.Linq;

namespace SinerfinGatewayCLR.Services
{
    /// <summary>
    /// Validacion de tarjetas. Logica identica al .NET Core.
    /// Compatible con .NET Framework 4.8 sin cambios (no usa C# 8+ features).
    /// </summary>
    public class TarjetaValidator
    {
        public bool ValidarLuhn(string numero)
        {
            var digits = numero.Replace(" ", "").Replace("-", "");
            if (digits.Length < 13 || digits.Length > 19) return false;
            if (!digits.All(char.IsDigit)) return false;

            int suma   = 0;
            bool doblar = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (doblar) { d *= 2; if (d > 9) d -= 9; }
                suma += d;
                doblar = !doblar;
            }
            return suma % 10 == 0;
        }

        public string DetectarBanco(string numero)
        {
            var n = numero.Replace(" ", "");
            if (n.StartsWith("4"))                                        return "Visa";
            if (n.StartsWith("51") || n.StartsWith("52") ||
                n.StartsWith("53") || n.StartsWith("54") ||
                n.StartsWith("55"))                                       return "Mastercard";
            if (n.StartsWith("34") || n.StartsWith("37"))               return "American Express";
            if (n.StartsWith("6011") || n.StartsWith("65"))             return "Discover";
            if (n.StartsWith("36") || n.StartsWith("38"))               return "Diners Club";
            return "Desconocido";
        }

        public bool ValidarFecha(string fecha)
        {
            var partes = fecha.Split('/');
            if (partes.Length != 2) return false;
            if (!int.TryParse(partes[0], out int mes))  return false;
            if (!int.TryParse(partes[1], out int anio)) return false;
            if (mes < 1 || mes > 12) return false;
            if (anio < 100) anio += 2000;
            var expiracion = new DateTime(anio, mes, 1).AddMonths(1).AddDays(-1);
            return expiracion >= DateTime.UtcNow.Date;
        }

        public bool ValidarCvv(string cvv, string numero)
        {
            if (!cvv.All(char.IsDigit)) return false;
            bool esAmex = numero.Replace(" ", "").StartsWith("34") ||
                          numero.Replace(" ", "").StartsWith("37");
            return esAmex ? cvv.Length == 4 : cvv.Length == 3;
        }

        public string Enmascarar(string numero)
        {
            var n = numero.Replace(" ", "");
            if (n.Length < 4) return "****";
            return "**** **** **** " + n.Substring(n.Length - 4);
        }

        public string GenerarCodigoAutorizacion()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            var result = new char[6];
            for (int i = 0; i < 6; i++)
                result[i] = chars[random.Next(chars.Length)];
            return new string(result);
        }
    }
}
