using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;
using FacturasCefer.Config;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer;

public partial class App : Application
{
    public static AppConfig Config { get; private set; } = null!;
    public static AuthService Auth { get; private set; } = null!;
    public static ProveedorService Proveedores { get; private set; } = null!;
    public static FacturaService Facturas { get; private set; } = null!;
    public static ExtraccionService Extraccion { get; private set; } = null!;
    public static CuentaContableService Cuentas { get; private set; } = null!;
    public static Exportacion.A3.A3ExportService ExportacionA3 { get; private set; } = null!;
    public static Usuario Usuario { get; private set; } = null!;

    /// <summary>Versión de la app (la del .csproj), p. ej. "1.4.0".</summary>
    public static string Version { get; } =
        typeof(App).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        // Fechas e importes en formato español (DatePicker, bindings con StringFormat…).
        var es = new CultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = es;
        CultureInfo.DefaultThreadCurrentUICulture = es;
        Thread.CurrentThread.CurrentCulture = es;
        Thread.CurrentThread.CurrentUICulture = es;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(es.IetfLanguageTag)));

        // Letra y color de texto del tema (Segoe UI 13, como BANCOSEMEN) en todas las ventanas.
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Window),
            new FrameworkPropertyMetadata(13.0, FrameworkPropertyMetadataOptions.Inherits));
        TextElement.FontFamilyProperty.OverrideMetadata(typeof(Window),
            new FrameworkPropertyMetadata(new System.Windows.Media.FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.Inherits));

        // Evitar que la app se cierre al cerrarse el diálogo de login antes de abrir la principal.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            Config = AppConfig.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error de configuración", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        Auth = new AuthService(Config);
        Proveedores = new ProveedorService(Config);
        Facturas = new FacturaService(Config);
        Extraccion = new ExtraccionService(Config);
        Cuentas = new CuentaContableService(Config);
        ExportacionA3 = new Exportacion.A3.A3ExportService(new Exportacion.A3.A3ExportRepository(Config));

        var login = new LoginWindow();
        if (login.ShowDialog() == true && login.UsuarioAutenticado is not null)
        {
            Usuario = login.UsuarioAutenticado;
            var main = new MainWindow();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }
        else
        {
            Shutdown(0);
        }
    }

    /// <summary>
    /// Añade la excepción (con fecha y usuario) a %TEMP%\FacturasCefer\{nombre}. Se acumula; al pasar de 2 MB
    /// el fichero se renombra a .old y empieza de nuevo.
    /// </summary>
    public static void Log(string nombre, Exception ex)
    {
        try
        {
            var log = Path.Combine(Path.GetTempPath(), "FacturasCefer", nombre);
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            if (File.Exists(log) && new FileInfo(log).Length > 2 * 1024 * 1024)
                File.Move(log, log + ".old", overwrite: true);
            File.AppendAllText(log,
                $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} · v{Version} · {Usuario?.NombreUser ?? "-"}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Aviso no bloqueante (banner en la esquina de la ventana principal). Si la ventana principal no está
    /// visible o el aviso viene de un diálogo, se muestra como mensaje normal.
    /// </summary>
    public static void Notificar(string texto, TipoAviso tipo = TipoAviso.Ok, Window? owner = null)
    {
        if (Current.MainWindow is MainWindow mw && mw.IsVisible && (owner is null || owner == mw))
        {
            mw.Notificar(texto, tipo);
            return;
        }
        MessageBox.Show(owner ?? Current.MainWindow!, texto, "FacturasCefer", MessageBoxButton.OK, tipo switch
        {
            TipoAviso.Error => MessageBoxImage.Error,
            TipoAviso.Atencion => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information,
        });
    }

    /// <summary>Muestra un error: los de negocio como aviso, el resto con una explicación y registrados en el log.</summary>
    public static void MostrarError(Exception ex, Window? owner = null)
    {
        if (ex is ReglaNegocioException)
        {
            Notificar(ex.Message, TipoAviso.Atencion, owner);
            return;
        }
        Log("app-error.log", ex);
        var texto = ex switch
        {
            Microsoft.Data.SqlClient.SqlException => "No se ha podido completar la operación en la base de datos.\n\n" + ex.Message,
            IOException or UnauthorizedAccessException => "No se ha podido acceder al fichero o a la carpeta de red.\n\n" + ex.Message,
            System.Net.Http.HttpRequestException or TaskCanceledException => "No se ha podido conectar con el servicio externo (¿hay conexión a internet?).\n\n" + ex.Message,
            _ => ex.Message,
        };
        MessageBox.Show(owner ?? Current.MainWindow!, texto + "\n\n(Detalle guardado en el registro de errores.)", "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("app-error.log", e.Exception);
        MessageBox.Show(e.Exception.Message, "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
