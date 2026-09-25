using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace FacturasCefer.Services;

/// <summary>Formato y lectura de importes en español, y apertura de ficheros con la aplicación del sistema.</summary>
public static class Formato
{
    public static readonly CultureInfo Es = new("es-ES");

    public static string Importe(decimal? v) => v?.ToString("#,##0.00", Es) ?? "";
    public static string Porcentaje(decimal? v) => v?.ToString("0.##", Es) ?? "";

    /// <summary>
    /// Importe en formato español ("1.060,00") o con punto decimal ("1060.00"). Vacío = null.
    /// Devuelve false si hay texto que no es un importe.
    /// </summary>
    public static bool TryImporte(string? texto, out decimal? valor)
    {
        valor = null;
        var s = (texto ?? "").Replace("€", "").Replace("%", "").Replace(" ", "").Trim();
        if (s.Length == 0) return true;

        var cultura = Es;
        var ultimoPunto = s.LastIndexOf('.');
        if (!s.Contains(',') && ultimoPunto >= 0 && s.IndexOf('.') == ultimoPunto && s.Length - ultimoPunto - 1 <= 2)
            cultura = CultureInfo.InvariantCulture; // "1060.5" / "1060.50": punto decimal

        if (!decimal.TryParse(s, NumberStyles.Number, cultura, out var d)) return false;
        valor = d;
        return true;
    }

    /// <summary>Abre un fichero con su aplicación predeterminada (visor PDF).</summary>
    public static void Abrir(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
            throw new ReglaNegocioException($"No se encuentra el fichero:\n{ruta}");
        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }
}
