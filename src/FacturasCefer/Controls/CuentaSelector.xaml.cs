using System.Windows;
using System.Windows.Controls;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;

namespace FacturasCefer.Controls;

/// <summary>
/// Selector de cuenta contable: desplegable con autocompletado por código y botón «…» para
/// buscar por descripción o dar de alta una cuenta. El catálogo se carga una vez y se comparte.
/// </summary>
public partial class CuentaSelector : UserControl
{
    private static Task<List<CuentaContable>>? _carga;

    private List<CuentaContable>? _cuentas;
    private string? _pendiente;
    private bool _asignando;

    /// <summary>Se llama tras crear o modificar cuentas para que los selectores recarguen el catálogo.</summary>
    public static void InvalidarCache() => _carga = null;

    public event EventHandler? CodigoCambiado;

    public CuentaSelector()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RecargarAsync();
        // Al darle el foco al control (p. ej. tras un error de validación), pasarlo al desplegable.
        Focusable = true;
        GotKeyboardFocus += (_, e) => { if (ReferenceEquals(e.NewFocus, this)) Cmb.Focus(); };
    }

    /// <summary>Código de la cuenta elegida, o null si no hay ninguna (o lo escrito no es una cuenta).</summary>
    public string? Codigo
    {
        get
        {
            if (_cuentas is null) return _pendiente;
            if (Cmb.SelectedItem is CuentaContable c && Cmb.Text == c.Texto) return c.Codigo;
            var t = Cmb.Text.Trim();
            if (t.Length >= 8 && t[..8].All(char.IsDigit))
                return _cuentas.FirstOrDefault(x => x.Codigo == t[..8])?.Codigo;
            return null;
        }
        set
        {
            _pendiente = value;
            if (_cuentas is null) return;
            _asignando = true;
            var item = value is null ? null : _cuentas.FirstOrDefault(c => c.Codigo == value);
            Cmb.SelectedItem = item;
            Cmb.Text = item?.Texto ?? value ?? "";
            _asignando = false;
        }
    }

    /// <summary>False si hay algo escrito que no corresponde a ninguna cuenta del catálogo.</summary>
    public bool EsValido => string.IsNullOrWhiteSpace(Cmb.Text) || Codigo is not null;

    private static Task<List<CuentaContable>> Catalogo() => _carga ??= App.Cuentas.BuscarAsync(null, incluirBajas: true);

    private async Task RecargarAsync()
    {
        var actual = Codigo;
        try
        {
            var todas = await Catalogo();
            // Activas + la actual aunque esté de baja (para no perderla en facturas antiguas).
            _cuentas = todas.Where(c => !c.Baja || c.Codigo == actual).ToList();
            Cmb.ItemsSource = _cuentas;
            Codigo = actual;
        }
        catch (Exception ex)
        {
            _carga = null;
            App.Log("app-error.log", ex);
        }
    }

    private async void BtnBuscar_Click(object sender, RoutedEventArgs e)
    {
        var texto = Codigo is null ? Cmb.Text : "";
        var dlg = new CuentaBuscarDialog(texto) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        _pendiente = dlg.Codigo;
        _cuentas = null;
        await RecargarAsync();
        Codigo = dlg.Codigo;
        CodigoCambiado?.Invoke(this, EventArgs.Empty);
    }

    private void Cmb_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_asignando && _cuentas is not null) CodigoCambiado?.Invoke(this, EventArgs.Empty);
    }

    private void Cmb_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_cuentas is null) return;
        // Si se ha tecleado un código válido, mostrar su descripción completa.
        if (Codigo is { } c && Cmb.SelectedItem is not CuentaContable)
        {
            Codigo = c;
            CodigoCambiado?.Invoke(this, EventArgs.Empty);
        }
    }
}
