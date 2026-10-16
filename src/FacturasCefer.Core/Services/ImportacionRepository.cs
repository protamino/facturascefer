using FacturasCefer.Config;
using FacturasCefer.Models;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

/// <summary>Registro de los PDF procesados por el importador (dbo.FacturaImportacion).</summary>
public sealed class ImportacionRepository
{
    private readonly AppConfig _cfg;

    public ImportacionRepository(AppConfig cfg) => _cfg = cfg;

    private SqlConnection Conexion() => new(_cfg.Facturas.ConnectionString);

    public async Task<bool> YaProcesadoAsync(string fuente, string idExterno, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.FacturaImportacion WHERE Fuente = @f AND IdExterno = @id";
        cmd.Parameters.AddWithValue("@f", fuente);
        cmd.Parameters.AddWithValue("@id", idExterno);
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    public async Task RegistrarAsync(string fuente, string idExterno, string nombreFichero, string? remitente,
        string? asunto, ResultadoImportacion r, CancellationToken ct = default)
    {
        static object Txt(string? s, int max) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : (s.Length > max ? s[..max] : s);

        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"INSERT INTO dbo.FacturaImportacion
                                (Fuente, IdExterno, NombreFichero, CorreoRemitente, CorreoAsunto, Resultado, Mensaje, IdsFacturas)
                            VALUES (@f, @id, @nom, @rem, @asu, @res, @msg, @ids)";
        cmd.Parameters.AddWithValue("@f", fuente);
        cmd.Parameters.AddWithValue("@id", idExterno);
        cmd.Parameters.AddWithValue("@nom", Txt(nombreFichero, 400));
        cmd.Parameters.AddWithValue("@rem", Txt(remitente, 200));
        cmd.Parameters.AddWithValue("@asu", Txt(asunto, 500));
        cmd.Parameters.AddWithValue("@res", r.Resultado);
        cmd.Parameters.AddWithValue("@msg", Txt(r.Mensaje, 100_000));
        cmd.Parameters.AddWithValue("@ids", Txt(string.Join(",", r.IdsFacturas), 400));
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (SqlUtil.EsDuplicado(ex))
        {
            // Ya registrado (p. ej. reintento tras un corte): no es un error.
        }
    }

    /// <summary>Registro de importaciones (más recientes primero; máx. 2000 filas).</summary>
    public async Task<List<RegistroImportacion>> ListarAsync(FiltroImportaciones filtro, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP (2000) Id, Fuente, IdExterno, NombreFichero, CorreoRemitente, CorreoAsunto, Fecha, Resultado, Mensaje, IdsFacturas
            FROM dbo.FacturaImportacion
            WHERE (@res IS NULL OR Resultado = @res OR (@res = 'incidencias' AND Resultado IN ('error', 'sin_factura')))
              AND (@desde IS NULL OR Fecha >= @desde)
              AND (@hasta IS NULL OR Fecha < DATEADD(day, 1, @hasta))
              AND (@t IS NULL OR NombreFichero LIKE '%' + @t + '%' OR CorreoRemitente LIKE '%' + @t + '%'
                   OR CorreoAsunto LIKE '%' + @t + '%' OR Mensaje LIKE '%' + @t + '%')
            ORDER BY Fecha DESC, Id DESC";
        cmd.Parameters.AddWithValue("@res", SqlUtil.DbVal(filtro.Resultado));
        cmd.Parameters.Add("@desde", System.Data.SqlDbType.Date).Value = (object?)filtro.Desde?.Date ?? DBNull.Value;
        cmd.Parameters.Add("@hasta", System.Data.SqlDbType.Date).Value = (object?)filtro.Hasta?.Date ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@t", SqlUtil.DbVal(filtro.Texto));

        var lista = new List<RegistroImportacion>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            string? S(int i) => rd.IsDBNull(i) ? null : rd.GetString(i);
            lista.Add(new RegistroImportacion
            {
                Id = rd.GetInt32(0),
                Fuente = rd.GetString(1),
                IdExterno = rd.GetString(2),
                NombreFichero = rd.GetString(3),
                Remitente = S(4),
                Asunto = S(5),
                Fecha = rd.GetDateTime(6),
                Resultado = rd.GetString(7),
                Mensaje = S(8),
                IdsFacturas = S(9),
            });
        }
        return lista;
    }

    /// <summary>Latido del servicio: cuándo terminó la última pasada por Drive y cómo.</summary>
    public async Task RegistrarLatidoAsync(bool correcta, string? mensaje, string version, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            MERGE dbo.ImportadorEstado AS d
            USING (SELECT 1 AS Id) AS o ON d.Id = o.Id
            WHEN MATCHED THEN UPDATE SET UltimaPasada = SYSDATETIME(), Correcta = @ok, Mensaje = @msg, Equipo = @eq, Version = @ver
            WHEN NOT MATCHED THEN INSERT (Id, UltimaPasada, Correcta, Mensaje, Equipo, Version)
                                  VALUES (1, SYSDATETIME(), @ok, @msg, @eq, @ver);";
        cmd.Parameters.AddWithValue("@ok", correcta);
        cmd.Parameters.AddWithValue("@msg", string.IsNullOrWhiteSpace(mensaje) ? DBNull.Value : (mensaje.Length > 500 ? mensaje[..500] : mensaje));
        cmd.Parameters.AddWithValue("@eq", Environment.MachineName);
        cmd.Parameters.AddWithValue("@ver", version);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Último latido, o null si el servicio nunca ha escrito uno.</summary>
    public async Task<EstadoImportador?> EstadoAsync(CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT UltimaPasada, Correcta, Mensaje, Equipo, Version FROM dbo.ImportadorEstado WHERE Id = 1";
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return null;
        string? S(int i) => rd.IsDBNull(i) ? null : rd.GetString(i);
        return new EstadoImportador(rd.GetDateTime(0), rd.GetBoolean(1), S(2), S(3), S(4));
    }
}
