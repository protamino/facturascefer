using System.Windows;

namespace FacturasCefer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TxtUsuario.Text = "Usuario: " + App.Usuario.NombreUser;
    }
}
