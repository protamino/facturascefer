using System.Windows;
using System.Windows.Input;
using FacturasCefer.Models;

namespace FacturasCefer;

public partial class LoginWindow : Window
{
    public Usuario? UsuarioAutenticado { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtUsuario.Focus();
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = IntentarLoginAsync();
    }

    private void BtnEntrar_Click(object sender, RoutedEventArgs e) => _ = IntentarLoginAsync();

    private async Task IntentarLoginAsync()
    {
        TxtError.Text = "";
        BtnEntrar.IsEnabled = false;
        try
        {
            var r = await App.Auth.LoginAsync(TxtUsuario.Text, TxtPassword.Password);
            if (!r.Ok)
            {
                TxtError.Text = r.UsuarioEncontrado
                    ? "Contraseña incorrecta."
                    : "Usuario no encontrado (o dado de baja).";
                return;
            }
            UsuarioAutenticado = r.Usuario;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            App.Log("login-error.log", ex);
            TxtError.Text = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            BtnEntrar.IsEnabled = true;
        }
    }
}
