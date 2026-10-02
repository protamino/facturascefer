using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FacturasCefer.Models;
using FacturasCefer.Services;

namespace FacturasCefer.Controls;

/// <summary>
/// Tabla editable del desglose de impuestos de una factura (base, % IVA, IVA, % RE, RE y cuenta opcional).
/// Se usa en la revisión al subir y en la ficha de la factura.
/// </summary>
public partial class DesgloseImpuestosGrid : UserControl
{
    private ObservableCollection<FacturaImpuesto> _lineas = new();

    /// <summary>Cambia cualquier importe o línea (para recalcular cuadre y totales).</summary>
    public event EventHandler? Cambiado;

    public DesgloseImpuestosGrid()
    {
        InitializeComponent();
        Grid.ItemsSource = _lineas;
        ActualizarTotales();
    }

    /// <summary>Líneas actuales (copias). Al asignar se copian; siempre queda al menos una línea.</summary>
    public IReadOnlyList<FacturaImpuesto> Lineas
    {
        get
        {
            Grid.CommitEdit(DataGridEditingUnit.Row, true);
            return _lineas.Select(l => l.Clone()).ToList();
        }
        set
        {
            foreach (var l in _lineas) l.PropertyChanged -= Linea_PropertyChanged;
            _lineas = new ObservableCollection<FacturaImpuesto>(value.Select(l => l.Clone()));
            if (_lineas.Count == 0) _lineas.Add(new FacturaImpuesto());
            foreach (var l in _lineas) l.PropertyChanged += Linea_PropertyChanged;
            Grid.ItemsSource = _lineas;
            ActualizarTotales();
        }
    }

    public bool SoloLectura
    {
        get => Grid.IsReadOnly;
        set
        {
            Grid.IsReadOnly = value;
            PanelBotones.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public decimal TotalBase => _lineas.Sum(l => l.BaseImponible);
    public decimal TotalIva => _lineas.Sum(l => l.CuotaIVA);
    public decimal TotalRe => _lineas.Sum(l => l.CuotaRE ?? 0);

    /// <summary>Resalta la tabla (dato dudoso de la IA).</summary>
    public void MarcarDudoso(bool dudoso, string? tooltip = null)
    {
        if (dudoso)
        {
            Grid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xF3, 0xC4));
            Grid.ToolTip = tooltip;
        }
        else
        {
            Grid.ClearValue(BackgroundProperty);
            Grid.ClearValue(ToolTipProperty);
        }
    }

    /// <summary>Validación de las líneas: devuelve el primer error, o null si todo está bien.</summary>
    public string? Validar()
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        for (var i = 0; i < _lineas.Count; i++)
        {
            var l = _lineas[i];
            var n = _lineas.Count > 1 ? $"Línea {i + 1}: " : "";
            if (l.PorcIVA < 0 || l.PorcIVA > 100) return n + "% de IVA no válido.";
            if (l.PorcRE is < 0 or > 100) return n + "% de recargo no válido.";
            if (l.CuentaContable is { } c && !Validaciones.CuentaValida(c))
                return n + $"la cuenta «{c}» debe tener 8 dígitos (o déjala vacía para usar la de la factura).";
        }
        return null;
    }

    private void Linea_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ActualizarTotales();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    private void Grid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(() => { ActualizarTotales(); Cambiado?.Invoke(this, EventArgs.Empty); });

    private void ActualizarTotales()
    {
        var re = TotalRe;
        TxtTotales.Text = $"Base {Formato.Importe(TotalBase)}   IVA {Formato.Importe(TotalIva)}" +
                          (re != 0 ? $"   RE {Formato.Importe(re)}" : "");
    }

    private void BtnAnadir_Click(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        var l = new FacturaImpuesto();
        l.PropertyChanged += Linea_PropertyChanged;
        _lineas.Add(l);
        Grid.SelectedItem = l;
        Grid.CurrentCell = new DataGridCellInfo(l, Grid.Columns[0]);
        Grid.BeginEdit();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    private void BtnQuitar_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not FacturaImpuesto l || _lineas.Count <= 1) return;
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        l.PropertyChanged -= Linea_PropertyChanged;
        _lineas.Remove(l);
        ActualizarTotales();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    private void BtnCalcular_Click(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var l in _lineas)
        {
            l.CuotaIVA = Math.Round(l.BaseImponible * l.PorcIVA / 100m, 2, MidpointRounding.AwayFromZero);
            if (l.PorcRE is { } pre) l.CuotaRE = Math.Round(l.BaseImponible * pre / 100m, 2, MidpointRounding.AwayFromZero);
        }
        Grid.Items.Refresh();
    }
}
