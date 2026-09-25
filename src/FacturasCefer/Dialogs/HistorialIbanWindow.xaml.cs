using System.Windows;
using FacturasCefer.Models;

namespace FacturasCefer.Dialogs;

public partial class HistorialIbanWindow : Window
{
    public HistorialIbanWindow(Proveedor p)
    {
        InitializeComponent();
        TxtTitulo.Text = $"{p.RazonSocial} ({p.CIF})";
        Loaded += async (_, _) =>
        {
            try
            {
                var lista = await App.Proveedores.HistorialIbanAsync(p.Id);
                Grid.ItemsSource = lista;
                if (lista.Count == 0) TxtVacio.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                App.MostrarError(ex, this);
            }
        };
    }
}
