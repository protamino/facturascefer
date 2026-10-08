using FacturasCefer.Config;
using FacturasCefer.Models;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

/// <summary>Catálogo de cuentas contables (dbo.CuentaContable), con baja lógica.</summary>
public sealed class CuentaContableService
{
    private readonly AppConfig _cfg;

    public CuentaContableService(AppConfig cfg) => _cfg = cfg;

    private SqlConnection Conexion() => new(_cfg.Facturas.ConnectionString);

    /// <summary>Cuentas con el nº de proveedores y facturas que las usan.</summary>
    public async Task<List<CuentaContable>> BuscarAsync(string? texto, bool incluirBajas, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT c.Codigo, c.Descripcion, c.Baja, c.FechaAlta,
                                   (SELECT COUNT(*) FROM dbo.Proveedor p WHERE p.CuentaContable = c.Codigo),
                                   (SELECT COUNT(*) FROM dbo.FacturaProveedores f WHERE f.CuentaContable = c.Codigo)
                            FROM dbo.CuentaContable c
                            WHERE (@t IS NULL OR c.Codigo LIKE @t + '%' OR c.Descripcion LIKE '%' + @t + '%')
                              AND (@bajas = 1 OR c.Baja = 0)
                            ORDER BY c.Codigo";
        cmd.Parameters.AddWithValue("@t", SqlUtil.DbVal(texto));
        cmd.Parameters.AddWithValue("@bajas", incluirBajas);

        var lista = new List<CuentaContable>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            lista.Add(new CuentaContable
            {
                Codigo = rd.GetString(0),
                Descripcion = rd.GetString(1),
                Baja = rd.GetBoolean(2),
                FechaAlta = rd.GetDateTime(3),
                Proveedores = rd.GetInt32(4),
                Facturas = rd.GetInt32(5),
            });
        return lista;
    }

    public async Task CrearAsync(CuentaContable c, int idUsuario, CancellationToken ct = default)
    {
        if (!Validaciones.CuentaValida(c.Codigo))
            throw new ReglaNegocioException("El código de cuenta debe tener exactamente 8 dígitos.");

        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"INSERT INTO dbo.CuentaContable (Codigo, Descripcion, IdUsuarioAlta) VALUES (@c, @d, @usr)";
        cmd.Parameters.AddWithValue("@c", c.Codigo);
        cmd.Parameters.AddWithValue("@d", c.Descripcion.Trim());
        cmd.Parameters.AddWithValue("@usr", idUsuario);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (SqlUtil.EsDuplicado(ex))
        {
            throw new ReglaNegocioException($"Ya existe la cuenta {c.Codigo}.");
        }
    }

    /// <summary>Solo cambia la descripción (el código no se modifica: lo referencian proveedores y facturas).</summary>
    public async Task ActualizarAsync(CuentaContable c, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.CuentaContable SET Descripcion = @d WHERE Codigo = @c";
        cmd.Parameters.AddWithValue("@c", c.Codigo);
        cmd.Parameters.AddWithValue("@d", c.Descripcion.Trim());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CambiarBajaAsync(string codigo, bool baja, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.CuentaContable SET Baja = @b WHERE Codigo = @c";
        cmd.Parameters.AddWithValue("@b", baja);
        cmd.Parameters.AddWithValue("@c", codigo);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
