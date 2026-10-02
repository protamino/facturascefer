using System.Globalization;

namespace FacturasCefer.Exportacion.A3;

/// <summary>
/// Definición centralizada de los campos exportables: nombre del campo → cómo se obtiene y formatea
/// desde el DTO. El orden y la cabecera de cada columna se configuran en A3ExportConfig.Columnas.
/// </summary>
public static class A3Campos
{
    /// <summary>Formatea los valores según la configuración (decimales, fechas).</summary>
    public sealed class Formateador
    {
        private readonly NumberFormatInfo _num;
        private readonly string _fecha;

        public Formateador(A3ExportConfig cfg)
        {
            _num = new NumberFormatInfo { NumberDecimalSeparator = cfg.SeparadorDecimal, NumberGroupSeparator = "" };
            _fecha = cfg.FormatoFecha;
        }

        /// <summary>Importe con 2 decimales, sin separador de miles: 1000,00</summary>
        public string Importe(decimal? v) => v?.ToString("0.00", _num) ?? "";

        /// <summary>Porcentaje sin el símbolo %: 21 / 10 / 5,5</summary>
        public string Porcentaje(decimal? v) => v?.ToString("0.##", _num) ?? "";

        public string Fecha(DateTime? d) => d?.ToString(_fecha, CultureInfo.InvariantCulture) ?? "";
    }

    private static readonly Dictionary<string, Func<A3ReceivedInvoiceExportDTO, Formateador, string>> Campos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FechaFactura"] = (d, f) => f.Fecha(d.FechaFactura),
            ["NumeroFactura"] = (d, _) => d.NumeroFactura,
            ["NumeroFacturaCorto"] = (d, _) => d.NumeroFacturaCorto,
            ["NIFProveedor"] = (d, _) => d.NIFProveedor,
            ["NombreProveedor"] = (d, _) => d.NombreProveedor,
            ["CuentaProveedor"] = (d, _) => d.CuentaProveedor ?? "",
            ["CuentaGasto"] = (d, _) => d.CuentaGasto ?? "",
            ["BaseImponible"] = (d, f) => f.Importe(d.BaseImponible),
            // Con equivalencia configurada se exporta el código A3; si no, el porcentaje.
            ["TipoIVA"] = (d, f) => d.CodigoTipoIVA ?? f.Porcentaje(d.TipoIVA),
            ["PorcentajeIVA"] = (d, f) => f.Porcentaje(d.TipoIVA),
            ["CuotaIVA"] = (d, f) => f.Importe(d.CuotaIVA),
            ["TipoRecargoEquivalencia"] = (d, f) => f.Porcentaje(d.TipoRecargoEquivalencia),
            ["CuotaRecargoEquivalencia"] = (d, f) => f.Importe(d.CuotaRecargoEquivalencia),
            ["TipoRetencion"] = (d, f) => f.Porcentaje(d.TipoRetencion),
            ["CuotaRetencion"] = (d, f) => f.Importe(d.CuotaRetencion),
            ["TotalFactura"] = (d, f) => f.Importe(d.TotalFactura),
            ["Concepto"] = (d, _) => d.Concepto ?? "",
            ["FechaOperacion"] = (d, f) => f.Fecha(d.FechaOperacion),
            ["FechaContabilizacion"] = (d, f) => f.Fecha(d.FechaContabilizacion),
            ["SerieFactura"] = (d, _) => d.SerieFactura ?? "",
            ["CodigoPais"] = (d, _) => d.CodigoPais ?? "",
            ["ClaveOperacion"] = (d, _) => d.ClaveOperacion ?? "",
            // Trazabilidad (opcionales)
            ["IdFactura"] = (d, _) => d.IdFactura.ToString(CultureInfo.InvariantCulture),
            ["LineaFactura"] = (d, _) => d.Fila.ToString(CultureInfo.InvariantCulture),
        };

    public static IEnumerable<string> Disponibles => Campos.Keys;

    public static bool Existe(string campo) => Campos.ContainsKey(campo);

    public static string Valor(string campo, A3ReceivedInvoiceExportDTO d, Formateador f) =>
        Campos.TryGetValue(campo, out var get) ? get(d, f) : "";

    /// <summary>Columnas por defecto: las 21 de la especificación, en su orden y con su nombre.</summary>
    public static List<A3Columna> ColumnasPorDefecto() => new[]
    {
        "FechaFactura", "NumeroFactura", "NumeroFacturaCorto", "NIFProveedor", "NombreProveedor",
        "CuentaProveedor", "CuentaGasto", "BaseImponible", "TipoIVA", "CuotaIVA",
        "TipoRecargoEquivalencia", "CuotaRecargoEquivalencia", "TipoRetencion", "CuotaRetencion",
        "TotalFactura", "Concepto", "FechaOperacion", "FechaContabilizacion", "SerieFactura",
        "CodigoPais", "ClaveOperacion",
    }.Select(c => new A3Columna(c, c)).ToList();
}
