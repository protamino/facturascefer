namespace FacturasCefer.Models;

/// <summary>Proveedor (dbo.Proveedor).</summary>
public sealed class Proveedor
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string CIF { get; set; } = "";
    public string? Direccion { get; set; }
    public string? CP { get; set; }
    public string? Poblacion { get; set; }
    public string? Provincia { get; set; }
    public string Pais { get; set; } = "España";
    public string? IBAN { get; set; }
    public FormaPago FormaPago { get; set; } = FormaPago.Transferencia;

    /// <summary>Tarjeta habitual (marca + últimos 4 dígitos), si paga con tarjeta.</summary>
    public string? Tarjeta { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Observaciones { get; set; }
    public bool Baja { get; set; }
    public DateTime FechaAlta { get; set; }

    /// <summary>IBAN agrupado de 4 en 4 para mostrar.</summary>
    public string IbanFormateado => Services.Validaciones.FormatearIban(IBAN);
    public string FormaPagoTexto => Textos.FormaPago(FormaPago);

    public Proveedor Clone() => (Proveedor)MemberwiseClone();
}
