using FacturasCefer.Config;
using FacturasCefer.Models;
using FacturasCefer.Services;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Exportacion.A3;

/// <summary>Datos internos de una factura necesarios para exportarla (capa 1: obtención).</summary>
public sealed class A3FacturaOrigen
{
    public int Id { get; init; }
    public DateTime FechaFactura { get; init; }
    public string NumeroFactura { get; init; } = "";
    public string? Concepto { get; init; }
    public decimal? BaseImponible { get; init; }
    public decimal? PorcIVA { get; init; }
    public decimal? CuotaIVA { get; init; }
    public decimal? PorcIRPF { get; init; }
    public decimal? CuotaIRPF { get; init; }
    public decimal Total { get; init; }
    public string? CuentaGasto { get; init; }
    public EstadoFactura Estado { get; init; }

    public int IdProveedor { get; init; }
    public string Proveedor { get; init; } = "";
    public string CIF { get; init; } = "";
    public string? Pais { get; init; }
    public string? CuentaProveedor { get; init; }

    /// <summary>Desglose (vacío en facturas anteriores a la migración: se usa la cabecera).</summary>
    public List<FacturaImpuesto> Impuestos { get; init; } = new();

    public DateTime? UltimaExportacion { get; init; }
}

/// <summary>Lectura de facturas para exportar y registro de exportaciones (dbo.FacturaExportacion).</summary>
public sealed class A3ExportRepository
{
    public const string Destino = "A3";
    private readonly AppConfig _cfg;

    public A3ExportRepository(AppConfig cfg) => _cfg = cfg;

    private SqlConnection Conexion() => new(_cfg.Facturas.ConnectionString);

    public async Task<List<A3FacturaOrigen>> ObtenerAsync(DateTime? desde, DateTime? hasta,
        IReadOnlyCollection<EstadoFactura> estados, bool incluirExportadas, CancellationToken ct = default)
    {
        var lista = new List<A3FacturaOrigen>();
        if (estados.Count == 0) return lista;

        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using (var cmd = cn.CreateCommand())
        {
            var ps = new List<string>();
            var i = 0;
            foreach (var e in estados)
            {
                ps.Add("@e" + i);
                cmd.Parameters.AddWithValue("@e" + i++, (byte)e);
            }
            cmd.CommandText = $@"
                SELECT f.Id, f.FechaFactura, f.NumeroFactura, f.Concepto, f.BaseImponible, f.PorcIVA, f.CuotaIVA,
                       f.PorcIRPF, f.CuotaIRPF, f.Total, f.CuentaContable, f.Estado,
                       p.Id, p.RazonSocial, p.CIF, p.Pais, p.CuentaProveedor,
                       (SELECT MAX(x.Fecha) FROM dbo.FacturaExportacion x WHERE x.IdFactura = f.Id AND x.Destino = @dest)
                FROM dbo.FacturaProveedores f
                JOIN dbo.Proveedor p ON p.Id = f.IdProveedor
                WHERE f.Estado IN ({string.Join(",", ps)})
                  AND (@desde IS NULL OR f.FechaFactura >= @desde)
                  AND (@hasta IS NULL OR f.FechaFactura <= @hasta)
                  AND (@todas = 1 OR NOT EXISTS (SELECT 1 FROM dbo.FacturaExportacion x WHERE x.IdFactura = f.Id AND x.Destino = @dest))
                ORDER BY f.FechaFactura, p.RazonSocial, f.NumeroFactura";
            cmd.Parameters.Add("@desde", System.Data.SqlDbType.Date).Value = (object?)desde?.Date ?? DBNull.Value;
            cmd.Parameters.Add("@hasta", System.Data.SqlDbType.Date).Value = (object?)hasta?.Date ?? DBNull.Value;
            cmd.Parameters.AddWithValue("@todas", incluirExportadas);
            cmd.Parameters.AddWithValue("@dest", Destino);

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                decimal? Dec(int c) => rd.IsDBNull(c) ? null : rd.GetDecimal(c);
                string? Str(int c) => rd.IsDBNull(c) ? null : rd.GetString(c).Trim();
                lista.Add(new A3FacturaOrigen
                {
                    Id = rd.GetInt32(0),
                    FechaFactura = rd.GetDateTime(1),
                    NumeroFactura = rd.GetString(2),
                    Concepto = Str(3),
                    BaseImponible = Dec(4),
                    PorcIVA = Dec(5),
                    CuotaIVA = Dec(6),
                    PorcIRPF = Dec(7),
                    CuotaIRPF = Dec(8),
                    Total = rd.GetDecimal(9),
                    CuentaGasto = Str(10),
                    Estado = (EstadoFactura)rd.GetByte(11),
                    IdProveedor = rd.GetInt32(12),
                    Proveedor = rd.GetString(13),
                    CIF = rd.GetString(14),
                    Pais = Str(15),
                    CuentaProveedor = Str(16),
                    UltimaExportacion = rd.IsDBNull(17) ? null : rd.GetDateTime(17),
                });
            }
        }

        // Desgloses (reutiliza la lectura de FacturaService)
        var desgloses = await FacturaService.LeerImpuestosAsync(cn, lista.Select(f => f.Id).ToList(), ct);
        foreach (var f in lista)
            if (desgloses.TryGetValue(f.Id, out var l)) f.Impuestos.AddRange(l);
        return lista;
    }

    /// <summary>Registra la exportación de las facturas (no impide volver a exportarlas).</summary>
    public async Task RegistrarAsync(IEnumerable<int> ids, string archivo, int idUsuario, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        foreach (var id in ids)
        {
            await using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO dbo.FacturaExportacion (IdFactura, Destino, IdUsuario, Archivo)
                                VALUES (@id, @dest, @usr, @arch)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@dest", Destino);
            cmd.Parameters.AddWithValue("@usr", idUsuario);
            cmd.Parameters.AddWithValue("@arch", archivo.Length > 400 ? archivo[^400..] : archivo);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
