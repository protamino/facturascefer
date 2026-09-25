using System.IO;
using System.Windows;
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
    public static Usuario Usuario { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

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

    /// <summary>Escribe la excepción en %TEMP%\FacturasCefer\{nombre}.</summary>
    public static void Log(string nombre, Exception ex)
    {
        try
        {
            var log = Path.Combine(Path.GetTempPath(), "FacturasCefer", nombre);
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.WriteAllText(log, ex.ToString());
        }
        catch { /* ignore */ }
    }

    /// <summary>Muestra un error: los de negocio tal cual, el resto con su tipo y registrados en el log.</summary>
    public static void MostrarError(Exception ex, Window? owner = null)
    {
        if (ex is ReglaNegocioException)
        {
            MessageBox.Show(owner ?? Current.MainWindow!, ex.Message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Log("app-error.log", ex);
        MessageBox.Show(owner ?? Current.MainWindow!, ex.GetType().Name + ": " + ex.Message, "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("app-error.log", e.Exception);
        MessageBox.Show(e.Exception.Message, "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
