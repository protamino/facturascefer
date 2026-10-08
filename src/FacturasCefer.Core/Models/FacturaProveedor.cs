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

/// <summary>Cómo entró la factura: desde la app o por el importador automático (correo → Drive).</summary>
public enum OrigenFactura : byte
{
    Manual = 1,
    Correo = 2,
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

    public string? CuentaContable { get; set; }

    public EstadoFactura Estado { get; set; } = EstadoFactura.Recibida;
    public DateTime? FechaPago { get; set; }
    public string RutaPdf { get; set; } = "";
    public string? RutaPdfOriginal { get; set; }
    public short? PaginaInicio { get; set; }
    public short? PaginaFin { get; set; }
    public string? NombreOriginal { get; set; }
    public string? JsonExtraccionIA { get; set; }
    public string? Observaciones { get; set; }

    public OrigenFactura Origen { get; set; } = OrigenFactura.Manual;

    /// <summary>Requiere revisión humana (alta automática, IBAN distinto, no cuadra, datos dudosos…).</summary>
    public bool Revisar { get; set; }
    public string? MotivoRevision { get; set; }

    /// <summary>Desglose de impuestos (al menos una línea). La cabecera se calcula como su suma.</summary>
    public List<FacturaImpuesto> Impuestos { get; set; } = new();
}
