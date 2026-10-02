using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using FacturasCefer.Exportacion.A3;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Dialogs;

/// <summary>
/// Exportaciones ▸ Facturas recibidas ▸ a3ASESOR | con: filtra, muestra incidencias, permite elegir
/// facturas y genera el CSV para el Importador de Datos de A3.
/// </summary>
public partial class ExportarA3Dialog : Window
{
    /// <summary>Fila del diálogo (una por factura).</summary>
    public sealed class FilaFactura : INotifyPropertyChanged
    {
        private bool _seleccionada;

        public FilaFactura(A3FacturaPreparada p)
        {
            Preparada = p;
            _seleccionada = !p.TieneErrores && p.Origen.UltimaExportacion is null;
        }

        public A3FacturaPreparada Preparada { get; }
        public bool Exportable => !Preparada.TieneErrores;
        public string Nivel => Preparada.TieneErrores ? "Error" : Preparada.Incidencias.Count > 0 ? "Aviso" : "OK";
        public string Icono => Preparada.TieneErrores ? "✖" : Preparada.Incidencias.Count > 0 ? "⚠" : "✔";
        public DateTime Fecha => Preparada.Origen.FechaFactura;
        public string Numero => Preparada.Origen.NumeroFactura;
        public string Proveedor => Preparada.Origen.Proveedor;
        public decimal Total => Preparada.Origen.Total;
        public int Filas => Preparada.Filas.Count;
        public DateTime? Exportada => Preparada.Origen.UltimaExportacion;
        public string TextoIncidencias => string.Join("  ·  ", Preparada.Incidencias.Select(i => i.ToString()));
        public string TextoIncidenciasCompleto => string.Join("\n", Preparada.Incidencias.Select(i => i.ToString()));

        public bool Seleccionada
        {
            get => _seleccionada;
            set
            {
                var v = value && Exportable;
                if (_seleccionada == v) return;
                _seleccionada = v;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seleccionada)));
                Cambio?.Invoke(this, EventArgs.Empty);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? Cambio;
    }

    private List<FilaFactura> _filas = new();

    public ExportarA3Dialog()
    {
        InitializeComponent();
        // Por defecto: mes anterior completo.
        var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DpDesde.SelectedDate = inicioMes.AddMonths(-1);
        DpHasta.SelectedDate = inicioMes.AddDays(-1);
        TxtRuta.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"{DateTime.Today:yyyy-MM-dd}-A3-facturas-recibidas.csv");
        Loaded += async (_, _) => await BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        var estados = new List<EstadoFactura>();
        if (ChkValidada.IsChecked == true) estados.Add(EstadoFactura.Validada);
        if (ChkPagada.IsChecked == true) estados.Add(EstadoFactura.Pagada);
        if (ChkRecibida.IsChecked == true) estados.Add(EstadoFactura.Recibida);

        IsEnabled = false;
        try
        {
            var preparadas = await App.ExportacionA3.PrepararAsync(DpDesde.SelectedDate, DpHasta.SelectedDate, estados,
                ChkExportadas.IsChecked == true);
            _filas = preparadas.Select(p => new FilaFactura(p)).ToList();
            foreach (var f in _filas) f.Cambio += (_, _) => ActualizarResumen();
            Grid.ItemsSource = _filas;
            MostrarConfiguracion();
            ActualizarResumen();
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, this);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void MostrarConfiguracion()
    {
        var cfg = App.ExportacionA3.Configuracion;
        var origen = File.Exists(A3ExportConfig.RutaPorDefecto) ? A3ExportConfig.NombreFichero : "valores por defecto (no hay a3-exportacion.json)";
        var desconocidas = new A3CsvWriter(cfg).ColumnasDesconocidas().ToList();
        TxtConfig.Text = $"Configuración: {origen}  ·  separador «{cfg.Separador}»  ·  decimal «{cfg.SeparadorDecimal}»  ·  " +
                         $"{cfg.Columnas.Count} columnas  ·  UTF-8{(cfg.Bom ? " con BOM" : "")}" +
                         (desconocidas.Count > 0 ? $"  ·  ⚠ campos desconocidos (se omiten): {string.Join(", ", desconocidas)}" : "");
    }

    private void ActualizarResumen()
    {
        var sel = _filas.Where(f => f.Seleccionada).ToList();
        TxtResumen.Text = $"{_filas.Count} factura(s): {_filas.Count(f => f.Nivel == "OK")} correctas, " +
                          $"{_filas.Count(f => f.Nivel == "Aviso")} con avisos, {_filas.Count(f => f.Nivel == "Error")} con errores (no exportables).   " +
                          $"Seleccionadas: {sel.Count} factura(s), {sel.Sum(f => f.Filas)} fila(s), total {Formato.Importe(sel.Sum(f => f.Total))} €";
        BtnExportar.IsEnabled = sel.Count > 0;
    }

    private void BtnBuscar_Click(object sender, RoutedEventArgs e) => _ = BuscarAsync();

    private void BtnTodas_Click(object sender, RoutedEventArgs e)
    {
        foreach (var f in _filas) f.Seleccionada = f.Exportable;
    }

    private void BtnNinguna_Click(object sender, RoutedEventArgs e)
    {
        foreach (var f in _filas) f.Seleccionada = false;
    }

    private void BtnCopiar_Click(object sender, RoutedEventArgs e)
    {
        var texto = string.Join("\r\n\r\n", _filas.Where(f => f.Preparada.Incidencias.Count > 0).Select(f =>
            $"Factura {f.Numero} ({f.Proveedor}, {f.Fecha:dd/MM/yyyy})\r\n" +
            string.Join("\r\n", f.Preparada.Incidencias.Select(i => "  " + i))));
        if (texto.Length == 0) texto = "Sin incidencias.";
        Clipboard.SetText(texto);
        MessageBox.Show(this, "Incidencias copiadas al portapapeles.", "Exportar a A3", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRuta_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar exportación para a3ASESOR",
            Filter = "CSV (*.csv)|*.csv",
            FileName = Path.GetFileName(TxtRuta.Text),
            InitialDirectory = Path.GetDirectoryName(TxtRuta.Text),
        };
        if (dlg.ShowDialog(this) == true) TxtRuta.Text = dlg.FileName;
    }

    private async void BtnExportar_Click(object sender, RoutedEventArgs e)
    {
        var sel = _filas.Where(f => f.Seleccionada).Select(f => f.Preparada).ToList();
        if (sel.Count == 0) return;
        var ruta = TxtRuta.Text.Trim();
        if (ruta.Length == 0 || Path.GetDirectoryName(ruta) is not { Length: > 0 } carpeta || !Directory.Exists(carpeta))
        {
            MessageBox.Show(this, "Indica un archivo de destino en una carpeta existente.", "Exportar a A3",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (File.Exists(ruta) && MessageBox.Show(this, $"El archivo ya existe:\n{ruta}\n\n¿Sobrescribirlo?", "Exportar a A3",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        var yaExportadas = sel.Count(s => s.Origen.UltimaExportacion is not null);
        if (yaExportadas > 0 && MessageBox.Show(this,
                $"{yaExportadas} de las facturas seleccionadas ya se exportaron a A3. Si las vuelves a importar en A3 se duplicarán.\n\n¿Exportarlas de nuevo?",
                "Exportar a A3", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        IsEnabled = false;
        try
        {
            var r = await App.ExportacionA3.ExportarAsync(sel, ruta, App.Usuario.IdUsuario);
            if (MessageBox.Show(this,
                    $"Exportación generada:\n{r.Archivo}\n\n{r.Facturas} factura(s), {r.Filas} fila(s).\n\n¿Abrir la carpeta?",
                    "Exportar a A3", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{r.Archivo}\"") { UseShellExecute = true });
        }
        catch (IOException ex)
        {
            App.MostrarError(new ReglaNegocioException($"No se puede escribir el archivo (¿está abierto en Excel?): {ex.Message}"), this);
            return;
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, this);
            return;
        }
        finally
        {
            IsEnabled = true;
        }
        await BuscarAsync();
    }
}
