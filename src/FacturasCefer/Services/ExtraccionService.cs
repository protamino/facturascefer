using System.IO;
using System.Net.Http;
using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using FacturasCefer.Config;
using FacturasCefer.Models;

namespace FacturasCefer.Services;

/// <summary>
/// Extrae los datos de las facturas de un PDF con Claude (PDF como document block + structured outputs).
/// Un PDF puede contener varias facturas: se devuelve una por cada una, con su rango de páginas.
/// </summary>
public sealed class ExtraccionService
{
    private const long MaxBytesPdf = 30 * 1024 * 1024; // límite de la API: 32 MB por petición (base64 incluido)

    private readonly AppConfig _cfg;
    private readonly AnthropicClient _client;

    public ExtraccionService(AppConfig cfg)
    {
        _cfg = cfg;
        _client = new AnthropicClient { ApiKey = cfg.Claude.ApiKey };
    }

    /// <summary>Envía el PDF a Claude y devuelve las facturas detectadas. Lanza ReglaNegocioException con mensajes para el usuario.</summary>
    public async Task<List<FacturaExtraida>> ExtraerAsync(string rutaPdf, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cfg.Claude.ApiKey))
            throw new ReglaNegocioException("Falta la clave de la API de Claude en appsettings.json (Claude.ApiKey).");

        var bytes = await File.ReadAllBytesAsync(rutaPdf, ct);
        if (bytes.Length * 4L / 3 > MaxBytesPdf)
            throw new ReglaNegocioException("El PDF es demasiado grande para la IA (máx. ~22 MB). Rellena los datos a mano.");

        var parametros = new MessageCreateParams
        {
            Model = _cfg.Claude.Model,
            MaxTokens = 16000,
            // Si un clasificador de seguridad rechaza la petición, la API la reintenta sola en el modelo recomendado.
            Betas = new List<ApiEnum<string, AnthropicBeta>> { AnthropicBeta.ServerSideFallback2026_07_01 },
            Fallbacks = new Default(),
            System = PromptSistema(),
            OutputConfig = new BetaOutputConfig
            {
                Effort = Effort.Medium,
                Format = new BetaJsonOutputFormat { Schema = Esquema() },
            },
            Messages = new List<BetaMessageParam>
            {
                new BetaMessageParam
                {
                    Role = Role.User,
                    Content = new List<BetaContentBlockParam>
                    {
                        new BetaRequestDocumentBlock { Source = new BetaBase64PdfSource { Data = Convert.ToBase64String(bytes) } },
                        new BetaTextBlockParam { Text = "Extrae los datos de todas las facturas de este PDF." },
                    },
                },
            },
        };

        BetaMessage respuesta;
        try
        {
            respuesta = await _client.Beta.Messages.Create(parametros, ct);
        }
        catch (AnthropicUnauthorizedException)
        {
            throw new ReglaNegocioException("La clave de la API de Claude no es válida. Revisa appsettings.json.");
        }
        catch (AnthropicRateLimitException)
        {
            throw new ReglaNegocioException("La API de Claude está saturada o se ha superado el límite. Espera un momento y reintenta.");
        }
        catch (AnthropicApiException ex)
        {
            throw new ReglaNegocioException("Error de la API de Claude: " + ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new ReglaNegocioException("No hay conexión con la API de Claude (api.anthropic.com): " + ex.Message);
        }

        if (respuesta.StopReason == BetaStopReason.Refusal)
            throw new ReglaNegocioException("La IA no ha procesado este documento. Rellena los datos a mano.");
        if (respuesta.StopReason == BetaStopReason.MaxTokens)
            throw new ReglaNegocioException("La respuesta de la IA se ha cortado (documento demasiado largo). Prueba a dividir el PDF.");

        var json = string.Concat(respuesta.Content
            .Select(b => b.TryPickText(out var t) ? t.Text : null)
            .Where(t => t is not null));

        var resultado = JsonSerializer.Deserialize<ResultadoExtraccion>(json)
                        ?? throw new ReglaNegocioException("La IA ha devuelto una respuesta vacía.");
        return resultado.Facturas;
    }

    private string PromptSistema() => $$"""
        Eres un asistente de administración de INSTITUTO DE REPRODUCCION CEFER, S.L. (CIF {{_cfg.CifPropio}}).
        Recibes PDFs con facturas que proveedores han emitido a CEFER y extraes sus datos.

        Reglas:
        - CEFER es siempre el DESTINATARIO. Nunca uses el CIF {{_cfg.CifPropio}} ni los datos de CEFER como proveedor:
          el proveedor es quien EMITE la factura.
        - Un PDF puede contener varias facturas: devuelve un elemento por factura distinta, con sus páginas
          (pagina_inicio y pagina_fin, empezando en 1). Las páginas de continuación pertenecen a la misma factura.
          Cada página que empieza con su propia cabecera de factura (emisor, número, fecha) es una factura distinta,
          aunque repita el número de otra (copias duplicadas): devuélvelas por separado.
          Si el documento no contiene ninguna factura, devuelve la lista vacía.
        - Fechas en formato AAAA-MM-DD. Importes como número con punto decimal, sin símbolo de moneda.
        - Si la factura está exenta de IVA, porc_iva y cuota_iva son 0. IRPF solo si aparece una retención
          (cuota_irpf en positivo); si no aparece, null.
        - Si hay varios tipos de IVA, suma las bases y las cuotas; en porc_iva pon el tipo principal.
        - forma_pago: "domiciliacion" si la factura se cobra por recibo/adeudo domiciliado en la cuenta del cliente
          (p. ej. "recibo domiciliado", "domiciliación bancaria", "adeudo SEPA", "giro"); "transferencia" si CEFER debe
          transferir a una cuenta del proveedor; "otra" si indica otro medio (tarjeta, contado…); "" si no consta.
        - iban: la cuenta bancaria que aparece para el pago, sin espacios. Si es domiciliación, es la cuenta de CEFER
          donde se carga el recibo (no la del proveedor); si es transferencia, la del proveedor. CIF sin espacios ni guiones.
        - concepto: resumen breve (máx. 200 caracteres) de lo facturado, tal como aparece en las líneas.
        - Si un dato no aparece, no lo inventes: cadena vacía "" en los campos de texto y null en los importes.
        - campos_dudosos: nombres de los campos que no has encontrado o que has leído con poca seguridad
          (texto borroso, ambiguo o deducido).
        """;

    private static readonly string[] CamposTexto =
    {
        "proveedor_razon_social", "proveedor_cif", "proveedor_direccion", "proveedor_cp", "proveedor_poblacion",
        "proveedor_provincia", "proveedor_email", "proveedor_telefono", "iban",
        "numero_factura", "fecha_factura", "fecha_vencimiento", "concepto",
    };

    private static readonly string[] CamposNumero =
        { "base_imponible", "porc_iva", "cuota_iva", "porc_irpf", "cuota_irpf", "total" };

    private static Dictionary<string, JsonElement> Esquema()
    {
        var anyNull = (string tipo) => new Dictionary<string, object>
        {
            ["anyOf"] = new object[] { new { type = tipo }, new { type = "null" } },
        };

        var props = new Dictionary<string, object>
        {
            ["pagina_inicio"] = new { type = "integer" },
            ["pagina_fin"] = new { type = "integer" },
        };
        // Texto: "" si no aparece (la API limita a 16 los campos con unión/nullable). Importes: null.
        foreach (var c in CamposTexto) props[c] = new { type = "string" };
        props["forma_pago"] = new { type = "string", @enum = new[] { "transferencia", "domiciliacion", "otra", "" } };
        foreach (var c in CamposNumero) props[c] = anyNull("number");
        props["campos_dudosos"] = new
        {
            type = "array",
            items = new { type = "string", @enum = CamposTexto.Concat(CamposNumero).ToArray() },
        };

        var factura = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = props.Keys.ToArray(),
            ["additionalProperties"] = false,
        };

        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["facturas"] = new { type = "array", items = factura },
            }),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "facturas" }),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        };
    }
}
