using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace FacturasCefer.Importador;

/// <summary>PDF pendiente en la carpeta de entrada de Drive, con los datos del correo que dejó n8n.</summary>
public sealed record PdfEnDrive(string Id, string Nombre, string? Remitente, string? Asunto);

/// <summary>
/// Acceso a la carpeta «CEFER/Facturas» de Google Drive con una cuenta de servicio
/// (la carpeta debe estar compartida con el correo de la cuenta de servicio como Editor).
/// Subcarpetas de resultado: Procesadas, Duplicadas, SinFactura, Error (se crean si no existen).
/// </summary>
public sealed class DriveCliente
{
    private const string MimeCarpeta = "application/vnd.google-apps.folder";
    private readonly DriveService _drive;
    private readonly string _entradaId;
    private readonly Dictionary<string, string> _subcarpetas = new();

    public DriveCliente(string rutaCredenciales, string carpetaEntradaId)
    {
        if (!File.Exists(rutaCredenciales))
            throw new FileNotFoundException($"No se encuentra el fichero de credenciales de Google: {rutaCredenciales}");
        if (string.IsNullOrWhiteSpace(carpetaEntradaId))
            throw new InvalidOperationException("Falta Importador.CarpetaEntradaId en appsettings.json (id de la carpeta CEFER/Facturas de Drive).");

        var credencial = GoogleCredential.FromFile(rutaCredenciales).CreateScoped(DriveService.Scope.Drive);
        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credencial,
            ApplicationName = "FacturasCefer Importador",
        });
        _entradaId = carpetaEntradaId.Trim();
    }

    /// <summary>PDF en la carpeta de entrada (no en las subcarpetas), del más antiguo al más nuevo.</summary>
    public async Task<List<PdfEnDrive>> ListarPendientesAsync(CancellationToken ct)
    {
        var lista = new List<PdfEnDrive>();
        string? pagina = null;
        do
        {
            var req = _drive.Files.List();
            req.Q = $"'{_entradaId}' in parents and mimeType = 'application/pdf' and trashed = false";
            req.Fields = "nextPageToken, files(id, name, properties, createdTime)";
            req.OrderBy = "createdTime";
            req.PageSize = 100;
            req.PageToken = pagina;
            req.SupportsAllDrives = true;
            req.IncludeItemsFromAllDrives = true;
            var res = await req.ExecuteAsync(ct);
            foreach (var f in res.Files ?? new List<DriveFile>())
            {
                string? Prop(string k) => f.Properties is not null && f.Properties.TryGetValue(k, out var v) ? v : null;
                lista.Add(new PdfEnDrive(f.Id, f.Name, Prop("remitente"), Prop("asunto")));
            }
            pagina = res.NextPageToken;
        } while (pagina is not null);
        return lista;
    }

    public async Task DescargarAsync(string id, string destino, CancellationToken ct)
    {
        await using var fs = File.Create(destino);
        var req = _drive.Files.Get(id);
        req.SupportsAllDrives = true;
        var progreso = await req.DownloadAsync(fs, ct);
        if (progreso.Exception is not null) throw progreso.Exception;
    }

    /// <summary>Mueve el fichero de la carpeta de entrada a la subcarpeta indicada (se crea si no existe).</summary>
    public async Task MoverAsync(string id, string subcarpeta, CancellationToken ct)
    {
        var destino = await SubcarpetaAsync(subcarpeta, ct);
        var req = _drive.Files.Update(new DriveFile(), id);
        req.AddParents = destino;
        req.RemoveParents = _entradaId;
        req.SupportsAllDrives = true;
        await req.ExecuteAsync(ct);
    }

    private async Task<string> SubcarpetaAsync(string nombre, CancellationToken ct)
    {
        if (_subcarpetas.TryGetValue(nombre, out var id)) return id;

        var buscar = _drive.Files.List();
        buscar.Q = $"'{_entradaId}' in parents and name = '{nombre}' and mimeType = '{MimeCarpeta}' and trashed = false";
        buscar.Fields = "files(id)";
        buscar.SupportsAllDrives = true;
        buscar.IncludeItemsFromAllDrives = true;
        var existentes = await buscar.ExecuteAsync(ct);
        if (existentes.Files?.FirstOrDefault() is { } f)
            return _subcarpetas[nombre] = f.Id;

        var crear = _drive.Files.Create(new DriveFile { Name = nombre, MimeType = MimeCarpeta, Parents = new[] { _entradaId } });
        crear.Fields = "id";
        crear.SupportsAllDrives = true;
        var nueva = await crear.ExecuteAsync(ct);
        return _subcarpetas[nombre] = nueva.Id;
    }
}
