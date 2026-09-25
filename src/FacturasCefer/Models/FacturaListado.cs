using FacturasCefer.Services;

namespace FacturasCefer.Models;

/// <summary>Fila del listado de facturas (FacturaProveedores + datos del proveedor).</summary>
public sealed class FacturaListado
{
    public int Id { get; set; }
    public int IdProveedor { get; set; }
    public string Proveedor { get; set; } = "";
    public string CIF { get; set; } = "";
    public string? IbanProveedor { get; set; }
    public string NumeroFactura { get; set; } = "";
    public string? Concepto { get; set; }
    public DateTime FechaFactura { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public decimal? BaseImponible { get; set; }
    public decimal? PorcIVA { get; set; }
    public decimal? CuotaIVA { get; set; }
    public decimal? PorcIRPF { get; set; }
    public decimal? CuotaIRPF { get; set; }
    public decimal Total { get; set; }
    public string? IBAN { get; set; }
    public EstadoFactura Estado { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? MotivoRechazo { get; set; }
    public string RutaPdf { get; set; } = "";
    public string? RutaPdfOriginal { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaRegistro { get; set; }

    public string EstadoTexto => Textos.Estado(Estado);

    /// <summary>Pendiente de pago con el vencimiento ya pasado.</summary>
    public bool Vencida => Estado is EstadoFactura.Recibida or EstadoFactura.Validada
                           && FechaVencimiento is { } v && v.Date < DateTime.Today;

    public bool Anulada => Estado == EstadoFactura.Rechazada;

    /// <summary>El IBAN de la factura no es el que tiene ahora el proveedor.</summary>
    public bool IbanDistinto =>
        Validaciones.NormalizarIban(IBAN) is { Length: > 0 } i && i != Validaciones.NormalizarIban(IbanProveedor);

    public string IbanFormateado => Validaciones.FormatearIban(IBAN);
    public string AvisoIban => IbanDistinto ? "⚠" : "";
}

/// <summary>Fila de dbo.FacturaEstadoHistorico con el nombre del usuario.</summary>
public sealed record CambioEstado(DateTime Fecha, EstadoFactura? EstadoAnterior, EstadoFactura EstadoNuevo,
    string Usuario, string? Comentario)
{
    public string Cambio => EstadoAnterior is { } a
        ? $"{Textos.Estado(a)} → {Textos.Estado(EstadoNuevo)}"
        : Textos.Estado(EstadoNuevo);
}

/// <summary>Criterios del listado de facturas.</summary>
public sealed class FiltroFacturas
{
    public List<EstadoFactura> Estados { get; set; } = new();
    public int? IdProveedor { get; set; }
    public string? Texto { get; set; }
    public bool PorVencimiento { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public static class Textos
{
    public static string Estado(EstadoFactura e) => e switch
    {
        EstadoFactura.Recibida => "Recibida",
        EstadoFactura.Validada => "Validada",
        EstadoFactura.Pagada => "Pagada",
        EstadoFactura.Rechazada => "Rechazada/Anulada",
        _ => e.ToString(),
    };
}
