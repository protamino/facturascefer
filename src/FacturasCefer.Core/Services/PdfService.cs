using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FacturasCefer.Services;

/// <summary>Operaciones con PDF (PdfSharp): nº de páginas y extracción de un rango de páginas.</summary>
public static class PdfService
{
    public static int ContarPaginas(string ruta)
    {
        using var doc = PdfReader.Open(ruta, PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    /// <summary>Copia las páginas [desde, hasta] (1-indexadas, inclusivas) de <paramref name="origen"/> a un PDF nuevo.</summary>
    public static void ExtraerPaginas(string origen, int desde, int hasta, string destino)
    {
        using var src = PdfReader.Open(origen, PdfDocumentOpenMode.Import);
        if (desde < 1 || hasta > src.PageCount || desde > hasta)
            throw new ReglaNegocioException($"Rango de páginas {desde}-{hasta} no válido (el PDF tiene {src.PageCount}).");

        using var dst = new PdfDocument();
        for (var i = desde - 1; i < hasta; i++)
            dst.AddPage(src.Pages[i]);
        dst.Save(destino);
    }

    /// <summary>Une varios PDFs en uno (usado para pruebas con PDFs de varias facturas).</summary>
    public static void Unir(IEnumerable<string> origenes, string destino)
    {
        using var dst = new PdfDocument();
        foreach (var ruta in origenes)
        {
            using var src = PdfReader.Open(ruta, PdfDocumentOpenMode.Import);
            foreach (var page in src.Pages)
                dst.AddPage(page);
        }
        dst.Save(destino);
    }
}
