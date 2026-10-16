using FacturasCefer.Services;

namespace FacturasCefer.Models;

/// <summary>Fila de dbo.FacturaImportacion: un PDF tratado por el importador automático.</summary>
public sealed class RegistroImportacion
{
    public int Id { get; set; }
    public string Fuente { get; set; } = "";
    public string IdExterno { get; set; } = "";
    public string NombreFichero { get; set; } = "";
    public string? Remitente { get; set; }
    public string? Asunto { get; set; }
    public DateTime Fecha { get; set; }
    public string Resultado { get; set; } = "";
    public string? Mensaje { get; set; }
    public string? IdsFacturas { get; set; }

    public List<int> Facturas => (IdsFacturas ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList();

    /// <summary>Error o PDF sin factura: alguien tiene que mirarlo.</summary>
    public bool ConIncidencia => Resultado is ResultadoImportacion.Error or ResultadoImportacion.SinFactura;

    /// <summary>Mensaje en una línea (para la tabla); el completo va en la descripción.</summary>
    public string MensajeCorto => (Mensaje ?? "").Replace("\r", " ").Replace("\n", " · ");

    public string ResultadoTexto => Resultado switch
    {
        ResultadoImportacion.Procesada => "Registrada",
        ResultadoImportacion.Duplicada => "Duplicada",
        ResultadoImportacion.SinFactura => "Sin factura",
        ResultadoImportacion.Error => "Error",
        _ => Resultado,
    };

    /// <summary>Enlace al PDF en Google Drive (el importador lo deja en una subcarpeta de CEFER/Facturas).</summary>
    public string? EnlaceDrive => Fuente == "drive" ? $"https://drive.google.com/file/d/{IdExterno}/view" : null;
}

/// <summary>Criterios del listado de importaciones.</summary>
public sealed class FiltroImportaciones
{
    /// <summary>procesada | duplicada | sin_factura | error | "incidencias" (error + sin_factura) | null = todos.</summary>
    public string? Resultado { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Texto { get; set; }
}

/// <summary>Último latido del servicio importador (dbo.ImportadorEstado).</summary>
public sealed record EstadoImportador(DateTime UltimaPasada, bool Correcta, string? Mensaje, string? Equipo, string? Version)
{
    /// <summary>Minutos sin noticias a partir de los cuales se avisa de que el servicio puede estar parado.</summary>
    public const int MinutosAviso = 30;

    public bool Parado => DateTime.Now - UltimaPasada > TimeSpan.FromMinutes(MinutosAviso);

    public string Hace
    {
        get
        {
            var t = DateTime.Now - UltimaPasada;
            if (t.TotalMinutes < 1) return "hace menos de un minuto";
            if (t.TotalMinutes < 60) return $"hace {(int)t.TotalMinutes} min";
            if (t.TotalHours < 48) return $"hace {(int)t.TotalHours} h";
            return $"hace {(int)t.TotalDays} días";
        }
    }
}
