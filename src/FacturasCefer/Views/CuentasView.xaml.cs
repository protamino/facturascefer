using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FacturasCefer.Controls;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;

namespace FacturasCefer.Views;

/// <summary>Catálogo de cuentas contables: alta, edición de la descripción y baja lógica.</summary>
public partial class CuentasView : UserControl
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private int _cargaActual;

    public CuentasView()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = CargarAsync(); };
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) _ = CargarAsync(Seleccionada?.Codigo); };
        ActualizarBotones();
    }

    private CuentaContable? Seleccionada => Grid.SelectedItem as CuentaContable;

    private async Task CargarAsync(string? seleccionar = null)
    {
        var carga = ++_cargaActual;
        try
        {
            var lista = await App.Cuentas.BuscarAsync(TxtBuscar.Text, ChkBajas.IsChecked == true);
            if (carga != _cargaActual) return;
            Grid.ItemsSource = lista;
            TxtContador.Text = $"{lista.Count} cuenta(s)";
            if (seleccionar is not null && lista.FirstOrDefault(c => c.Codigo == seleccionar) is { } c)
            {
                Grid.SelectedItem = c;
                Grid.ScrollIntoView(c);
            }
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
    }

    private void ActualizarBotones()
    {
        var c = Seleccionada;
        BtnEditar.IsEnabled = c is not null;
        BtnBaja.IsEnabled = c is not null;
        BtnBaja.Content = c?.Baja == true ? "Reactivar" : "Dar de baja";
    }

    private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Filtro_Changed(object sender, RoutedEventArgs e) => _ = CargarAsync();
    private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = CargarAsync(Seleccionada?.Codigo);
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) => ActualizarBotones();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Seleccionada is { } c && e.OriginalSource is FrameworkElement { DataContext: CuentaContable }) Editar(c);
    }

    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CuentaDialog(new CuentaContable(), esNueva: true) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) _ = CargarAsync(dlg.Resultado!.Codigo);
    }

    private void BtnEditar_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionada is { } c) Editar(c);
    }

    private void Editar(CuentaContable c)
    {
        var dlg = new CuentaDialog(c.Clone(), esNueva: false) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) _ = CargarAsync(c.Codigo);
    }

    private async void BtnBaja_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionada is not { } c) return;
        var owner = Window.GetWindow(this);
        var aviso = !c.Baja && (c.Proveedores > 0 || c.Facturas > 0)
            ? $"\n\nLa usan {c.Proveedores} proveedor(es) y {c.Facturas} factura(s); las conservarán, pero no se podrá elegir para nuevas."
            : "";
        if (MessageBox.Show(owner, $"¿{(c.Baja ? "Reactivar" : "Dar de baja")} la cuenta {c.Texto}?{aviso}", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await App.Cuentas.CambiarBajaAsync(c.Codigo, !c.Baja);
            CuentaSelector.InvalidarCache();
            await CargarAsync(c.Codigo);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, owner);
        }
    }
}
