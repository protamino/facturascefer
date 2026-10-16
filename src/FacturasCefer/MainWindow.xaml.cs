using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FacturasCefer;

public partial class MainWindow : Window
{
    private readonly (RadioButton Nav, FrameworkElement Vista, string Titulo, string Subtitulo)[] _secciones;

    public MainWindow()
    {
        InitializeComponent();
        Title = "FacturasCefer " + App.Version;
        TxtUsuario.Text = App.Usuario.NombreUser;
        TxtVersion.Text = "Versión " + App.Version;

        _secciones = new (RadioButton, FrameworkElement, string, string)[]
        {
            (NavSubir, Subir, "Subir facturas", "Arrastra, pega (Ctrl+V) o añade PDFs: la IA extrae los datos y tú los revisas antes de guardar."),
            (NavFacturas, Facturas, "Facturas", "Listado de facturas recibidas, estados, pagos y exportación a a3ASESOR | con."),
            (NavProveedores, Proveedores, "Proveedores", "Fichas de proveedores, IBAN, forma de pago y cuentas por defecto."),
            (NavCuentas, Cuentas, "Cuentas contables", "Catálogo de cuentas de gasto para asignar a proveedores y facturas."),
        };
        Mostrar(NavSubir);
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_secciones is not null && sender is RadioButton rb) Mostrar(rb);
    }

    private void Mostrar(RadioButton nav)
    {
        foreach (var s in _secciones)
        {
            var activa = s.Nav == nav;
            s.Vista.Visibility = activa ? Visibility.Visible : Visibility.Collapsed;
            if (!activa) continue;
            TxtTitulo.Text = s.Titulo;
            TxtSubtitulo.Text = s.Subtitulo;
        }
    }

    private async void Proveedores_VerFacturas(object? sender, Models.Proveedor p)
    {
        await Facturas.FiltrarPorProveedorAsync(p.Id);
        NavFacturas.IsChecked = true;
    }

    /// <summary>Aviso que aparece abajo a la derecha y desaparece solo (clic para cerrarlo antes).</summary>
    public void Notificar(string texto, TipoAviso tipo)
    {
        var aviso = new Border
        {
            Style = Tema.EstiloAviso(tipo),
            Margin = new Thickness(0, 8, 0, 0),
            MaxWidth = 440,
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.15, Color = Colors.Black },
            Child = new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap },
        };
        PanelAvisos.Children.Add(aviso);

        void Quitar()
        {
            var fundido = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
            fundido.Completed += (_, _) => PanelAvisos.Children.Remove(aviso);
            aviso.BeginAnimation(OpacityProperty, fundido);
        }

        var temporizador = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(tipo is TipoAviso.Error or TipoAviso.Atencion ? 9 : 5),
        };
        temporizador.Tick += (_, _) => { temporizador.Stop(); Quitar(); };
        aviso.MouseLeftButtonUp += (_, _) => { temporizador.Stop(); Quitar(); };
        temporizador.Start();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Subir.HayPendientes &&
            MessageBox.Show(this, "Hay facturas subidas sin guardar. Si sales se perderán.\n\n¿Salir igualmente?",
                "Salir", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            e.Cancel = true;
    }
}
