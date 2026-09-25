using System.Windows;
using FacturasCefer.Services;

namespace FacturasCefer.Dialogs;

/// <summary>Pide los datos de un cambio de estado: fecha de pago, motivo de rechazo o comentario.</summary>
public partial class CambioEstadoDialog : Window
{
    private readonly bool _textoObligatorio;

    public DateTime? Fecha => DpFecha.SelectedDate;
    public string Texto => TxtTexto.Text.Trim();

    public CambioEstadoDialog(AccionEstado accion, int numFacturas)
    {
        InitializeComponent();
        var cuantas = numFacturas == 1 ? "la factura seleccionada" : $"las {numFacturas} facturas seleccionadas";
        switch (accion)
        {
            case AccionEstado.Validar:
                Title = "Validar";
                TxtMensaje.Text = $"Se marcarán como Validadas (revisadas y conformes para pago) {cuantas}.";
                break;
            case AccionEstado.Pagar:
                Title = "Marcar pagada";
                TxtMensaje.Text = $"Se marcarán como Pagadas {cuantas}.";
                PanelFecha.Visibility = Visibility.Visible;
                DpFecha.SelectedDate = DateTime.Today;
                break;
            case AccionEstado.Rechazar:
                Title = "Rechazar / anular";
                TxtMensaje.Text = $"Se marcarán como Rechazadas/Anuladas {cuantas}.";
                LblTexto.Text = "Motivo *";
                _textoObligatorio = true;
                break;
            case AccionEstado.Deshacer:
                Title = "Deshacer último cambio";
                TxtMensaje.Text = $"{char.ToUpper(cuantas[0])}{cuantas[1..]} volverán al estado que tenían antes de su último cambio.";
                break;
        }
        Loaded += (_, _) => { if (_textoObligatorio) TxtTexto.Focus(); };
    }

    private void BtnAceptar_Click(object sender, RoutedEventArgs e)
    {
        if (PanelFecha.Visibility == Visibility.Visible)
        {
            if (Fecha is null) { TxtError.Text = "Indica la fecha de pago."; return; }
            if (Fecha > DateTime.Today &&
                MessageBox.Show(this, "La fecha de pago es futura. ¿Continuar?", "Fecha de pago",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }
        if (_textoObligatorio && Texto.Length == 0) { TxtError.Text = "El motivo es obligatorio."; TxtTexto.Focus(); return; }
        DialogResult = true;
    }
}
