using FacturasCefer.Config;
using FacturasCefer.Models;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

/// <summary>Cifra (número de facturas e importe) de una tarjeta del panel de inicio.</summary>
public sealed record Cifra(int Numero, decimal Importe);

/// <summary>Gasto de un mes (facturas no rechazadas por fecha de factura).</summary>
public sealed record GastoMes(DateTime Mes, decimal Importe, int Numero)
{
    public string Texto => Mes.ToString("MMM yyyy");
}

/// <summary>Datos del panel de inicio.</summary>
public sealed class ResumenInicio
{
    public Cifra Pendientes { get; set; } = new(0, 0);
    public Cifra Vencidas { get; set; } = new(0, 0);
    public Cifra VencenPronto { get; set; } = new(0, 0);
    public int PorRevisar { get; set; }
    public int SinCuenta { get; set; }
    public int ProveedoresPorRevisar { get; set; }
    public Cifra MesActual { get; set; } = new(0, 0);
    public Cifra MesAnterior { get; set; } = new(0, 0);
    public List<GastoMes> GastoPorMes { get; } = new();
    public List<FacturaListado> ProximosVencimientos { get; } = new();
    public int ImportacionesConIncidencia24h { get; set; }
    public int ImportadasHoy { get; set; }
    public EstadoImportador? Importador { get; set; }
}

/// <summary>Consultas agregadas para el panel de inicio (solo lectura).</summary>
public sealed class ResumenService
{
    private readonly AppConfig _cfg;
    private readonly FacturaService _facturas;
    private readonly ImportacionRepository _importaciones;

    public ResumenService(AppConfig cfg, FacturaService facturas, ImportacionRepository importaciones)
    {
        _cfg = cfg;
        _facturas = facturas;
        _importaciones = importaciones;
    }

    public async Task<ResumenInicio> ObtenerAsync(CancellationToken ct = default)
    {
        var r = new ResumenInicio();
        var hoy = DateTime.Today;
        var mes = new DateTime(hoy.Year, hoy.Month, 1);

        await using (var cn = new SqlConnection(_cfg.Facturas.ConnectionString))
        {
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    SUM(CASE WHEN Estado IN (1,2) THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Estado IN (1,2) THEN Total ELSE 0 END),
                    SUM(CASE WHEN Estado IN (1,2) AND FechaVencimiento < @hoy THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Estado IN (1,2) AND FechaVencimiento < @hoy THEN Total ELSE 0 END),
                    SUM(CASE WHEN Estado IN (1,2) AND FechaVencimiento BETWEEN @hoy AND @limite THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Estado IN (1,2) AND FechaVencimiento BETWEEN @hoy AND @limite THEN Total ELSE 0 END),
                    SUM(CASE WHEN Revisar = 1 AND Estado <> 4 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN CuentaContable IS NULL AND Estado <> 4 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Estado <> 4 AND FechaFactura >= @mes THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Estado <> 4 AND FechaFactura >= @mes THEN Total ELSE 0 END),
                    SUM(CASE WHEN Estado <> 4 AND FechaFactura >= @mesAnt AND FechaFactura < @mes THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Estado <> 4 AND FechaFactura >= @mesAnt AND FechaFactura < @mes THEN Total ELSE 0 END),
                    (SELECT COUNT(*) FROM dbo.Proveedor WHERE PendienteRevision = 1 AND Baja = 0)
                FROM dbo.FacturaProveedores;

                SELECT DATEFROMPARTS(YEAR(FechaFactura), MONTH(FechaFactura), 1) AS Mes, SUM(Total), COUNT(*)
                FROM dbo.FacturaProveedores
                WHERE Estado <> 4 AND FechaFactura >= @desde6
                GROUP BY DATEFROMPARTS(YEAR(FechaFactura), MONTH(FechaFactura), 1)
                ORDER BY Mes;";
            cmd.Parameters.Add("@hoy", System.Data.SqlDbType.Date).Value = hoy;
            cmd.Parameters.Add("@limite", System.Data.SqlDbType.Date).Value = hoy.AddDays(FacturaListado.DiasAvisoVencimiento);
            cmd.Parameters.Add("@mes", System.Data.SqlDbType.Date).Value = mes;
            cmd.Parameters.Add("@mesAnt", System.Data.SqlDbType.Date).Value = mes.AddMonths(-1);
            cmd.Parameters.Add("@desde6", System.Data.SqlDbType.Date).Value = mes.AddMonths(-5);

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                int N(int i) => rd.IsDBNull(i) ? 0 : rd.GetInt32(i);
                decimal D(int i) => rd.IsDBNull(i) ? 0 : rd.GetDecimal(i);
                r.Pendientes = new(N(0), D(1));
                r.Vencidas = new(N(2), D(3));
                r.VencenPronto = new(N(4), D(5));
                r.PorRevisar = N(6);
                r.SinCuenta = N(7);
                r.MesActual = new(N(8), D(9));
                r.MesAnterior = new(N(10), D(11));
                r.ProveedoresPorRevisar = N(12);
            }
            await rd.NextResultAsync(ct);
            var porMes = new Dictionary<DateTime, GastoMes>();
            while (await rd.ReadAsync(ct))
                porMes[rd.GetDateTime(0)] = new GastoMes(rd.GetDateTime(0), rd.GetDecimal(1), rd.GetInt32(2));
            for (var m = mes.AddMonths(-5); m <= mes; m = m.AddMonths(1))
                r.GastoPorMes.Add(porMes.TryGetValue(m, out var g) ? g : new GastoMes(m, 0, 0));
        }

        // Próximos pagos: vencidas y que vencen pronto, por fecha de vencimiento
        var proximas = await _facturas.BuscarAsync(new FiltroFacturas
        {
            Estados = { EstadoFactura.Recibida, EstadoFactura.Validada },
            Vencimiento = FiltroVencimiento.VencidasOPronto,
        }, ct);
        r.ProximosVencimientos.AddRange(proximas.OrderBy(f => f.FechaVencimiento).Take(12));

        var desde = DateTime.Now.AddHours(-24);
        var recientes = await _importaciones.ListarAsync(new FiltroImportaciones { Desde = DateTime.Today.AddDays(-1) }, ct);
        r.ImportacionesConIncidencia24h = recientes.Count(i => i.Fecha >= desde && i.ConIncidencia);
        r.ImportadasHoy = recientes.Count(i => i.Fecha.Date == DateTime.Today && i.Resultado == ResultadoImportacion.Procesada);
        r.Importador = await _importaciones.EstadoAsync(ct);
        return r;
    }
}
