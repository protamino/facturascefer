using System.Windows;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Dialogs;

/// <summary>
/// Alta / edición de proveedor. Admite datos prellenados (lo usará la subida de facturas
/// cuando la IA detecte un proveedor nuevo) y un aviso opcional en la cabecera.
/// Guarda en BD al pulsar Guardar; el proveedor guardado queda en <see cref="Resultado"/>.
/// </summary>
public partial class ProveedorDialog : Window
{
    private readonly Proveedor _p;
    private readonly bool _esNuevo;
    private readonly string _ibanOriginal;
    private readonly int? _idFacturaOrigen;

    public Proveedor? Resultado { get; private set; }

    public ProveedorDialog(Proveedor p, bool esNuevo, string? aviso = null, int? idFacturaOrigen = null)
    {
        InitializeComponent();
        _p = p;
        _esNuevo = esNuevo;
        _ibanOriginal = Validaciones.NormalizarIban(p.IBAN);
        _idFacturaOrigen = idFacturaOrigen;

        Title = esNuevo ? "Nuevo proveedor" : "Editar proveedor";
        if (!string.IsNullOrWhiteSpace(aviso))
        {
            TxtAviso.Text = aviso;
            TxtAviso.Visibility = Visibility.Visible;
        }

        TxtRazonSocial.Text = p.RazonSocial;
        TxtCif.Text = p.CIF;
        TxtIban.Text = Validaciones.FormatearIban(p.IBAN);
        CmbFormaPago.SelectedIndex = p.FormaPago == FormaPago.Domiciliacion ? 1 : 0;
        TxtDireccion.Text = p.Direccion;
        TxtCp.Text = p.CP;
        TxtPoblacion.Text = p.Poblacion;
        TxtProvincia.Text = p.Provincia;
        TxtPais.Text = string.IsNullOrWhiteSpace(p.Pais) ? "España" : p.Pais;
        TxtEmail.Text = p.Email;
        TxtTelefono.Text = p.Telefono;
        TxtObservaciones.Text = p.Observaciones;

        Loaded += (_, _) => TxtRazonSocial.Focus();
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        var cif = Validaciones.NormalizarCif(TxtCif.Text);
        var iban = Validaciones.NormalizarIban(TxtIban.Text);

        if (string.IsNullOrWhiteSpace(TxtRazonSocial.Text)) { Error("La razón social es obligatoria.", TxtRazonSocial); return; }
        if (cif.Length == 0) { Error("El CIF / NIF es obligatorio.", TxtCif); return; }
        if (iban.Length > 0 && !Validaciones.IbanValido(iban)) { Error("El IBAN no es válido (revisa los dígitos).", TxtIban); return; }

        // Proveedores extranjeros pueden no tener CIF español: aviso, no bloqueo.
        if (!Validaciones.CifValido(cif) &&
            MessageBox.Show(this, $"El CIF/NIF «{cif}» no es un CIF, NIF o NIE español válido.\n\n¿Guardar igualmente?",
                "CIF no válido", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        // Antifraude: cambiar la cuenta de un proveedor existente se confirma explícitamente.
        if (!_esNuevo && _ibanOriginal.Length > 0 && iban != _ibanOriginal &&
            MessageBox.Show(this,
                $"Vas a CAMBIAR el IBAN de este proveedor:\n\n" +
                $"  Actual: {Validaciones.FormatearIban(_ibanOriginal)}\n" +
                $"  Nuevo:  {(iban.Length > 0 ? Validaciones.FormatearIban(iban) : "(vacío)")}\n\n" +
                "Los cambios de cuenta bancaria son una vía habitual de fraude. " +
                "Confírmalo con el proveedor por un canal conocido antes de aceptar.\n\n¿Confirmar el cambio?",
                "Cambio de IBAN", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        _p.RazonSocial = TxtRazonSocial.Text.Trim();
        _p.CIF = cif;
        _p.IBAN = iban.Length > 0 ? iban : null;
        _p.FormaPago = FormaPagoSeleccionada;
        _p.Direccion = TxtDireccion.Text;
        _p.CP = TxtCp.Text;
        _p.Poblacion = TxtPoblacion.Text;
        _p.Provincia = TxtProvincia.Text;
        _p.Pais = TxtPais.Text;
        _p.Email = TxtEmail.Text;
        _p.Telefono = TxtTelefono.Text;
        _p.Observaciones = TxtObservaciones.Text;

        BtnGuardar.IsEnabled = false;
        try
        {
            if (_esNuevo)
                _p.Id = await App.Proveedores.CrearAsync(_p, App.Usuario.IdUsuario);
            else
                await App.Proveedores.ActualizarAsync(_p, App.Usuario.IdUsuario, _idFacturaOrigen);

            Resultado = _p;
            DialogResult = true;
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message, TxtCif);
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

    private FormaPago FormaPagoSeleccionada =>
        CmbFormaPago.SelectedIndex == 1 ? FormaPago.Domiciliacion : FormaPago.Transferencia;

    private void CmbFormaPago_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LblIban is null) return; // durante InitializeComponent
        LblIban.Text = FormaPagoSeleccionada == FormaPago.Domiciliacion
            ? "IBAN del proveedor (opcional si domicilia)"
            : "IBAN del proveedor";
    }

    private void Error(string msg, System.Windows.Controls.Control foco)
    {
        TxtError.Text = msg;
        foco.Focus();
    }
}
