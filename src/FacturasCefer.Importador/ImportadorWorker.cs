using FacturasCefer.Config;
using FacturasCefer.Services;

namespace FacturasCefer.Importador;

/// <summary>
/// Cada <c>Importador.IntervaloMinutos</c> revisa la carpeta de Drive, procesa los PDF nuevos con la lógica
/// de la app y los mueve a Procesadas / Duplicadas / SinFactura / Error.
/// </summary>
public sealed class ImportadorWorker : BackgroundService
{
    public const string Fuente = "drive";

    private readonly AppConfig _cfg;
    private readonly ImportacionAutomaticaService _importacion;
    private readonly ImportacionRepository _registro;
    private readonly ILogger<ImportadorWorker> _log;
    private readonly Lazy<DriveCliente> _drive;

    public ImportadorWorker(AppConfig cfg, ImportacionAutomaticaService importacion, ImportacionRepository registro,
        ILogger<ImportadorWorker> log)
    {
        _cfg = cfg;
        _importacion = importacion;
        _registro = registro;
        _log = log;
        _drive = new Lazy<DriveCliente>(() => new DriveCliente(Programa.RutaJuntoAlExe(cfg.Importador.CredencialesGoogle),
            cfg.Importador.CarpetaEntradaId));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Importador iniciado. Intervalo {Min} min. Simulación: {Sim}.",
            _cfg.Importador.IntervaloMinutos, _cfg.Importador.Simular);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UnaPasadaAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error revisando la carpeta de Drive");
            }
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _cfg.Importador.IntervaloMinutos)), ct);
        }
    }

    /// <summary>Procesa todos los PDF pendientes una vez. Devuelve cuántos ha procesado.</summary>
    public async Task<int> UnaPasadaAsync(CancellationToken ct)
    {
        var simular = _cfg.Importador.Simular;
        var drive = _drive.Value;
        var pendientes = await drive.ListarPendientesAsync(ct);
        if (pendientes.Count > 0) _log.LogInformation("{N} PDF pendiente(s) en Drive.", pendientes.Count);

        var procesados = 0;
        foreach (var pdf in pendientes)
        {
            ct.ThrowIfCancellationRequested();
            if (await _registro.YaProcesadoAsync(Fuente, pdf.Id, ct))
            {
                _log.LogWarning("«{Nombre}» ya estaba registrado; se mueve a Procesadas.", pdf.Nombre);
                if (!simular) await drive.MoverAsync(pdf.Id, "Procesadas", ct);
                continue;
            }

            var temp = Path.Combine(Path.GetTempPath(), "FacturasCefer.Importador", Guid.NewGuid().ToString("N") + ".pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
            try
            {
                await drive.DescargarAsync(pdf.Id, temp, ct);
                var r = await _importacion.ProcesarAsync(temp, pdf.Nombre, pdf.Remitente, _cfg.Importador.IdUsuario, simular, ct);
                _log.LogInformation("«{Nombre}» ({Remitente}) → {Resultado}\n{Mensaje}", pdf.Nombre, pdf.Remitente, r.Resultado, r.Mensaje);

                if (!simular)
                {
                    await _registro.RegistrarAsync(Fuente, pdf.Id, pdf.Nombre, pdf.Remitente, pdf.Asunto, r, ct);
                    await drive.MoverAsync(pdf.Id, r.Resultado switch
                    {
                        ResultadoImportacion.Procesada => "Procesadas",
                        ResultadoImportacion.Duplicada => "Duplicadas",
                        ResultadoImportacion.SinFactura => "SinFactura",
                        _ => "Error",
                    }, ct);
                }
                procesados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Error procesando «{Nombre}»; se reintentará en la próxima pasada.", pdf.Nombre);
            }
            finally
            {
                try { File.Delete(temp); } catch { /* ignore */ }
            }
        }
        return procesados;
    }
}
