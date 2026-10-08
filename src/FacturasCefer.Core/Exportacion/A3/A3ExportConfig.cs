using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FacturasCefer.Exportacion.A3;

/// <summary>
/// Configuración de la exportación a a3ASESOR | con. Se lee de <c>a3-exportacion.json</c> junto al exe;
/// si no existe se usan los valores por defecto. Permite adaptar columnas, formato y equivalencias
/// a la plantilla del Importador de Datos de A3 sin tocar código.
/// </summary>
public sealed class A3ExportConfig
{
    public const string NombreFichero = "a3-exportacion.json";

    // ---------- Formato del CSV
    public string Separador { get; set; } = ";";

    /// <summary>"," (por defecto, configuración española) o ".".</summary>
    public string SeparadorDecimal { get; set; } = ",";

    /// <summary>UTF-8 con BOM (recomendado para que Excel reconozca los acentos).</summary>
    public bool Bom { get; set; } = true;

    public bool IncluirCabecera { get; set; } = true;
    public string FormatoFecha { get; set; } = "dd/MM/yyyy";

    // ---------- Reglas
    /// <summary>NumeroFacturaCorto = últimos N caracteres de NumeroFactura.</summary>
    public int LongitudNumeroCorto { get; set; } = 10;

    public bool CuentaProveedorObligatoria { get; set; } = true;
    public bool CuentaGastoObligatoria { get; set; } = true;

    /// <summary>"vacia" o "fecha_factura".</summary>
    public string FechaOperacion { get; set; } = "vacia";

    /// <summary>"vacia", "fecha_factura" o "fecha_exportacion".</summary>
    public string FechaContabilizacion { get; set; } = "vacia";

    /// <summary>En facturas con varias filas: la retención solo en la primera fila (si no, se repite).</summary>
    public bool RetencionSoloPrimeraFila { get; set; } = true;

    /// <summary>En facturas con varias filas: el total solo en la primera fila (si no, se repite en todas).</summary>
    public bool TotalSoloPrimeraFila { get; set; } = false;

    // ---------- Columnas (orden y nombre de cabecera); Campo = nombre de A3Campos
    public List<A3Columna> Columnas { get; set; } = A3Campos.ColumnasPorDefecto();

    // ---------- Equivalencias (vacías por defecto: no se inventa ningún código A3)
    public A3Equivalencias Equivalencias { get; set; } = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string RutaPorDefecto => Path.Combine(AppContext.BaseDirectory, NombreFichero);

    /// <summary>Carga la configuración (o los valores por defecto si el fichero no existe).</summary>
    public static A3ExportConfig Cargar(string? ruta = null)
    {
        ruta ??= RutaPorDefecto;
        if (!File.Exists(ruta)) return new A3ExportConfig();
        try
        {
            var cfg = JsonSerializer.Deserialize<A3ExportConfig>(File.ReadAllText(ruta), Json) ?? new A3ExportConfig();
            if (cfg.Columnas.Count == 0) cfg.Columnas = A3Campos.ColumnasPorDefecto();
            return cfg;
        }
        catch (JsonException ex)
        {
            throw new Services.ReglaNegocioException($"El fichero {NombreFichero} no es un JSON válido: {ex.Message}");
        }
    }

    public string Serializar() => JsonSerializer.Serialize(this, Json);
}

public sealed class A3Columna
{
    public A3Columna() { }
    public A3Columna(string cabecera, string campo) { Cabecera = cabecera; Campo = campo; }

    /// <summary>Texto de la cabecera en el CSV (el que se mapeará en la plantilla de A3).</summary>
    public string Cabecera { get; set; } = "";

    /// <summary>Campo de A3ReceivedInvoiceExportDTO (ver A3Campos).</summary>
    public string Campo { get; set; } = "";
}

/// <summary>
/// Tablas de equivalencia interno → A3. Clave = valor interno; valor = código A3.
/// Si no hay equivalencia se usa el dato interno (cuentas) o se deja vacío (códigos A3).
/// </summary>
public sealed class A3Equivalencias
{
    /// <summary>CIF del proveedor → cuenta de proveedor A3 (si no está en la ficha del proveedor).</summary>
    public Dictionary<string, string> CuentaProveedorPorCif { get; set; } = new();

    /// <summary>Cuenta de gasto interna → cuenta de gasto A3 (si difieren).</summary>
    public Dictionary<string, string> CuentaGasto { get; set; } = new();

    /// <summary>% IVA ("21", "10", "4", "0") → código de tipo de IVA de A3. Sin equivalencia se exporta el %.</summary>
    public Dictionary<string, string> TipoIva { get; set; } = new();

    /// <summary>CIF del proveedor → serie A3.</summary>
    public Dictionary<string, string> SeriePorCif { get; set; } = new();

    /// <summary>País del proveedor (texto, p. ej. "España") → código de país de A3.</summary>
    public Dictionary<string, string> Pais { get; set; } = new();

    /// <summary>CIF del proveedor → clave de operación A3.</summary>
    public Dictionary<string, string> ClaveOperacionPorCif { get; set; } = new();

    /// <summary>% IVA → clave de operación A3 (p. ej. para exentas). Se aplica si no hay clave por CIF.</summary>
    public Dictionary<string, string> ClaveOperacionPorTipoIva { get; set; } = new();
}
