using System.IO;
using System.Text;

namespace FacturasCefer.Exportacion.A3;

/// <summary>
/// Capa 4: genera el CSV (UTF-8, con BOM por defecto, separador configurable, fin de línea CRLF).
/// Escapado estándar RFC 4180: los campos con separador, comillas o saltos de línea van entre comillas
/// y las comillas internas se duplican.
/// </summary>
public sealed class A3CsvWriter
{
    private readonly A3ExportConfig _cfg;

    public A3CsvWriter(A3ExportConfig cfg) => _cfg = cfg;

    public string Generar(IEnumerable<A3ReceivedInvoiceExportDTO> filas)
    {
        var columnas = _cfg.Columnas.Where(c => A3Campos.Existe(c.Campo)).ToList();
        var f = new A3Campos.Formateador(_cfg);
        var sb = new StringBuilder();
        if (_cfg.IncluirCabecera)
            sb.Append(string.Join(_cfg.Separador, columnas.Select(c => Escapar(c.Cabecera)))).Append("\r\n");
        foreach (var fila in filas)
            sb.Append(string.Join(_cfg.Separador, columnas.Select(c => Escapar(A3Campos.Valor(c.Campo, fila, f))))).Append("\r\n");
        return sb.ToString();
    }

    public void Escribir(string ruta, IEnumerable<A3ReceivedInvoiceExportDTO> filas) =>
        File.WriteAllText(ruta, Generar(filas), new UTF8Encoding(encoderShouldEmitUTF8Identifier: _cfg.Bom));

    public string Escapar(string? valor)
    {
        var s = valor ?? "";
        var hayQueCitar = s.Contains(_cfg.Separador) || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        return hayQueCitar ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Columnas configuradas con un campo inexistente (para avisar en el diálogo).</summary>
    public IEnumerable<string> ColumnasDesconocidas() =>
        _cfg.Columnas.Where(c => !A3Campos.Existe(c.Campo)).Select(c => c.Campo);
}
