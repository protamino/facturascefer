using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace FacturasCefer.Services;

/// <summary>Columna de una tabla tal como la dejó el usuario.</summary>
public sealed class ColumnaPref
{
    public int Orden { get; set; }
    public double Ancho { get; set; }
    public DataGridLengthUnitType Unidad { get; set; }
}

/// <summary>Tamaño y posición de la ventana principal.</summary>
public sealed class VentanaPref
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximizada { get; set; }
}

/// <summary>
/// Preferencias del usuario de Windows en <c>%APPDATA%\FacturasCefer\preferencias.json</c>: tamaño de la ventana,
/// columnas y orden de las tablas, filtros del listado, secciones plegadas y carpetas de exportación.
/// Si el fichero falta o está dañado se usan los valores por defecto (nunca impide arrancar).
/// </summary>
public sealed class Preferencias
{
    private static readonly string Ruta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FacturasCefer", "preferencias.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static Preferencias? _actual;

    public static Preferencias Actual => _actual ??= Cargar();

    /// <summary>Columnas por tabla (clave de tabla → cabecera de columna → preferencia).</summary>
    public Dictionary<string, Dictionary<string, ColumnaPref>> Tablas { get; set; } = new();

    /// <summary>Orden por tabla: «cabecera|Ascending» o «cabecera|Descending».</summary>
    public Dictionary<string, string> Orden { get; set; } = new();

    /// <summary>Valores de filtros (clave → valor serializado como texto).</summary>
    public Dictionary<string, string> Filtros { get; set; } = new();

    /// <summary>Secciones plegables (clave → desplegada).</summary>
    public Dictionary<string, bool> Secciones { get; set; } = new();

    public string? CarpetaA3 { get; set; }
    public string? CarpetaExcel { get; set; }
    public VentanaPref? Ventana { get; set; }

    private static Preferencias Cargar()
    {
        try
        {
            if (File.Exists(Ruta))
                return JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) ?? new Preferencias();
        }
        catch (Exception ex)
        {
            App.Log("app-error.log", ex);
        }
        return new Preferencias();
    }

    public void Guardar()
    {
        try
        {
            foreach (var t in TablaPersistente.Registradas) t.Capturar();
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex)
        {
            App.Log("app-error.log", ex);
        }
    }

    // ------------------------------------------------------------------ Filtros

    public bool? LeerBool(string clave) => Filtros.TryGetValue(clave, out var v) && bool.TryParse(v, out var b) ? b : null;
    public int? LeerInt(string clave) => Filtros.TryGetValue(clave, out var v) && int.TryParse(v, out var i) ? i : null;
    public void Poner(string clave, object? valor)
    {
        if (valor is null) Filtros.Remove(clave);
        else Filtros[clave] = valor.ToString()!;
    }

    // ------------------------------------------------------------------ Secciones

    public bool Desplegada(string clave, bool porDefecto = true) => Secciones.TryGetValue(clave, out var v) ? v : porDefecto;

    /// <summary>Recuerda si el usuario pliega o despliega la sección.</summary>
    public void Recordar(Expander expander, string clave)
    {
        expander.IsExpanded = Desplegada(clave, expander.IsExpanded);
        expander.Expanded += (_, _) => Secciones[clave] = true;
        expander.Collapsed += (_, _) => Secciones[clave] = false;
    }
}

/// <summary>
/// Tabla cuyas columnas (orden y ancho) y orden de filas se recuerdan entre sesiones. El orden de filas también se
/// mantiene al recargar los datos (al cambiar ItemsSource WPF lo pierde).
/// </summary>
public sealed class TablaPersistente
{
    internal static readonly List<TablaPersistente> Registradas = new();

    private readonly DataGrid _grid;
    private readonly string _clave;
    private bool _restaurada;

    private TablaPersistente(DataGrid grid, string clave)
    {
        _grid = grid;
        _clave = clave;
    }

    public static void Registrar(DataGrid grid, string clave)
    {
        var t = new TablaPersistente(grid, clave);
        Registradas.Add(t);
        grid.Loaded += (_, _) => t.Restaurar();
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid))
            .AddValueChanged(grid, (_, _) => t.ReaplicarOrden());
        grid.Sorting += (_, e) =>
        {
            // Se guarda tras aplicar el orden (Sorting llega antes de cambiar SortDirection).
            grid.Dispatcher.BeginInvoke(t.CapturarOrden);
        };
    }

    private static string Cabecera(DataGridColumn c) => c.Header?.ToString() ?? c.SortMemberPath ?? "";

    private void Restaurar()
    {
        if (_restaurada) return;
        _restaurada = true;
        var p = Preferencias.Actual;
        if (p.Tablas.TryGetValue(_clave, out var cols))
        {
            try
            {
                foreach (var c in _grid.Columns)
                {
                    if (!cols.TryGetValue(Cabecera(c), out var cp)) continue;
                    if (cp.Ancho > 0) c.Width = new DataGridLength(Math.Max(cp.Ancho, c.MinWidth), cp.Unidad);
                }
                // Orden de columnas: se asigna de menor a mayor para que DisplayIndex no choque.
                foreach (var (c, cp) in _grid.Columns
                             .Select(c => (c, cp: cols.TryGetValue(Cabecera(c), out var x) ? x : null))
                             .Where(x => x.cp is not null)
                             .OrderBy(x => x.cp!.Orden))
                {
                    if (cp!.Orden >= 0 && cp.Orden < _grid.Columns.Count) c.DisplayIndex = cp.Orden;
                }
            }
            catch (Exception ex)
            {
                App.Log("app-error.log", ex); // columnas cambiadas entre versiones: se ignora
            }
        }
        ReaplicarOrden();
    }

    /// <summary>Guarda en las preferencias el orden y ancho actuales de las columnas.</summary>
    internal void Capturar()
    {
        if (!_restaurada) return;
        var cols = new Dictionary<string, ColumnaPref>();
        foreach (var c in _grid.Columns)
        {
            var w = c.Width;
            cols[Cabecera(c)] = new ColumnaPref
            {
                Orden = c.DisplayIndex,
                // Las columnas «*» se guardan tal cual; las de ancho fijo con su ancho real (el usuario las arrastra).
                Ancho = w.IsStar ? w.Value : c.ActualWidth,
                Unidad = w.IsStar ? DataGridLengthUnitType.Star : DataGridLengthUnitType.Pixel,
            };
        }
        Preferencias.Actual.Tablas[_clave] = cols;
    }

    private void CapturarOrden()
    {
        var c = _grid.Columns.FirstOrDefault(x => x.SortDirection is not null);
        if (c is null) Preferencias.Actual.Orden.Remove(_clave);
        else Preferencias.Actual.Orden[_clave] = $"{Cabecera(c)}|{c.SortDirection}";
    }

    private void ReaplicarOrden()
    {
        if (!Preferencias.Actual.Orden.TryGetValue(_clave, out var o)) return;
        var partes = o.Split('|');
        if (partes.Length != 2 || !Enum.TryParse<ListSortDirection>(partes[1], out var dir)) return;
        var col = _grid.Columns.FirstOrDefault(c => Cabecera(c) == partes[0]);
        var ruta = col?.SortMemberPath;
        if (col is null || string.IsNullOrEmpty(ruta) || _grid.ItemsSource is null) return;

        // Tras cambiar ItemsSource la vista se crea después: se aplica en la siguiente vuelta del despachador.
        _grid.Dispatcher.BeginInvoke(() =>
        {
            var vista = _grid.Items;
            vista.SortDescriptions.Clear();
            vista.SortDescriptions.Add(new SortDescription(ruta, dir));
            foreach (var c in _grid.Columns) c.SortDirection = null;
            col.SortDirection = dir;
        });
    }
}
