using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Views;

/// <summary>Panel de inicio: cifras clave, próximos pagos y gasto por mes. Cada tarjeta abre el listado filtrado.</summary>
public partial class InicioView : UserControl
{
    /// <summary>Barra de gasto de un mes (porcentaje respecto al mes con más gasto).</summary>
    private sealed record BarraMes(string Texto, double Porcentaje, string ImporteTexto);

    private bool _cargando;

    /// <summary>Se pulsa una tarjeta: el destino es su Tag («Pendientes», «Vencidas», «Importaciones»…).</summary>
    public event EventHandler<string>? Ir;

    public InicioView()
    {
        InitializeComponent();
        // Se refresca cada vez que se entra en Inicio.
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) _ = CargarAsync(); };
    }

    public async Task CargarAsync()
    {
        if (_cargando) return;
        _cargando = true;
        Estado.Cargando = true;
        try
        {
            var r = await App.Resumen.ObtenerAsync();

            TxtPendientes.Text = Formato.Importe(r.Pendientes.Importe) + " €";
            TxtPendientesDet.Text = $"{r.Pendientes.Numero} factura(s)";

            TxtVencidas.Text = r.Vencidas.Numero.ToString();
            TxtVencidas.Foreground = r.Vencidas.Numero > 0 ? Tema.ErrorTexto : Tema.Texto;
            TxtVencidasDet.Text = r.Vencidas.Numero > 0 ? Formato.Importe(r.Vencidas.Importe) + " €" : "ninguna";

            TxtPronto.Text = r.VencenPronto.Numero.ToString();
            TxtPronto.Foreground = r.VencenPronto.Numero > 0 ? Tema.AvisoTexto : Tema.Texto;
            TxtProntoDet.Text = r.VencenPronto.Numero > 0 ? Formato.Importe(r.VencenPronto.Importe) + " €" : "ninguna";

            TxtMesTitulo.Text = "GASTO DE " + DateTime.Today.ToString("MMMM").ToUpperInvariant();
            TxtMes.Text = Formato.Importe(r.MesActual.Importe) + " €";
            TxtMesDet.Text = $"{r.MesActual.Numero} factura(s) · mes anterior {Formato.Importe(r.MesAnterior.Importe)} €";

            TxtRevisar.Text = r.PorRevisar.ToString();
            TxtRevisar.Foreground = r.PorRevisar > 0 ? Tema.Pincel("Marca") : Tema.Texto;
            TxtSinCuenta.Text = r.SinCuenta.ToString();
            TxtProvRevisar.Text = r.ProveedoresPorRevisar.ToString();
            TxtProvRevisar.Foreground = r.ProveedoresPorRevisar > 0 ? Tema.Pincel("Marca") : Tema.Texto;

            MostrarImportador(r);

            GridProximos.ItemsSource = r.ProximosVencimientos;
            EstadoProximos.Vacio = r.ProximosVencimientos.Count == 0;

            var max = r.GastoPorMes.Count == 0 ? 0 : r.GastoPorMes.Max(g => g.Importe);
            ListaMeses.ItemsSource = r.GastoPorMes
                .Select(g => new BarraMes(g.Texto, max <= 0 ? 0 : (double)(g.Importe / max * 100), Formato.Importe(g.Importe) + " €"))
                .ToList();
            TxtActualizado.Text = $"Actualizado a las {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
        finally
        {
            Estado.Cargando = false;
            _cargando = false;
        }
    }

    private void MostrarImportador(ResumenInicio r)
    {
        var e = r.Importador;
        TxtImportador.Text = r.ImportacionesConIncidencia24h > 0 ? $"{r.ImportacionesConIncidencia24h} con incidencias" : "Sin incidencias";
        TxtImportador.FontSize = 20;
        TxtImportador.Margin = new Thickness(0, 10, 0, 4);
        TxtImportador.Foreground = r.ImportacionesConIncidencia24h > 0 ? Tema.ErrorTexto : Tema.OkTexto;
        TxtImportadorDet.Text = $"{r.ImportadasHoy} registrada(s) hoy · " +
                                (e is null ? "sin datos del servicio" : "última revisión " + e.Hace);

        if (e is null || e.Parado || !e.Correcta)
        {
            TxtImportadorAviso.Text = e is null
                ? "⚠ No hay datos del importador automático: el servicio no ha registrado ninguna revisión de la carpeta de Drive. " +
                  "Las facturas que lleguen por correo no se están registrando."
                : e.Parado
                    ? $"⚠ El importador automático no revisa la carpeta de Drive desde el {e.UltimaPasada:dd/MM/yyyy HH:mm} ({e.Hace}). " +
                      $"Comprueba el servicio «FacturasCefer.Importador» en {e.Equipo ?? "el servidor"}."
                    : $"⚠ La última revisión del importador ({e.Hace}) terminó con error: {e.Mensaje}";
            PanelImportador.Visibility = Visibility.Visible;
        }
        else
        {
            PanelImportador.Visibility = Visibility.Collapsed;
        }
    }

    private void Tarjeta_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string destino }) Ir?.Invoke(this, destino);
    }

    private void GridProximos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridProximos.SelectedItem is not FacturaListado f) return;
        var dlg = new FacturaDialog(f.Id) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Modificada) _ = CargarAsync();
    }
}
