using System.Security.Cryptography;
using System.Text;
using FacturasCefer.Models;

namespace FacturasCefer.Services;

/// <summary>Factura existente que podría ser la misma que se está registrando.</summary>
/// <remarks>
/// <see cref="Fuerte"/>: mismo nº escrito de otra forma (casi seguro la misma factura). Si no, solo coincide la fecha y el
/// importe, algo normal en facturas periódicas con nº consecutivos: es un aviso, no un bloqueo.
/// </remarks>
public sealed record PosibleDuplicado(int Id, string Numero, DateTime Fecha, decimal Total, EstadoFactura Estado, string Motivo, bool Fuerte)
{
    public string Texto => $"nº {Numero} del {Fecha:dd/MM/yyyy} ({Formato.Importe(Total)} €, {Textos.Estado(Estado)}): {Motivo}";
}

/// <summary>Factura ya registrada a partir del mismo PDF (misma huella).</summary>
public sealed record FacturaMismoPdf(int Id, string Proveedor, string Numero, DateTime FechaRegistro)
{
    public string Texto => $"{Proveedor} nº {Numero} (registrada el {FechaRegistro:dd/MM/yyyy})";
}

/// <summary>Utilidades para detectar facturas repetidas más allá del nº exacto.</summary>
public static class Duplicados
{
    /// <summary>
    /// Nº de factura comparable: mayúsculas, solo letras y dígitos, y sin ceros a la izquierda en cada grupo
    /// de dígitos (los separadores cortan el grupo). «F-001», «F 1» y «f/0001» dan «F1»; «2026/087» y «2026-87» dan «202687».
    /// </summary>
    public static string NormalizarNumero(string? numero)
    {
        var sb = new System.Text.StringBuilder();
        var grupo = new System.Text.StringBuilder();
        void CerrarGrupo()
        {
            if (grupo.Length == 0) return;
            var sinCeros = grupo.ToString().TrimStart('0');
            sb.Append(sinCeros.Length == 0 ? "0" : sinCeros);
            grupo.Clear();
        }
        foreach (var c in (numero ?? "").ToUpperInvariant())
        {
            if (char.IsDigit(c)) grupo.Append(c);
            else
            {
                CerrarGrupo();
                if (char.IsLetter(c)) sb.Append(c);
            }
        }
        CerrarGrupo();
        return sb.ToString();
    }

    /// <summary>Nº de factura solo con letras y dígitos (sin separadores): «FV26-07761» da «FV2607761».</summary>
    public static string NumeroCompacto(string? numero) =>
        new((numero ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Mismo nº escrito de otra forma (separadores, mayúsculas o ceros a la izquierda).</summary>
    public static bool MismoNumero(string? a, string? b)
    {
        var ca = NumeroCompacto(a);
        if (ca.Length == 0) return false;
        return ca == NumeroCompacto(b) || NormalizarNumero(a) == NormalizarNumero(b);
    }

    /// <summary>Huella SHA-256 (hex en minúsculas) del fichero.</summary>
    public static string Huella(string ruta)
    {
        using var fs = File.OpenRead(ruta);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }

    /// <summary>
    /// Compara una factura nueva con las existentes del mismo proveedor: mismo nº escrito de otra forma,
    /// o misma fecha y mismo total.
    /// </summary>
    public static List<PosibleDuplicado> Buscar(IEnumerable<(int Id, string Numero, DateTime Fecha, decimal Total, EstadoFactura Estado)> existentes,
        string numero, DateTime fecha, decimal total)
    {
        var lista = new List<PosibleDuplicado>();
        foreach (var e in existentes)
        {
            if (MismoNumero(numero, e.Numero))
                lista.Add(new PosibleDuplicado(e.Id, e.Numero, e.Fecha, e.Total, e.Estado,
                    string.Equals(e.Numero.Trim(), numero.Trim(), StringComparison.OrdinalIgnoreCase)
                        ? "mismo número" : "mismo número escrito de otra forma", Fuerte: true));
            else if (e.Fecha.Date == fecha.Date && Math.Abs(e.Total - total) <= 0.01m)
                lista.Add(new PosibleDuplicado(e.Id, e.Numero, e.Fecha, e.Total, e.Estado, "misma fecha y mismo importe", Fuerte: false));
        }
        return lista;
    }
}
