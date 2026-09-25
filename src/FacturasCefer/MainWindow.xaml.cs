using System.ComponentModel;
using System.Windows;

namespace FacturasCefer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TxtUsuario.Text = "Usuario: " + App.Usuario.NombreUser;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Subir.HayPendientes &&
            MessageBox.Show(this, "Hay facturas subidas sin guardar. Si sales se perderán.\n\n¿Salir igualmente?",
                "Salir", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            e.Cancel = true;
    }
}
