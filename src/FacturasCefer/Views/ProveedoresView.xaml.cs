using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;

namespace FacturasCefer.Views;

public partial class ProveedoresView : UserControl
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private int _cargaActual;

    public ProveedoresView()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = CargarAsync(); };
        Loaded += (_, _) => { if (Grid.ItemsSource is null) _ = CargarAsync(); };
        ActualizarBotones();
    }

    private Proveedor? Seleccionado => Grid.SelectedItem as Proveedor;

    private async Task CargarAsync(int? seleccionarId = null)
    {
        var carga = ++_cargaActual;
        try
        {
            var lista = await App.Proveedores.BuscarAsync(TxtBuscar.Text, ChkBajas.IsChecked == true);
            if (carga != _cargaActual) return; // llegó una búsqueda más reciente
            Grid.ItemsSource = lista;
            TxtContador.Text = $"{lista.Count} proveedor(es)";
            if (seleccionarId is int id)
            {
                var p = lista.FirstOrDefault(x => x.Id == id);
                if (p is not null) { Grid.SelectedItem = p; Grid.ScrollIntoView(p); }
            }
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
    }

    private void ActualizarBotones()
    {
        var p = Seleccionado;
        BtnEditar.IsEnabled = p is not null;
        BtnHistIban.IsEnabled = p is not null;
        BtnBaja.IsEnabled = p is not null;
        BtnBaja.Content = p?.Baja == true ? "Reactivar" : "Dar de baja";
    }

    private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Filtro_Changed(object sender, RoutedEventArgs e) => _ = CargarAsync();
    private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = CargarAsync(Seleccionado?.Id);
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) => ActualizarBotones();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Seleccionado is not null) Editar(Seleccionado);
    }

    private void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ProveedorDialog(new Proveedor(), esNuevo: true) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) _ = CargarAsync(dlg.Resultado!.Id);
    }

    private void BtnEditar_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not null) Editar(Seleccionado);
    }

    private void Editar(Proveedor p)
    {
        var dlg = new ProveedorDialog(p.Clone(), esNuevo: false) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) _ = CargarAsync(p.Id);
    }

    private async void BtnBaja_Click(object sender, RoutedEventArgs e)
    {
        var p = Seleccionado;
        if (p is null) return;
        var owner = Window.GetWindow(this);
        var accion = p.Baja ? "reactivar" : "dar de baja";
        if (MessageBox.Show(owner, $"¿Seguro que quieres {accion} a {p.RazonSocial}?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await App.Proveedores.CambiarBajaAsync(p.Id, !p.Baja);
            await CargarAsync(p.Id);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, owner);
        }
    }

    private void BtnHistIban_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not { } p) return;
        new HistorialIbanWindow(p) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
