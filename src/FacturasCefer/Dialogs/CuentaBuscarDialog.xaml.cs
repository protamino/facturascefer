using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Dialogs;

/// <summary>Busca una cuenta contable activa por código o descripción, o da de alta una nueva.</summary>
public partial class CuentaBuscarDialog : Window
{
    private List<CuentaContable> _todas = new();

    public string? Codigo { get; private set; }

    public CuentaBuscarDialog(string? textoInicial)
    {
        InitializeComponent();
        TxtBuscar.Text = textoInicial ?? "";
        Loaded += async (_, _) =>
        {
            await CargarAsync();
            TxtBuscar.Focus();
            TxtBuscar.SelectAll();
        };
    }

    private async Task CargarAsync()
    {
        try
        {
            _todas = await App.Cuentas.BuscarAsync(null, incluirBajas: false);
            Filtrar();
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, this);
        }
    }

    private void Filtrar()
    {
        var t = TxtBuscar.Text.Trim();
        var lista = t.Length == 0
            ? _todas
            : _todas.Where(c => c.Codigo.StartsWith(t) || c.Descripcion.Contains(t, StringComparison.CurrentCultureIgnoreCase)).ToList();
        Grid.ItemsSource = lista;
        if (lista.Count > 0) Grid.SelectedIndex = 0;
    }

    private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => Filtrar();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: CuentaContable }) Elegir();
    }

    private void BtnElegir_Click(object sender, RoutedEventArgs e) => Elegir();

    private void Elegir()
    {
        if (Grid.SelectedItem is not CuentaContable c) return;
        Codigo = c.Codigo;
        DialogResult = true;
    }

    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        // Si lo buscado son 8 dígitos se propone como código; si no, como descripción.
        var t = TxtBuscar.Text.Trim();
        var nueva = Validaciones.CuentaValida(t)
            ? new CuentaContable { Codigo = t }
            : new CuentaContable { Descripcion = t };
        var dlg = new CuentaDialog(nueva, esNueva: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        Codigo = dlg.Resultado!.Codigo;
        DialogResult = true;
    }
}
