namespace FacturasCefer.Models;

/// <summary>Estados de dbo.FacturaProveedores.Estado.</summary>
public enum EstadoFactura : byte
{
    Recibida = 1,
    Validada = 2,
    Pagada = 3,
    Rechazada = 4,
}

/// <summary>
/// Forma de pago (Proveedor.FormaPago y FacturaProveedores.FormaPago).
/// Con domiciliación el IBAN de la factura es la cuenta de cargo de CEFER, no la del proveedor.
/// Con tarjeta la factura ya está cobrada: se registra directamente como Pagada.
/// </summary>
public enum FormaPago : byte
{
    Transferencia = 1,
    Domiciliacion = 2,
    Tarjeta = 3,
}

/// <summary>Factura de proveedor (dbo.FacturaProveedores).</summary>
public sealed class FacturaProveedor
{
    public int Id { get; set; }
    public int IdProveedor { get; set; }
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
    public FormaPago FormaPago { get; set; } = FormaPago.Transferencia;

    /// <summary>Tarjeta con la que se pagó (marca + últimos 4 dígitos).</summary>
    public string? Tarjeta { get; set; }

    public EstadoFactura Estado { get; set; } = EstadoFactura.Recibida;
    public DateTime? FechaPago { get; set; }
    public string RutaPdf { get; set; } = "";
    public string? RutaPdfOriginal { get; set; }
    public short? PaginaInicio { get; set; }
    public short? PaginaFin { get; set; }
    public string? NombreOriginal { get; set; }
    public string? JsonExtraccionIA { get; set; }
    public string? Observaciones { get; set; }
}
