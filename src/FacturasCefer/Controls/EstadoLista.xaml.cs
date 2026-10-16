using System.Windows;
using System.Windows.Controls;

namespace FacturasCefer.Controls;

/// <summary>Capa sobre una tabla: indicador de carga y mensaje cuando no hay resultados.</summary>
public partial class EstadoLista : UserControl
{
    public EstadoLista()
    {
        InitializeComponent();
    }

    /// <summary>Texto que se muestra cuando la lista está vacía.</summary>
    public string MensajeVacio
    {
        get => TxtVacio.Text;
        set => TxtVacio.Text = value;
    }

    public bool Cargando
    {
        set
        {
            PanelCargando.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value) PanelVacio.Visibility = Visibility.Collapsed;
        }
    }

    public bool Vacio
    {
        set => PanelVacio.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }
}
