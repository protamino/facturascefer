using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace FacturasCefer.Services;

/// <summary>
/// Exporta una tabla de la app a Excel (.xlsx) tal como se ve: columnas visibles en su orden, filas con el orden
/// y filtro actuales. Importes como número, fechas como fecha.
/// </summary>
public static class ExportarExcel
{
    public static void Exportar(DataGrid grid, string nombre, Window? owner)
    {
        var filas = grid.Items.Cast<object>().Where(i => i != CollectionView.NewItemPlaceholder).ToList();
        if (filas.Count == 0)
        {
            App.Notificar("No hay filas que exportar con los filtros actuales.", TipoAviso.Atencion, owner);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Exportar a Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"{DateTime.Today:yyyy-MM-dd}-{nombre}.xlsx",
            InitialDirectory = Preferencias.Actual.CarpetaExcel is { } c && Directory.Exists(c)
                ? c : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(owner) != true) return;

        try
        {
            Escribir(grid, nombre, dlg.FileName);
            Preferencias.Actual.CarpetaExcel = Path.GetDirectoryName(dlg.FileName);
        }
        catch (IOException ex)
        {
            App.MostrarError(new IOException($"No se ha podido guardar «{dlg.FileName}» (¿está abierto en Excel?).", ex), owner);
            return;
        }

        App.Notificar($"Exportadas {filas.Count} filas a «{Path.GetFileName(dlg.FileName)}».", TipoAviso.Ok, owner);
        try
        {
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch
        {
            // Sin Excel instalado: el fichero queda guardado igualmente.
        }
    }

    /// <summary>Escribe el .xlsx con las filas y columnas visibles de la tabla. Devuelve el nº de filas.</summary>
    public static int Escribir(DataGrid grid, string nombre, string ruta)
    {
        var filas = grid.Items.Cast<object>().Where(i => i != CollectionView.NewItemPlaceholder).ToList();
        var columnas = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .OrderBy(c => c.DisplayIndex)
            .Select(c => (Titulo: Titulo(c), Ruta: Ruta(c), Formato: Formato(c)))
            .Where(c => c.Ruta is not null)
            .ToList();

        {
            using var wb = new XLWorkbook();
            var hoja = wb.Worksheets.Add(nombre.Length > 31 ? nombre[..31] : nombre);
            for (var c = 0; c < columnas.Count; c++)
                hoja.Cell(1, c + 1).Value = columnas[c].Titulo;

            for (var f = 0; f < filas.Count; f++)
            {
                for (var c = 0; c < columnas.Count; c++)
                {
                    var celda = hoja.Cell(f + 2, c + 1);
                    switch (Valor(filas[f], columnas[c].Ruta!))
                    {
                        case null:
                            break;
                        case decimal d:
                            celda.Value = d;
                            celda.Style.NumberFormat.Format = "#,##0.00";
                            break;
                        case int or short or long or double:
                            celda.Value = Convert.ToDouble(Valor(filas[f], columnas[c].Ruta!));
                            break;
                        case DateTime dt:
                            celda.Value = dt;
                            celda.Style.DateFormat.Format = columnas[c].Formato?.Contains("HH") == true ? "dd/mm/yyyy hh:mm" : "dd/mm/yyyy";
                            break;
                        case bool b:
                            celda.Value = b ? "Sí" : "";
                            break;
                        case var otro:
                            celda.Value = otro.ToString();
                            break;
                    }
                }
            }

            var cabecera = hoja.Range(1, 1, 1, columnas.Count);
            cabecera.Style.Font.Bold = true;
            cabecera.Style.Font.FontColor = XLColor.White;
            cabecera.Style.Fill.BackgroundColor = XLColor.FromHtml("#921F30");
            hoja.Range(1, 1, filas.Count + 1, columnas.Count).SetAutoFilter();
            hoja.SheetView.FreezeRows(1);
            hoja.Columns().AdjustToContents(8, 60);

            wb.SaveAs(ruta);
        }
        return filas.Count;
    }

    private static string Titulo(DataGridColumn c) => c.Header?.ToString() switch
    {
        "⚑" => "Revisar",
        null or "" => c.SortMemberPath ?? "",
        var h => h,
    };

    private static string? Ruta(DataGridColumn c) =>
        c is DataGridBoundColumn { Binding: Binding b } ? b.Path?.Path : string.IsNullOrEmpty(c.SortMemberPath) ? null : c.SortMemberPath;

    private static string? Formato(DataGridColumn c) => c is DataGridBoundColumn { Binding: Binding b } ? b.StringFormat : null;

    /// <summary>Lee una propiedad (admite rutas «A.B») por reflexión.</summary>
    private static object? Valor(object? obj, string ruta)
    {
        foreach (var parte in ruta.Split('.'))
        {
            if (obj is null) return null;
            obj = obj.GetType().GetProperty(parte, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
        }
        return obj;
    }
}
