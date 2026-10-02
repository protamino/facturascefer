namespace FacturasCefer.Exportacion.A3;

/// <summary>
/// Una fila de la exportación a a3ASESOR | con. Una factura genera una fila por cada combinación
/// distinta de cuenta de gasto × % IVA × % recargo; todas repiten NumeroFactura, fecha y proveedor.
/// Los importes son decimal; el formato (coma/punto) lo aplica el generador del CSV.
/// </summary>
public sealed class A3ReceivedInvoiceExportDTO
{
    // Trazabilidad (no son columnas por defecto)
    public int IdFactura { get; init; }
    public int Fila { get; init; }
    public int FilasFactura { get; init; }

    public DateTime FechaFactura { get; init; }
    public string NumeroFactura { get; init; } = "";
    public string NumeroFacturaCorto { get; init; } = "";
    public string NIFProveedor { get; init; } = "";
    public string NombreProveedor { get; init; } = "";
    public string? CuentaProveedor { get; init; }
    public string? CuentaGasto { get; init; }

    public decimal BaseImponible { get; init; }
    public decimal TipoIVA { get; init; }
    public decimal CuotaIVA { get; init; }

    /// <summary>Código A3 del tipo de IVA si hay equivalencia configurada; si no, null (se exporta el %).</summary>
    public string? CodigoTipoIVA { get; init; }

    public decimal? TipoRecargoEquivalencia { get; init; }
    public decimal? CuotaRecargoEquivalencia { get; init; }
    public decimal? TipoRetencion { get; init; }
    public decimal? CuotaRetencion { get; init; }
    public decimal? TotalFactura { get; init; }

    public string? Concepto { get; init; }
    public DateTime? FechaOperacion { get; init; }
    public DateTime? FechaContabilizacion { get; init; }
    public string? SerieFactura { get; init; }
    public string? CodigoPais { get; init; }
    public string? ClaveOperacion { get; init; }
}
