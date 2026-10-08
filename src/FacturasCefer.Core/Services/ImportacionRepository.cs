using FacturasCefer.Config;
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
}
