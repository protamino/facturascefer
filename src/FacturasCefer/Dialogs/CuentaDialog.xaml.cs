using System.Windows;
using FacturasCefer.Controls;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Dialogs;

/// <summary>Alta / edición de una cuenta contable. El código solo se puede escribir en el alta.</summary>
public partial class CuentaDialog : Window
{
    private readonly CuentaContable _c;
    private readonly bool _esNueva;

    public CuentaContable? Resultado { get; private set; }

    public CuentaDialog(CuentaContable c, bool esNueva)
    {
        InitializeComponent();
        _c = c;
        _esNueva = esNueva;
        Title = esNueva ? "Nueva cuenta contable" : "Editar cuenta contable";
        TxtCodigo.Text = c.Codigo;
        TxtCodigo.IsEnabled = esNueva;
        TxtDescripcion.Text = c.Descripcion;
        Loaded += (_, _) => (esNueva && c.Codigo.Length == 0 ? TxtCodigo : TxtDescripcion).Focus();
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var codigo = TxtCodigo.Text.Trim();
        if (!Validaciones.CuentaValida(codigo)) { Error("El código debe tener exactamente 8 dígitos.", TxtCodigo); return; }
        if (string.IsNullOrWhiteSpace(TxtDescripcion.Text)) { Error("La descripción es obligatoria.", TxtDescripcion); return; }

        _c.Codigo = codigo;
        _c.Descripcion = TxtDescripcion.Text.Trim();
        BtnGuardar.IsEnabled = false;
        try
        {
            if (_esNueva) await App.Cuentas.CrearAsync(_c, App.Usuario.IdUsuario);
            else await App.Cuentas.ActualizarAsync(_c);
            CuentaSelector.InvalidarCache();
            Resultado = _c;
            DialogResult = true;
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message, TxtCodigo);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, this);
        }
        finally
        {
            BtnGuardar.IsEnabled = true;
        }
    }

    private void Error(string msg, System.Windows.Controls.Control foco)
    {
        TxtError.Text = msg;
        foco.Focus();
    }
}
