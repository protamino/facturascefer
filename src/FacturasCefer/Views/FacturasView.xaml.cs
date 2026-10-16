using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Views;

/// <summary>Listado de facturas con filtros, totales y cambios de estado (individuales o masivos).</summary>
public partial class FacturasView : UserControl
{
    private sealed record OpcionProveedor(int? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    /// <summary>Codigo null = todas; "" = sin cuenta.</summary>
    private sealed record OpcionCuenta(string? Codigo, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly bool _listo;
    private bool _cargandoProveedores;
    private int? _proveedorPendiente;
    private int _cargaActual;

    public FacturasView()
    {
        InitializeComponent();
        TablaPersistente.Registrar(Grid, "facturas");
        RestaurarFiltros();
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = CargarAsync(); };
        // Al entrar en la pestaña se refresca (puede haber facturas nuevas subidas en la otra).
        IsVisibleChanged += async (_, e) =>
        {
            if (e.NewValue is not true) return;
            await CargarProveedoresAsync();
            await CargarAsync();
        };
        _listo = true;
        ActualizarBotones();
    }

    private List<FacturaListado> Seleccionadas => Grid.SelectedItems.Cast<FacturaListado>().ToList();

    /// <summary>Vistas predefinidas (desde las tarjetas de Inicio).</summary>
    public enum VistaRapida { Pendientes, Vencidas, VencenPronto, VencidasOPronto, MesActual, PorRevisar, SinCuenta }

    /// <summary>Deja los filtros en una vista predefinida.</summary>
    /// <remarks>Llamar antes de mostrar la sección: la carga la hace el refresco al hacerse visible.</remarks>
    public async Task AplicarVistaAsync(VistaRapida vista)
    {
        _cargandoProveedores = true;
        RestablecerFiltros();
        switch (vista)
        {
            case VistaRapida.Vencidas:
                CmbVencimiento.SelectedIndex = (int)FiltroVencimiento.Vencidas;
                break;
            case VistaRapida.VencenPronto:
                CmbVencimiento.SelectedIndex = (int)FiltroVencimiento.VencenPronto;
                break;
            case VistaRapida.VencidasOPronto:
                CmbVencimiento.SelectedIndex = (int)FiltroVencimiento.VencidasOPronto;
                break;
            case VistaRapida.MesActual:
                ChkPagada.IsChecked = true;
                DpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                break;
            case VistaRapida.PorRevisar:
                ChkPagada.IsChecked = true;
                ChkRevisar.IsChecked = true;
                break;
            case VistaRapida.SinCuenta:
                ChkPagada.IsChecked = true;
                CmbCuenta.SelectedIndex = 1; // (Sin cuenta)
                break;
        }
        _cargandoProveedores = false;
        _debounce.Stop();
        if (IsVisible) await CargarAsync();
    }

    /// <summary>Filtros de la última sesión (estados, forma de pago, vencimiento, origen, revisar, campo de fecha).</summary>
    private void RestaurarFiltros()
    {
        var p = Preferencias.Actual;
        ChkRecibida.IsChecked = p.LeerBool("facturas.recibida") ?? true;
        ChkValidada.IsChecked = p.LeerBool("facturas.validada") ?? true;
        ChkPagada.IsChecked = p.LeerBool("facturas.pagada") ?? false;
        ChkRechazada.IsChecked = p.LeerBool("facturas.rechazada") ?? false;
        ChkRevisar.IsChecked = p.LeerBool("facturas.revisar") ?? false;
        static int Indice(ComboBox c, int? i) => i is { } v && v >= 0 && v < c.Items.Count ? v : 0;
        CmbFormaPago.SelectedIndex = Indice(CmbFormaPago, p.LeerInt("facturas.formapago"));
        CmbVencimiento.SelectedIndex = Indice(CmbVencimiento, p.LeerInt("facturas.vencimiento"));
        CmbOrigen.SelectedIndex = Indice(CmbOrigen, p.LeerInt("facturas.origen"));
        CmbCampoFecha.SelectedIndex = Indice(CmbCampoFecha, p.LeerInt("facturas.campofecha"));
    }

    private void RecordarFiltros()
    {
        var p = Preferencias.Actual;
        p.Poner("facturas.recibida", ChkRecibida.IsChecked == true);
        p.Poner("facturas.validada", ChkValidada.IsChecked == true);
        p.Poner("facturas.pagada", ChkPagada.IsChecked == true);
        p.Poner("facturas.rechazada", ChkRechazada.IsChecked == true);
        p.Poner("facturas.revisar", ChkRevisar.IsChecked == true);
        p.Poner("facturas.formapago", CmbFormaPago.SelectedIndex);
        p.Poner("facturas.vencimiento", CmbVencimiento.SelectedIndex);
        p.Poner("facturas.origen", CmbOrigen.SelectedIndex);
        p.Poner("facturas.campofecha", CmbCampoFecha.SelectedIndex);
    }

    /// <summary>Filtros por defecto: pendientes de pago, sin más criterios.</summary>
    private void RestablecerFiltros()
    {
        ChkRecibida.IsChecked = ChkValidada.IsChecked = true;
        ChkPagada.IsChecked = ChkRechazada.IsChecked = false;
        if (CmbProveedor.Items.Count > 0) CmbProveedor.SelectedIndex = 0;
        CmbFormaPago.SelectedIndex = 0;
        CmbVencimiento.SelectedIndex = 0;
        if (CmbCuenta.Items.Count > 0) CmbCuenta.SelectedIndex = 0;
        CmbOrigen.SelectedIndex = 0;
        ChkRevisar.IsChecked = false;
        CmbCampoFecha.SelectedIndex = 0;
        DpDesde.SelectedDate = DpHasta.SelectedDate = null;
        TxtBuscar.Text = "";
    }

    /// <summary>Muestra todas las facturas de un proveedor (desde la pestaña Proveedores).</summary>
    /// <remarks>Llamar antes de mostrar la pestaña: la carga la hace el refresco al hacerse visible.</remarks>
    public async Task FiltrarPorProveedorAsync(int idProveedor)
    {
        _proveedorPendiente = idProveedor;
        _cargandoProveedores = true;
        ChkRecibida.IsChecked = ChkValidada.IsChecked = ChkPagada.IsChecked = ChkRechazada.IsChecked = true;
        DpDesde.SelectedDate = DpHasta.SelectedDate = null;
        CmbFormaPago.SelectedIndex = 0;
        CmbVencimiento.SelectedIndex = 0;
        CmbCuenta.SelectedIndex = 0;
        CmbOrigen.SelectedIndex = 0;
        ChkRevisar.IsChecked = false;
        TxtBuscar.Text = "";
        _cargandoProveedores = false;
        _debounce.Stop();
        if (IsVisible)
        {
            await CargarProveedoresAsync();
            await CargarAsync();
        }
    }

    // ------------------------------------------------------------------ Carga

    private async Task CargarProveedoresAsync()
    {
        try
        {
            var seleccion = _proveedorPendiente ?? (CmbProveedor.SelectedItem as OpcionProveedor)?.Id;
            _proveedorPendiente = null;
            var proveedores = await App.Proveedores.BuscarAsync(null, incluirBajas: true);
            var opciones = new List<OpcionProveedor> { new(null, "(Todos)") };
            opciones.AddRange(proveedores.Select(p => new OpcionProveedor(p.Id, p.RazonSocial)));

            var cuentaSel = (CmbCuenta.SelectedItem as OpcionCuenta)?.Codigo;
            var cuentas = new List<OpcionCuenta> { new(null, "(Todas)"), new("", "(Sin cuenta)") };
            cuentas.AddRange((await App.Cuentas.BuscarAsync(null, incluirBajas: true)).Select(c => new OpcionCuenta(c.Codigo, c.Texto)));

            _cargandoProveedores = true;
            CmbProveedor.ItemsSource = opciones;
            CmbProveedor.SelectedItem = opciones.FirstOrDefault(o => o.Id == seleccion) ?? opciones[0];
            CmbCuenta.ItemsSource = cuentas;
            CmbCuenta.SelectedItem = cuentas.FirstOrDefault(o => o.Codigo == cuentaSel) ?? cuentas[0];
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
        finally
        {
            _cargandoProveedores = false;
        }
    }

    private FiltroFacturas LeerFiltro()
    {
        var f = new FiltroFacturas
        {
            IdProveedor = (CmbProveedor.SelectedItem as OpcionProveedor)?.Id,
            FormaPago = CmbFormaPago.SelectedIndex > 0 ? (FormaPago)CmbFormaPago.SelectedIndex : null,
            CuentaContable = (CmbCuenta.SelectedItem as OpcionCuenta)?.Codigo,
            Origen = CmbOrigen.SelectedIndex > 0 ? (OrigenFactura)CmbOrigen.SelectedIndex : null,
            Vencimiento = (FiltroVencimiento)Math.Max(0, CmbVencimiento.SelectedIndex),
            SoloRevisar = ChkRevisar.IsChecked == true,
            Texto = TxtBuscar.Text,
            PorVencimiento = CmbCampoFecha.SelectedIndex == 1,
            Desde = DpDesde.SelectedDate,
            Hasta = DpHasta.SelectedDate,
        };
        if (ChkRecibida.IsChecked == true) f.Estados.Add(EstadoFactura.Recibida);
        if (ChkValidada.IsChecked == true) f.Estados.Add(EstadoFactura.Validada);
        if (ChkPagada.IsChecked == true) f.Estados.Add(EstadoFactura.Pagada);
        if (ChkRechazada.IsChecked == true) f.Estados.Add(EstadoFactura.Rechazada);
        RecordarFiltros();
        return f;
    }

    private async Task CargarAsync(IReadOnlyCollection<int>? seleccionar = null)
    {
        var carga = ++_cargaActual;
        seleccionar ??= Seleccionadas.Select(f => f.Id).ToList();
        Estado.Cargando = true;
        try
        {
            var lista = await App.Facturas.BuscarAsync(LeerFiltro());
            if (carga != _cargaActual) return; // hay una carga más reciente
            Grid.ItemsSource = lista;
            foreach (var f in lista.Where(f => seleccionar.Contains(f.Id))) Grid.SelectedItems.Add(f);

            Estado.Vacio = lista.Count == 0;
            var vencidas = lista.Count(f => f.Vencida);
            var revisar = lista.Count(f => f.Revisar);
            var pronto = lista.Count(f => f.VencePronto);
            TxtResumen.Text = $"{lista.Count} factura(s)   ·   Total: {Formato.Importe(lista.Sum(f => f.Total))} €" +
                              (vencidas > 0 ? $"   ·   ⚠ {vencidas} vencida(s) sin pagar" : "") +
                              (pronto > 0 ? $"   ·   ⏰ {pronto} vence(n) en ≤ {FacturaListado.DiasAvisoVencimiento} días" : "") +
                              (revisar > 0 ? $"   ·   ⚑ {revisar} por revisar" : "");
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

    private void Filtro_Changed(object sender, RoutedEventArgs e)
    {
        if (!_listo || _cargandoProveedores) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => Filtro_Changed(sender, e);

    private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
    {
        _cargandoProveedores = true;
        RestablecerFiltros();
        _cargandoProveedores = false;
        _debounce.Stop();
        _ = CargarAsync();
    }

    private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = CargarAsync();

    private void BtnExcel_Click(object sender, RoutedEventArgs e) =>
        ExportarExcel.Exportar(Grid, "facturas", Window.GetWindow(this));

    // ------------------------------------------------------------------ Selección y botones

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) => ActualizarBotones();

    private void ActualizarBotones()
    {
        if (!_listo) return;
        var sel = Seleccionadas;
        var validar = sel.Any(f => f.Estado == EstadoFactura.Recibida);
        var pagar = sel.Any(f => f.Estado == EstadoFactura.Validada);
        var rechazar = sel.Any(f => f.Estado is EstadoFactura.Recibida or EstadoFactura.Validada);
        var deshacer = sel.Count > 0;
        var una = sel.Count == 1;

        BtnValidar.IsEnabled = MnuValidar.IsEnabled = validar;
        BtnPagar.IsEnabled = MnuPagar.IsEnabled = pagar;
        BtnRechazar.IsEnabled = MnuRechazar.IsEnabled = rechazar;
        BtnDeshacer.IsEnabled = MnuDeshacer.IsEnabled = deshacer;
        BtnFicha.IsEnabled = MnuFicha.IsEnabled = una;
        BtnPdf.IsEnabled = MnuPdf.IsEnabled = una;
        BtnCuenta.IsEnabled = MnuCuenta.IsEnabled = sel.Count > 0;
    }

    // ------------------------------------------------------------------ Cambios de estado

    private void BtnValidar_Click(object sender, RoutedEventArgs e) => _ = CambiarEstadoAsync(AccionEstado.Validar);
    private void BtnPagar_Click(object sender, RoutedEventArgs e) => _ = CambiarEstadoAsync(AccionEstado.Pagar);
    private void BtnRechazar_Click(object sender, RoutedEventArgs e) => _ = CambiarEstadoAsync(AccionEstado.Rechazar);
    private void BtnDeshacer_Click(object sender, RoutedEventArgs e) => _ = CambiarEstadoAsync(AccionEstado.Deshacer);

    private async Task CambiarEstadoAsync(AccionEstado accion)
    {
        var sel = Seleccionadas;
        if (sel.Count == 0) return;
        var owner = Window.GetWindow(this);

        // Antifraude: antes de validar o pagar, avisar si el IBAN de alguna factura no es el del proveedor.
        if (accion is AccionEstado.Validar or AccionEstado.Pagar)
        {
            var dudosas = sel.Where(f => f.IbanDistinto).ToList();
            if (dudosas.Count > 0 && MessageBox.Show(owner,
                    "⚠ El IBAN de estas facturas no coincide con el que tiene el proveedor:\n\n" +
                    string.Join("\n", dudosas.Select(f => $"  · {f.Proveedor} nº {f.NumeroFactura}: {f.IbanFormateado}")) +
                    "\n\nPuede ser un intento de fraude por cambio de cuenta. Confírmalo con el proveedor por un canal conocido.\n\n¿Continuar?",
                    "IBAN distinto", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
        }

        var dlg = new CambioEstadoDialog(accion, sel.Count) { Owner = owner };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var r = await App.Facturas.CambiarEstadoAsync(sel.Select(f => f.Id), accion, dlg.Fecha, dlg.Texto,
                App.Usuario.IdUsuario);
            if (r.Omitidas.Count > 0)
                App.Notificar(
                    $"Aplicado a {r.Aplicadas} factura(s). No se ha aplicado a {r.Omitidas.Count}:\n" +
                    string.Join("\n", r.Omitidas.Take(10)) + (r.Omitidas.Count > 10 ? "\n…" : ""),
                    TipoAviso.Atencion, owner);
            else
                App.Notificar($"Cambio aplicado a {r.Aplicadas} factura(s).", TipoAviso.Ok, owner);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, owner);
        }
        await CargarAsync(sel.Select(f => f.Id).ToList());
    }

    // ------------------------------------------------------------------ Cuenta contable

    private async void BtnCuenta_Click(object sender, RoutedEventArgs e)
    {
        var sel = Seleccionadas;
        if (sel.Count == 0) return;
        var owner = Window.GetWindow(this);
        var dlg = new CuentaBuscarDialog(null) { Owner = owner, Title = $"Asignar cuenta a {sel.Count} factura(s)" };
        if (dlg.ShowDialog() != true || dlg.Codigo is null) return;
        try
        {
            await App.Facturas.AsignarCuentaAsync(sel.Select(f => f.Id), dlg.Codigo, App.Usuario.IdUsuario);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, owner);
        }
        await CargarProveedoresAsync();
        await CargarAsync(sel.Select(f => f.Id).ToList());
    }

    // ------------------------------------------------------------------ Exportaciones

    private void BtnExportarA3_Click(object sender, RoutedEventArgs e)
    {
        new ExportarA3Dialog { Owner = Window.GetWindow(this) }.ShowDialog();
        _ = CargarAsync();
    }

    // ------------------------------------------------------------------ Ficha y PDF

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is FacturaListado f && e.OriginalSource is FrameworkElement { DataContext: FacturaListado })
            AbrirFicha(f);
    }

    private void BtnFicha_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is FacturaListado f) AbrirFicha(f);
    }

    private void AbrirFicha(FacturaListado f)
    {
        var dlg = new FacturaDialog(f.Id) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Modificada) _ = CargarAsync(new[] { f.Id });
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not FacturaListado f) return;
        try { Formato.Abrir(f.RutaPdf); }
        catch (Exception ex) { App.MostrarError(ex, Window.GetWindow(this)); }
    }
}
