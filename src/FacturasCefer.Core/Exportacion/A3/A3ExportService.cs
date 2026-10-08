using FacturasCefer.Models;

namespace FacturasCefer.Exportacion.A3;

/// <summary>Una factura preparada para exportar: datos, filas resultantes e incidencias.</summary>
public sealed class A3FacturaPreparada
{
    public required A3FacturaOrigen Origen { get; init; }
    public required List<A3ReceivedInvoiceExportDTO> Filas { get; init; }
    public required List<A3Incidencia> Incidencias { get; init; }

    public bool TieneErrores => Incidencias.Any(i => i.Nivel == A3Nivel.Error);
}

public sealed record A3ResultadoExportacion(int Facturas, int Filas, string Archivo);

/// <summary>
/// Orquesta la exportación de facturas recibidas a a3ASESOR | con:
/// obtención (repositorio) → transformación → validación → CSV → registro de la exportación.
/// </summary>
public sealed class A3ExportService
{
    private readonly A3ExportRepository _repo;

    public A3ExportService(A3ExportRepository repo) => _repo = repo;

    public A3ExportConfig Configuracion { get; private set; } = new();

    /// <summary>Lee la configuración (a3-exportacion.json) y prepara las facturas del filtro.</summary>
    public async Task<List<A3FacturaPreparada>> PrepararAsync(DateTime? desde, DateTime? hasta,
        IReadOnlyCollection<EstadoFactura> estados, bool incluirExportadas, CancellationToken ct = default)
    {
        Configuracion = A3ExportConfig.Cargar();
        var transformer = new A3Transformer(Configuracion, DateTime.Now);
        var validator = new A3Validator(Configuracion);

        var origenes = await _repo.ObtenerAsync(desde, hasta, estados, incluirExportadas, ct);
        return origenes.Select(o =>
        {
            var filas = transformer.Transformar(o);
            return new A3FacturaPreparada { Origen = o, Filas = filas, Incidencias = validator.Validar(o, filas) };
        }).ToList();
    }

    /// <summary>Genera el CSV con las facturas indicadas (las que tienen errores se descartan) y registra la exportación.</summary>
    public async Task<A3ResultadoExportacion> ExportarAsync(IEnumerable<A3FacturaPreparada> facturas, string ruta,
        int idUsuario, CancellationToken ct = default)
    {
        var validas = facturas.Where(f => !f.TieneErrores).ToList();
        if (validas.Count == 0) throw new Services.ReglaNegocioException("No hay facturas sin errores para exportar.");

        var filas = validas.SelectMany(f => f.Filas).ToList();
        new A3CsvWriter(Configuracion).Escribir(ruta, filas);
        await _repo.RegistrarAsync(validas.Select(f => f.Origen.Id), ruta, idUsuario, ct);
        return new A3ResultadoExportacion(validas.Count, filas.Count, ruta);
    }
}
