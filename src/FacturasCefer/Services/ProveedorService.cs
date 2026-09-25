using FacturasCefer.Config;
using FacturasCefer.Models;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

/// <summary>CRUD de dbo.Proveedor (con baja lógica). Los cambios de IBAN quedan en dbo.ProveedorIbanHistorico.</summary>
public sealed class ProveedorService
{
    private readonly AppConfig _cfg;

    public ProveedorService(AppConfig cfg) => _cfg = cfg;

    private SqlConnection Conexion() => new(_cfg.Facturas.ConnectionString);

    private const string Columnas =
        "Id, RazonSocial, CIF, Direccion, CP, Poblacion, Provincia, Pais, IBAN, FormaPago, Tarjeta, Email, Telefono, Observaciones, Baja, FechaAlta";

    public async Task<List<Proveedor>> BuscarAsync(string? texto, bool incluirBajas, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"SELECT {Columnas} FROM dbo.Proveedor
                             WHERE (@t IS NULL OR RazonSocial LIKE '%' + @t + '%' OR CIF LIKE '%' + @t + '%')
                               AND (@bajas = 1 OR Baja = 0)
                             ORDER BY RazonSocial";
        cmd.Parameters.AddWithValue("@t", SqlUtil.DbVal(texto));
        cmd.Parameters.AddWithValue("@bajas", incluirBajas);

        var lista = new List<Proveedor>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) lista.Add(Leer(rd));
        return lista;
    }

    public async Task<Proveedor?> ObtenerPorCifAsync(string cif, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM dbo.Proveedor WHERE CIF = @cif";
        cmd.Parameters.AddWithValue("@cif", Validaciones.NormalizarCif(cif));
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? Leer(rd) : null;
    }

    /// <summary>Inserta y devuelve el Id.</summary>
    public async Task<int> CrearAsync(Proveedor p, int idUsuario, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"INSERT INTO dbo.Proveedor
                                (RazonSocial, CIF, Direccion, CP, Poblacion, Provincia, Pais, IBAN, FormaPago, Tarjeta, Email, Telefono, Observaciones, IdUsuarioAlta)
                            OUTPUT INSERTED.Id
                            VALUES (@rs, @cif, @dir, @cp, @pob, @prov, @pais, @iban, @fpago, @tarj, @email, @tel, @obs, @usr)";
        AddParams(cmd, p);
        cmd.Parameters.AddWithValue("@usr", idUsuario);
        try
        {
            return (int)(await cmd.ExecuteScalarAsync(ct))!;
        }
        catch (SqlException ex) when (SqlUtil.EsDuplicado(ex))
        {
            throw new ReglaNegocioException($"Ya existe un proveedor con el CIF {Validaciones.NormalizarCif(p.CIF)}.");
        }
    }

    /// <summary>
    /// Actualiza el proveedor. Si el IBAN cambia, registra el cambio en ProveedorIbanHistorico
    /// (con la factura que lo originó, si la hay) en la misma transacción.
    /// </summary>
    public async Task ActualizarAsync(Proveedor p, int idUsuario, int? idFacturaOrigen = null, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        string? ibanAnterior;
        await using (var sel = cn.CreateCommand())
        {
            sel.Transaction = tx;
            sel.CommandText = "SELECT IBAN FROM dbo.Proveedor WITH (UPDLOCK) WHERE Id = @id";
            sel.Parameters.AddWithValue("@id", p.Id);
            var r = await sel.ExecuteScalarAsync(ct);
            ibanAnterior = r is DBNull or null ? null : (string)r;
        }

        await using (var upd = cn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = @"UPDATE dbo.Proveedor
                                SET RazonSocial = @rs, CIF = @cif, Direccion = @dir, CP = @cp, Poblacion = @pob,
                                    Provincia = @prov, Pais = @pais, IBAN = @iban, FormaPago = @fpago, Tarjeta = @tarj, Email = @email, Telefono = @tel,
                                    Observaciones = @obs
                                WHERE Id = @id";
            AddParams(upd, p);
            upd.Parameters.AddWithValue("@id", p.Id);
            try
            {
                await upd.ExecuteNonQueryAsync(ct);
            }
            catch (SqlException ex) when (SqlUtil.EsDuplicado(ex))
            {
                throw new ReglaNegocioException($"Ya existe otro proveedor con el CIF {Validaciones.NormalizarCif(p.CIF)}.");
            }
        }

        var ibanNuevo = Validaciones.NormalizarIban(p.IBAN);
        if (Validaciones.NormalizarIban(ibanAnterior) != ibanNuevo)
        {
            await using var hist = cn.CreateCommand();
            hist.Transaction = tx;
            hist.CommandText = @"INSERT INTO dbo.ProveedorIbanHistorico (IdProveedor, IbanAnterior, IbanNuevo, IdUsuario, IdFacturaOrigen)
                                 VALUES (@id, @ant, @nue, @usr, @fac)";
            hist.Parameters.AddWithValue("@id", p.Id);
            hist.Parameters.AddWithValue("@ant", SqlUtil.DbVal(ibanAnterior));
            hist.Parameters.AddWithValue("@nue", SqlUtil.DbVal(ibanNuevo));
            hist.Parameters.AddWithValue("@usr", idUsuario);
            hist.Parameters.AddWithValue("@fac", (object?)idFacturaOrigen ?? DBNull.Value);
            await hist.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task CambiarBajaAsync(int id, bool baja, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Proveedor SET Baja = @b WHERE Id = @id";
        cmd.Parameters.AddWithValue("@b", baja);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<IbanCambio>> HistorialIbanAsync(int idProveedor, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT Fecha, IbanAnterior, IbanNuevo, IdUsuario, IdFacturaOrigen
                            FROM dbo.ProveedorIbanHistorico WHERE IdProveedor = @id ORDER BY Fecha DESC, Id DESC";
        cmd.Parameters.AddWithValue("@id", idProveedor);

        var lista = new List<IbanCambio>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            lista.Add(new IbanCambio(
                rd.GetDateTime(0),
                rd.IsDBNull(1) ? null : rd.GetString(1),
                rd.IsDBNull(2) ? null : rd.GetString(2),
                rd.GetInt32(3),
                rd.IsDBNull(4) ? null : rd.GetInt32(4)));
        return lista;
    }

    private static void AddParams(SqlCommand cmd, Proveedor p)
    {
        cmd.Parameters.AddWithValue("@rs", p.RazonSocial.Trim());
        cmd.Parameters.AddWithValue("@cif", Validaciones.NormalizarCif(p.CIF));
        cmd.Parameters.AddWithValue("@dir", SqlUtil.DbVal(p.Direccion));
        cmd.Parameters.AddWithValue("@cp", SqlUtil.DbVal(p.CP));
        cmd.Parameters.AddWithValue("@pob", SqlUtil.DbVal(p.Poblacion));
        cmd.Parameters.AddWithValue("@prov", SqlUtil.DbVal(p.Provincia));
        cmd.Parameters.AddWithValue("@pais", string.IsNullOrWhiteSpace(p.Pais) ? "España" : p.Pais.Trim());
        cmd.Parameters.AddWithValue("@iban", SqlUtil.DbVal(Validaciones.NormalizarIban(p.IBAN)));
        cmd.Parameters.AddWithValue("@fpago", (byte)p.FormaPago);
        cmd.Parameters.AddWithValue("@tarj", SqlUtil.DbVal(Validaciones.EnmascararTarjeta(p.Tarjeta)));
        cmd.Parameters.AddWithValue("@email", SqlUtil.DbVal(p.Email));
        cmd.Parameters.AddWithValue("@tel", SqlUtil.DbVal(p.Telefono));
        cmd.Parameters.AddWithValue("@obs", SqlUtil.DbVal(p.Observaciones));
    }

    private static Proveedor Leer(SqlDataReader rd) => new()
    {
        Id = rd.GetInt32(rd.GetOrdinal("Id")),
        RazonSocial = rd.Str("RazonSocial") ?? "",
        CIF = rd.Str("CIF") ?? "",
        Direccion = rd.Str("Direccion"),
        CP = rd.Str("CP"),
        Poblacion = rd.Str("Poblacion"),
        Provincia = rd.Str("Provincia"),
        Pais = rd.Str("Pais") ?? "España",
        IBAN = rd.Str("IBAN"),
        FormaPago = (FormaPago)rd.GetByte(rd.GetOrdinal("FormaPago")),
        Tarjeta = rd.Str("Tarjeta"),
        Email = rd.Str("Email"),
        Telefono = rd.Str("Telefono"),
        Observaciones = rd.Str("Observaciones"),
        Baja = rd.GetBoolean(rd.GetOrdinal("Baja")),
        FechaAlta = rd.GetDateTime(rd.GetOrdinal("FechaAlta")),
    };
}
