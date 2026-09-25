using System.Text.Json.Serialization;

namespace FacturasCefer.Models;

/// <summary>Una factura tal como la devuelve la IA (JSON de structured outputs, snake_case).</summary>
public sealed class FacturaExtraida
{
    [JsonPropertyName("pagina_inicio")] public int PaginaInicio { get; set; }
    [JsonPropertyName("pagina_fin")] public int PaginaFin { get; set; }

    [JsonPropertyName("proveedor_razon_social")] public string? ProveedorRazonSocial { get; set; }
    [JsonPropertyName("proveedor_cif")] public string? ProveedorCif { get; set; }
    [JsonPropertyName("proveedor_direccion")] public string? ProveedorDireccion { get; set; }
    [JsonPropertyName("proveedor_cp")] public string? ProveedorCp { get; set; }
    [JsonPropertyName("proveedor_poblacion")] public string? ProveedorPoblacion { get; set; }
    [JsonPropertyName("proveedor_provincia")] public string? ProveedorProvincia { get; set; }
    [JsonPropertyName("proveedor_email")] public string? ProveedorEmail { get; set; }
    [JsonPropertyName("proveedor_telefono")] public string? ProveedorTelefono { get; set; }
    [JsonPropertyName("iban")] public string? Iban { get; set; }

    /// <summary>"transferencia", "domiciliacion", "tarjeta", "otra" o "" si no consta.</summary>
    [JsonPropertyName("forma_pago")] public string? FormaPago { get; set; }

    /// <summary>Si se pagó con tarjeta: marca y últimos 4 dígitos (p. ej. "VISA 1234").</summary>
    [JsonPropertyName("tarjeta")] public string? Tarjeta { get; set; }

    [JsonPropertyName("numero_factura")] public string? NumeroFactura { get; set; }
    [JsonPropertyName("fecha_factura")] public string? FechaFactura { get; set; }
    [JsonPropertyName("fecha_vencimiento")] public string? FechaVencimiento { get; set; }
    [JsonPropertyName("concepto")] public string? Concepto { get; set; }

    [JsonPropertyName("base_imponible")] public decimal? BaseImponible { get; set; }
    [JsonPropertyName("porc_iva")] public decimal? PorcIva { get; set; }
    [JsonPropertyName("cuota_iva")] public decimal? CuotaIva { get; set; }
    [JsonPropertyName("porc_irpf")] public decimal? PorcIrpf { get; set; }
    [JsonPropertyName("cuota_irpf")] public decimal? CuotaIrpf { get; set; }
    [JsonPropertyName("total")] public decimal? Total { get; set; }

    /// <summary>Campos no encontrados o de baja confianza (nombres JSON).</summary>
    [JsonPropertyName("campos_dudosos")] public List<string> CamposDudosos { get; set; } = new();
}

public sealed class ResultadoExtraccion
{
    [JsonPropertyName("facturas")] public List<FacturaExtraida> Facturas { get; set; } = new();
}
