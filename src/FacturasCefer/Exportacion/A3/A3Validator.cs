using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Exportacion.A3;

public enum A3Nivel { Aviso, Error }

/// <summary>Incidencia de una factura. Las de nivel Error impiden exportarla; los avisos no.</summary>
public sealed record A3Incidencia(A3Nivel Nivel, string Mensaje)
{
    public override string ToString() => (Nivel == A3Nivel.Error ? "✖ " : "⚠ ") + Mensaje;
}

/// <summary>Capa 3: validaciones previas a la generación del archivo.</summary>
public sealed class A3Validator
{
    private const decimal Tolerancia = 0.01m;
    private readonly A3ExportConfig _cfg;

    public A3Validator(A3ExportConfig cfg) => _cfg = cfg;

    public List<A3Incidencia> Validar(A3FacturaOrigen f, IReadOnlyList<A3ReceivedInvoiceExportDTO> filas)
    {
        var inc = new List<A3Incidencia>();
        void Error(string m) => inc.Add(new A3Incidencia(A3Nivel.Error, m));
        void Aviso(string m) => inc.Add(new A3Incidencia(A3Nivel.Aviso, m));

        if (string.IsNullOrWhiteSpace(f.NumeroFactura)) Error("Falta el número de factura.");
        if (f.FechaFactura == default) Error("Falta la fecha de factura.");
        if (string.IsNullOrWhiteSpace(f.Proveedor)) Error("Falta el proveedor.");
        if (string.IsNullOrWhiteSpace(f.CIF)) Error("Falta el NIF del proveedor.");
        else if (!Validaciones.CifValido(f.CIF)) Aviso($"El NIF «{f.CIF}» no es un NIF/CIF/NIE español válido (¿proveedor extranjero?).");
        if (f.Estado == EstadoFactura.Rechazada) Error("La factura está rechazada/anulada.");

        if (filas.Count == 0) Error("La factura no tiene base imponible ni impuestos.");
        if (f.Impuestos.Count == 0) Aviso("Factura sin desglose de impuestos: se exporta con los datos de la cabecera.");

        // Cuentas
        if (filas.Any(x => string.IsNullOrWhiteSpace(x.CuentaProveedor)))
        {
            const string m = "No existe cuenta contable asociada al proveedor (ficha del proveedor o equivalencias).";
            if (_cfg.CuentaProveedorObligatoria) Error(m); else Aviso(m);
        }
        if (filas.Any(x => string.IsNullOrWhiteSpace(x.CuentaGasto)))
        {
            const string m = "Falta la cuenta de gasto (en la factura o en alguna línea del desglose).";
            if (_cfg.CuentaGastoObligatoria) Error(m); else Aviso(m);
        }

        // Importes por línea
        foreach (var l in A3Transformer.Lineas(f))
        {
            var esperada = Math.Round(l.BaseImponible * l.PorcIVA / 100m, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(esperada - l.CuotaIVA) > Tolerancia)
                Aviso($"La cuota de IVA {Formato.Importe(l.CuotaIVA)} no corresponde a base {Formato.Importe(l.BaseImponible)} × {l.PorcIVA:0.##} % " +
                      $"({Formato.Importe(esperada)}). ¿Hay varios tipos de IVA sin desglosar?");
            if (l.PorcIVA is not (0 or 4 or 5 or 10 or 21))
                Aviso($"Tipo de IVA poco habitual: {l.PorcIVA:0.##} %.");
        }
        if (filas.All(x => x.BaseImponible == 0) && f.Total != 0) Error("La base imponible es 0 y el total no.");

        // Cuadre de la factura: bases + IVA + recargo − retención = total
        var lineas = A3Transformer.Lineas(f);
        var calculado = Desglose.TotalCalculado(lineas, f.CuotaIRPF);
        if (Math.Abs(calculado - f.Total) > Tolerancia)
            Error($"La suma de bases + IVA + recargo − retención ({Formato.Importe(calculado)}) no coincide con el total ({Formato.Importe(f.Total)}).");

        if (f.UltimaExportacion is { } ult) Aviso($"Ya exportada a A3 el {ult:dd/MM/yyyy HH:mm}.");
        return inc;
    }
}
