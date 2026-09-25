using System.Windows;
using System.Windows.Controls;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Dialogs;

/// <summary>Ficha de una factura: datos (editables en Recibida/Validada), historial de estados y acceso a los PDFs.</summary>
public partial class FacturaDialog : Window
{
    private readonly int _id;
    private FacturaListado? _f;

    /// <summary>True si se han guardado cambios (para refrescar el listado).</summary>
    public bool Modificada { get; private set; }

    public FacturaDialog(int idFactura)
    {
        InitializeComponent();
        _id = idFactura;
        Loaded += async (_, _) => await CargarAsync();
    }

    private async Task CargarAsync()
    {
        try
        {
            _f = await App.Facturas.ObtenerAsync(_id);
            if (_f is null)
            {
                MessageBox.Show(this, "La factura ya no existe.", "Factura", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }
            GridHistorial.ItemsSource = await App.Facturas.HistorialAsync(_id);
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, this);
            Close();
            return;
        }

        var f = _f;
        Title = $"Factura {f.NumeroFactura}";
        TxtProveedor.Text = $"{f.Proveedor} ({f.CIF})";

        var estado = $"Estado: {f.EstadoTexto}";
        if (f.Estado == EstadoFactura.Pagada && f.FechaPago is { } fp) estado += $" el {fp:dd/MM/yyyy}";
        if (f.Estado == EstadoFactura.Rechazada && !string.IsNullOrWhiteSpace(f.MotivoRechazo)) estado += $" — Motivo: {f.MotivoRechazo}";
        if (f.Vencida) estado += "   ⚠ VENCIDA";
        estado += $"   ·   Registrada el {f.FechaRegistro:dd/MM/yyyy HH:mm}";
        TxtEstado.Text = estado;

        if (f.IbanDistinto)
        {
            TxtIbanAviso.Text =
                "⚠ El IBAN de esta factura no coincide con el que tiene ahora el proveedor.\n" +
                $"Factura:     {Validaciones.FormatearIban(f.IBAN)}\n" +
                $"Proveedor: {(string.IsNullOrWhiteSpace(f.IbanProveedor) ? "(sin IBAN)" : Validaciones.FormatearIban(f.IbanProveedor))}\n" +
                "Confirma la cuenta con el proveedor antes de pagar.";
            PanelIban.Visibility = Visibility.Visible;
        }

        TxtNumero.Text = f.NumeroFactura;
        DpFecha.SelectedDate = f.FechaFactura;
        DpVencimiento.SelectedDate = f.FechaVencimiento;
        TxtIban.Text = Validaciones.FormatearIban(f.IBAN);
        CmbFormaPago.SelectedIndex = (int)f.FormaPago - 1;
        TxtTarjeta.Text = f.Tarjeta ?? "";
        TxtConcepto.Text = f.Concepto ?? "";
        TxtBase.Text = Formato.Importe(f.BaseImponible);
        TxtPorcIva.Text = Formato.Porcentaje(f.PorcIVA);
        TxtCuotaIva.Text = Formato.Importe(f.CuotaIVA);
        TxtPorcIrpf.Text = Formato.Porcentaje(f.PorcIRPF);
        TxtCuotaIrpf.Text = Formato.Importe(f.CuotaIRPF);
        TxtTotal.Text = Formato.Importe(f.Total);
        TxtObservaciones.Text = f.Observaciones ?? "";

        var editable = f.Estado is EstadoFactura.Recibida or EstadoFactura.Validada;
        Formulario.IsEnabled = editable;
        BtnGuardar.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
        BtnOriginal.IsEnabled = !string.IsNullOrWhiteSpace(f.RutaPdfOriginal);
        if (!editable) TxtError.Text = "Las facturas Pagadas o Rechazadas no se pueden editar (deshaz el último cambio si es necesario).";
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (_f is null) return;
        TxtError.Text = "";

        var numero = TxtNumero.Text.Trim();
        if (numero.Length == 0) { Error("El nº de factura es obligatorio.", TxtNumero); return; }
        if (DpFecha.SelectedDate is not DateTime fecha) { Error("La fecha es obligatoria.", DpFecha); return; }
        if (!Leer(TxtBase, out var b) || !Leer(TxtPorcIva, out var piva) || !Leer(TxtCuotaIva, out var civa) ||
            !Leer(TxtPorcIrpf, out var pirpf) || !Leer(TxtCuotaIrpf, out var cirpf)) return;
        if (!Formato.TryImporte(TxtTotal.Text, out var total) || total is null) { Error("El total es obligatorio y debe ser un importe válido.", TxtTotal); return; }

        var iban = Validaciones.NormalizarIban(TxtIban.Text);
        if (iban.Length > 0 && !Validaciones.IbanValido(iban) && !Confirmar("El IBAN no es válido. ¿Guardar igualmente?")) return;
        if (b is not null && Math.Abs(b.Value + (civa ?? 0) - (cirpf ?? 0) - total.Value) > 0.02m &&
            !Confirmar($"Base + IVA − IRPF = {Formato.Importe(b.Value + (civa ?? 0) - (cirpf ?? 0))} €, pero el total es {Formato.Importe(total)} €.\n\n¿Guardar igualmente?"))
            return;

        var f = _f;
        f.NumeroFactura = numero;
        f.FechaFactura = fecha;
        f.FechaVencimiento = DpVencimiento.SelectedDate;
        f.IBAN = iban.Length > 0 ? iban : null;
        f.FormaPago = (FormaPago)(Math.Max(CmbFormaPago.SelectedIndex, 0) + 1);
        f.Tarjeta = f.FormaPago == FormaPago.Tarjeta ? Validaciones.EnmascararTarjeta(TxtTarjeta.Text) : null;
        f.Concepto = TxtConcepto.Text.Trim();
        f.BaseImponible = b;
        f.PorcIVA = piva;
        f.CuotaIVA = civa;
        f.PorcIRPF = pirpf;
        f.CuotaIRPF = cirpf;
        f.Total = total.Value;
        f.Observaciones = TxtObservaciones.Text;

        BtnGuardar.IsEnabled = false;
        try
        {
            await App.Facturas.ActualizarAsync(f);
            Modificada = true;
            DialogResult = true;
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message, TxtNumero);
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

    private void CmbFormaPago_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LblIban is null || PanelTarjetaFac is null) return; // durante InitializeComponent
        LblIban.Text = CmbFormaPago.SelectedIndex == 1
            ? "Cuenta de cargo (cuenta de CEFER donde se cobra el recibo)"
            : "IBAN del proveedor";
        var tarjeta = CmbFormaPago.SelectedIndex == 2;
        PanelIbanFac.Visibility = tarjeta ? Visibility.Collapsed : Visibility.Visible;
        PanelTarjetaFac.Visibility = tarjeta ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool Leer(TextBox tb, out decimal? valor)
    {
        if (Formato.TryImporte(tb.Text, out valor)) return true;
        Error("Importe no válido.", tb);
        return false;
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e) => Abrir(_f?.RutaPdf);
    private void BtnOriginal_Click(object sender, RoutedEventArgs e) => Abrir(_f?.RutaPdfOriginal);

    private void Abrir(string? ruta)
    {
        try { Formato.Abrir(ruta); }
        catch (Exception ex) { App.MostrarError(ex, this); }
    }

    private void Error(string msg, Control foco)
    {
        TxtError.Text = msg;
        foco.Focus();
    }

    private bool Confirmar(string msg) =>
        MessageBox.Show(this, msg, "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
