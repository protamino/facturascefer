using System.Globalization;
using FacturasCefer.Models;

namespace FacturasCefer.Exportacion.A3;

/// <summary>
/// Capa 2: transforma una factura interna en filas de exportación A3, aplicando reglas y equivalencias.
/// No inventa datos: lo que no existe ni está configurado queda vacío (y lo señala el validador).
/// </summary>
public sealed class A3Transformer
{
    private readonly A3ExportConfig _cfg;
    private readonly DateTime _fechaExportacion;

    public A3Transformer(A3ExportConfig cfg, DateTime fechaExportacion)
    {
        _cfg = cfg;
        _fechaExportacion = fechaExportacion;
    }

    /// <summary>Clave interna del % IVA para las equivalencias: "21", "10", "5,5"→"5.5".</summary>
    public static string ClaveTipo(decimal porc) => porc.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Líneas efectivas: el desglose, o una línea con la cabecera si la factura no tiene desglose.</summary>
    public static List<FacturaImpuesto> Lineas(A3FacturaOrigen f) =>
        f.Impuestos.Count > 0
            ? f.Impuestos
            : new List<FacturaImpuesto>
            {
                new() { BaseImponible = f.BaseImponible ?? 0, PorcIVA = f.PorcIVA ?? 0, CuotaIVA = f.CuotaIVA ?? 0 },
            };

    public List<A3ReceivedInvoiceExportDTO> Transformar(A3FacturaOrigen f)
    {
        var eq = _cfg.Equivalencias;
        var cif = (f.CIF ?? "").Trim();

        // Agrupar por cuenta de gasto efectiva × % IVA × % RE: nunca se suman importes de cuentas o tipos distintos.
        var grupos = Lineas(f)
            .Select(l => new { Linea = l, Cuenta = l.CuentaContable ?? f.CuentaGasto })
            .GroupBy(x => (x.Cuenta, x.Linea.PorcIVA, PorcRE: x.Linea.PorcRE))
            .Select(g => new
            {
                g.Key.Cuenta,
                g.Key.PorcIVA,
                g.Key.PorcRE,
                Base = g.Sum(x => x.Linea.BaseImponible),
                Iva = g.Sum(x => x.Linea.CuotaIVA),
                Re = g.Any(x => x.Linea.CuotaRE is not null) ? g.Sum(x => x.Linea.CuotaRE ?? 0) : (decimal?)null,
            })
            .ToList();

        var cuentaProveedor = Vacio(f.CuentaProveedor)
                              ?? Buscar(eq.CuentaProveedorPorCif, cif);
        var serie = Buscar(eq.SeriePorCif, cif);
        var pais = f.Pais is null ? null : Buscar(eq.Pais, f.Pais.Trim());
        var numCorto = NumeroCorto(f.NumeroFactura);
        DateTime? fechaOperacion = _cfg.FechaOperacion == "fecha_factura" ? f.FechaFactura : null;
        DateTime? fechaContab = _cfg.FechaContabilizacion switch
        {
            "fecha_factura" => f.FechaFactura,
            "fecha_exportacion" => _fechaExportacion.Date,
            _ => null,
        };

        var filas = new List<A3ReceivedInvoiceExportDTO>();
        for (var i = 0; i < grupos.Count; i++)
        {
            var g = grupos[i];
            var primera = i == 0;
            var claveTipo = ClaveTipo(g.PorcIVA);
            var cuentaGasto = g.Cuenta is null ? null : Buscar(eq.CuentaGasto, g.Cuenta) ?? g.Cuenta;
            var conRetencion = primera || !_cfg.RetencionSoloPrimeraFila;

            filas.Add(new A3ReceivedInvoiceExportDTO
            {
                IdFactura = f.Id,
                Fila = i + 1,
                FilasFactura = grupos.Count,
                FechaFactura = f.FechaFactura,
                NumeroFactura = f.NumeroFactura,
                NumeroFacturaCorto = numCorto,
                NIFProveedor = LimpiarNif(cif),
                NombreProveedor = f.Proveedor.Trim(),
                CuentaProveedor = cuentaProveedor,
                CuentaGasto = cuentaGasto,
                BaseImponible = g.Base,
                TipoIVA = g.PorcIVA,
                CuotaIVA = g.Iva,
                CodigoTipoIVA = Buscar(eq.TipoIva, claveTipo),
                TipoRecargoEquivalencia = g.Re is null ? null : g.PorcRE,
                CuotaRecargoEquivalencia = g.Re,
                TipoRetencion = conRetencion && (f.CuotaIRPF ?? 0) != 0 ? f.PorcIRPF : null,
                CuotaRetencion = conRetencion && (f.CuotaIRPF ?? 0) != 0 ? f.CuotaIRPF : null,
                TotalFactura = primera || !_cfg.TotalSoloPrimeraFila ? f.Total : null,
                Concepto = f.Concepto,
                FechaOperacion = fechaOperacion,
                FechaContabilizacion = fechaContab,
                SerieFactura = serie,
                CodigoPais = pais,
                ClaveOperacion = Buscar(eq.ClaveOperacionPorCif, cif) ?? Buscar(eq.ClaveOperacionPorTipoIva, claveTipo),
            });
        }
        return filas;
    }

    /// <summary>Últimos N caracteres del número completo (N configurable).</summary>
    public string NumeroCorto(string numero)
    {
        var n = Math.Max(1, _cfg.LongitudNumeroCorto);
        var s = (numero ?? "").Trim();
        return s.Length <= n ? s : s[^n..];
    }

    /// <summary>Solo limpieza de espacios; no se altera el contenido.</summary>
    public static string LimpiarNif(string nif) => new(nif.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Buscar(Dictionary<string, string> tabla, string clave)
    {
        if (tabla.Count == 0 || string.IsNullOrEmpty(clave)) return null;
        foreach (var (k, v) in tabla)
            if (string.Equals(k.Trim(), clave, StringComparison.OrdinalIgnoreCase))
                return Vacio(v);
        return null;
    }
}
