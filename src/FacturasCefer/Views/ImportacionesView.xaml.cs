using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Views;

/// <summary>Registro del importador automático (dbo.FacturaImportacion) y estado del servicio.</summary>
public partial class ImportacionesView : UserControl
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly bool _listo;
    private bool _aplicando;
    private int _cargaActual;

    /// <summary>Pide ir a «Subir facturas».</summary>
    public event EventHandler? IrASubir;

    public ImportacionesView()
    {
        InitializeComponent();
        TablaPersistente.Registrar(Grid, "importaciones");
        DpDesde.SelectedDate = DateTime.Today.AddDays(-30);
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = CargarAsync(); };
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) _ = CargarAsync(); };
        _listo = true;
        ActualizarBotones();
    }

    private RegistroImportacion? Seleccionada => Grid.SelectedItem as RegistroImportacion;

    /// <summary>Muestra solo las importaciones con incidencias de los últimos 7 días (desde Inicio).</summary>
    public void MostrarIncidencias(bool soloIncidencias)
    {
        _aplicando = true;
        CmbResultado.SelectedIndex = soloIncidencias ? 1 : 0;
        DpDesde.SelectedDate = DateTime.Today.AddDays(soloIncidencias ? -7 : -30);
        DpHasta.SelectedDate = null;
        TxtBuscar.Text = "";
        _aplicando = false;
        if (IsVisible) _ = CargarAsync();
    }

    private async Task CargarAsync()
    {
        var carga = ++_cargaActual;
        Estado.Cargando = true;
        try
        {
            var filtro = new FiltroImportaciones
            {
                Resultado = (CmbResultado.SelectedItem as ComboBoxItem)?.Tag as string is { Length: > 0 } r ? r : null,
                Desde = DpDesde.SelectedDate,
                Hasta = DpHasta.SelectedDate,
                Texto = TxtBuscar.Text,
            };
            var lista = await App.Importaciones.ListarAsync(filtro);
            var estado = await App.Importaciones.EstadoAsync();
            if (carga != _cargaActual) return;

            Grid.ItemsSource = lista;
            Estado.Vacio = lista.Count == 0;
            var incidencias = lista.Count(i => i.ConIncidencia);
            TxtContador.Text = $"{lista.Count} PDF" +
                               $"   ·   {lista.Count(i => i.Resultado == ResultadoImportacion.Procesada)} registrado(s)" +
                               (incidencias > 0 ? $"   ·   ⚠ {incidencias} con incidencias" : "") +
                               (lista.Count >= 2000 ? "   ·   (se muestran los 2000 más recientes)" : "");
            MostrarServicio(estado);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
        finally
        {
            if (carga == _cargaActual) Estado.Cargando = false;
        }
        ActualizarBotones();
    }

    private void MostrarServicio(EstadoImportador? e)
    {
        if (e is null)
        {
            PanelServicio.Style = Tema.EstiloAviso(TipoAviso.Atencion);
            TxtServicio.Text = "El servicio importador todavía no ha registrado ninguna revisión de la carpeta de Drive " +
                               "(se registra a partir de la versión 1.8.0 del servicio).";
            return;
        }
        var detalle = $"Última revisión de la carpeta de Drive: {e.UltimaPasada:dd/MM/yyyy HH:mm} ({e.Hace})" +
                      $" · {e.Equipo} · versión {e.Version}" + (string.IsNullOrWhiteSpace(e.Mensaje) ? "" : $" · {e.Mensaje}");
        if (e.Parado)
        {
            PanelServicio.Style = Tema.EstiloAviso(TipoAviso.Error);
            TxtServicio.Text = $"⚠ El servicio lleva más de {EstadoImportador.MinutosAviso} minutos sin revisar Drive: puede estar parado. " +
                               $"Comprueba «FacturasCefer.Importador» en services.msc.\n{detalle}";
        }
        else if (!e.Correcta)
        {
            PanelServicio.Style = Tema.EstiloAviso(TipoAviso.Error);
            TxtServicio.Text = "⚠ La última revisión terminó con error.\n" + detalle;
        }
        else
        {
            PanelServicio.Style = Tema.EstiloAviso(TipoAviso.Ok);
            TxtServicio.Text = "✔ El servicio funciona. " + detalle;
        }
    }

    private void ActualizarBotones()
    {
        if (!_listo) return;
        var s = Seleccionada;
        BtnFicha.IsEnabled = s?.Facturas.Count > 0;
        BtnDrive.IsEnabled = s?.EnlaceDrive is not null;
        TxtDetalle.Text = s is null
            ? ""
            : $"{s.NombreFichero}   ·   {s.Remitente}   ·   {s.Asunto}\n{s.Mensaje}";
    }

    private void Filtro_Changed(object sender, RoutedEventArgs e)
    {
        if (!_listo || _aplicando) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => Filtro_Changed(sender, e);
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) => ActualizarBotones();
    private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = CargarAsync();
    private void BtnExcel_Click(object sender, RoutedEventArgs e) => ExportarExcel.Exportar(Grid, "importaciones", Window.GetWindow(this));
    private void BtnSubir_Click(object sender, RoutedEventArgs e) => IrASubir?.Invoke(this, EventArgs.Empty);

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Seleccionada?.Facturas.Count > 0) AbrirFicha();
        else if (Seleccionada?.EnlaceDrive is not null) AbrirDrive();
    }

    private void BtnFicha_Click(object sender, RoutedEventArgs e) => AbrirFicha();
    private void BtnDrive_Click(object sender, RoutedEventArgs e) => AbrirDrive();

    private void AbrirFicha()
    {
        if (Seleccionada?.Facturas is not { Count: > 0 } ids) return;
        foreach (var id in ids)
            new FacturaDialog(id) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void AbrirDrive()
    {
        if (Seleccionada?.EnlaceDrive is not { } url) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
    }
}
