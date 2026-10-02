namespace FacturasCefer.Models;

/// <summary>Cuenta contable del catálogo (dbo.CuentaContable). Código de 8 dígitos.</summary>
public sealed class CuentaContable
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool Baja { get; set; }
    public DateTime FechaAlta { get; set; }

    /// <summary>Nº de proveedores que la tienen por defecto y nº de facturas asignadas (solo en el listado).</summary>
    public int Proveedores { get; set; }
    public int Facturas { get; set; }

    public string Texto => $"{Codigo} — {Descripcion}" + (Baja ? " (baja)" : "");
    public override string ToString() => Texto;

    public CuentaContable Clone() => (CuentaContable)MemberwiseClone();
}
