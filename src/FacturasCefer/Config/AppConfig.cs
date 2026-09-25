using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FacturasCefer.Config;

/// <summary>
/// Configuración de la aplicación, cargada de appsettings.json (junto al ejecutable).
/// </summary>
public sealed class AppConfig
{
    public ConnCfg Facturas { get; set; } = new();
    public ConnCfg Dmstra { get; set; } = new();
    public RepoCfg Repositorio { get; set; } = new();
    public ClaudeCfg Claude { get; set; } = new();

    /// <summary>CIF de CEFER, para que la IA no lo confunda con el del proveedor.</summary>
    public string CifPropio { get; set; } = "";

    public sealed class ConnCfg
    {
        public string ConnectionString { get; set; } = "";
    }

    public sealed class RepoCfg
    {
        public string RutaUnc { get; set; } = "";
    }

    public sealed class ClaudeCfg
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "claude-sonnet-5";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Carga appsettings.json del directorio del ejecutable. Lanza si falta o es inválido.</summary>
    public static AppConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"No se encuentra appsettings.json en {path}. Copia appsettings.example.json a appsettings.json y rellena las credenciales.");

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts)
                  ?? throw new InvalidOperationException("appsettings.json vacío o inválido.");
        return cfg;
    }
}
