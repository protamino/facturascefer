using FacturasCefer.Services;

namespace FacturasCefer.Models;

public enum EstadoRevision { Pendiente, Guardada, Descartada }

/// <summary>Una factura detectada en un PDF, pendiente de revisar por el usuario.</summary>
public sealed class FacturaEnRevision
{
    public FacturaEnRevision(FacturaExtraida ia, string? jsonIa)
    {
        Ia = ia;
        JsonIa = jsonIa;
    }

    /// <summary>Datos de la factura; empiezan siendo los de la IA y recogen las correcciones del usuario.</summary>
    public FacturaExtraida Ia { get; }

    /// <summary>Respuesta original de la IA (sin correcciones), para FacturaProveedores.JsonExtraccionIA.</summary>
    public string? JsonIa { get; }

    public string? Observaciones { get; set; }

    /// <summary>Cuenta contable elegida (null: aún no se ha tocado → se toma la del proveedor).</summary>
    public string? CuentaContable { get; set; }

    /// <summary>Desglose editado por el usuario (null: aún no se ha tocado → se toma el de la IA).</summary>
    public List<FacturaImpuesto>? Impuestos { get; set; }
    public EstadoRevision Estado { get; set; } = EstadoRevision.Pendiente;
    public int? IdFactura { get; set; }
}

/// <summary>Un PDF subido: copia local temporal, sus facturas y el estado de la copia del original en red.</summary>
public sealed class PdfEnRevision
{
    public PdfEnRevision(string rutaLocal, string nombreOriginal)
    {
        RutaLocal = rutaLocal;
        NombreOriginal = nombreOriginal;
    }

    public string RutaLocal { get; }
    public string NombreOriginal { get; }
    public int Paginas { get; set; }
    public List<FacturaEnRevision> Facturas { get; } = new();
    public int Indice { get; set; }
    public OriginalGuardado Original { get; } = new();

    public FacturaEnRevision? Actual => Indice >= 0 && Indice < Facturas.Count ? Facturas[Indice] : null;
    public bool Terminado => Facturas.All(f => f.Estado != EstadoRevision.Pendiente);
}
