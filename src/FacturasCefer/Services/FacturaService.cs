using System.IO;
using FacturasCefer.Config;
using FacturasCefer.Models;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

/// <summary>
/// Ruta en la carpeta de red del PDF subido tal cual. Se comparte entre todas las facturas
/// de un mismo PDF para copiarlo una sola vez.
/// </summary>
public sealed class OriginalGuardado
{
    public string? RutaUnc { get; set; }
}

public enum AccionEstado { Validar, Pagar, Rechazar, Deshacer }

/// <summary>Resultado de un cambio de estado masivo: cuántas se aplicaron y por qué se omitieron las demás.</summary>
public sealed record ResultadoCambio(int Aplicadas, List<string> Omitidas);

/// <summary>
/// Facturas de proveedores: alta (copia de PDFs a la carpeta de red + INSERT), listado,
/// edición y cambios de estado con histórico.
/// </summary>
public sealed class FacturaService
{
    private readonly AppConfig _cfg;

    public FacturaService(AppConfig cfg) => _cfg = cfg;

    private SqlConnection Conexion() => new(_cfg.Facturas.ConnectionString);

    public async Task<bool> ExisteAsync(int idProveedor, string numeroFactura, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.FacturaProveedores WHERE IdProveedor = @p AND NumeroFactura = @n";
        cmd.Parameters.AddWithValue("@p", idProveedor);
        cmd.Parameters.AddWithValue("@n", numeroFactura.Trim());
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    /// <summary>
    /// Guarda la factura: copia a <c>{RutaUnc}\{año}\{guid}.pdf</c> sus páginas del PDF subido
    /// (y el PDF subido completo a <c>originales\</c> si aún no se había copiado), e inserta la factura
    /// con estado Recibida y su fila de histórico. Si el INSERT falla, borra los ficheros copiados.
    /// </summary>
    public async Task<int> GuardarAsync(FacturaProveedor f, string pdfSubido, int paginaInicio, int paginaFin,
        OriginalGuardado original, int idUsuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cfg.Repositorio.RutaUnc))
            throw new ReglaNegocioException("Falta la carpeta de red en appsettings.json (Repositorio.RutaUnc).");

        var carpeta = Path.Combine(_cfg.Repositorio.RutaUnc, f.FechaFactura.Year.ToString());
        var copiados = new List<string>();
        var originalCopiadoAhora = false;

        try
        {
            try
            {
                Directory.CreateDirectory(carpeta);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ReglaNegocioException($"No se puede escribir en la carpeta de red {carpeta}: {ex.Message}");
            }

            if (original.RutaUnc is null)
            {
                var carpetaOriginales = Path.Combine(carpeta, "originales");
                Directory.CreateDirectory(carpetaOriginales);
                var destOriginal = Path.Combine(carpetaOriginales, Guid.NewGuid().ToString("D") + ".pdf");
                File.Copy(pdfSubido, destOriginal);
                copiados.Add(destOriginal);
                original.RutaUnc = destOriginal;
                originalCopiadoAhora = true;
            }

            var destino = Path.Combine(carpeta, Guid.NewGuid().ToString("D") + ".pdf");
            if (paginaInicio == 1 && paginaFin == PdfService.ContarPaginas(pdfSubido))
                File.Copy(pdfSubido, destino);
            else
                PdfService.ExtraerPaginas(pdfSubido, paginaInicio, paginaFin, destino);
            copiados.Add(destino);

            f.RutaPdf = destino;
            f.RutaPdfOriginal = original.RutaUnc;
            f.PaginaInicio = (short)paginaInicio;
            f.PaginaFin = (short)paginaFin;

            return await InsertarAsync(f, idUsuario, ct);
        }
        catch
        {
            foreach (var ruta in copiados)
            {
                try { File.Delete(ruta); } catch { /* ignore */ }
            }
            if (originalCopiadoAhora) original.RutaUnc = null;
            throw;
        }
    }

    private async Task<int> InsertarAsync(FacturaProveedor f, int idUsuario, CancellationToken ct)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        int id;
        await using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO dbo.FacturaProveedores
                (IdProveedor, NumeroFactura, Concepto, FechaFactura, FechaVencimiento, BaseImponible, PorcIVA, CuotaIVA,
                 PorcIRPF, CuotaIRPF, Total, IBAN, FormaPago, Estado, RutaPdf, RutaPdfOriginal, PaginaInicio, PaginaFin,
                 NombreOriginal, JsonExtraccionIA, Observaciones, IdUsuarioRegistro)
                OUTPUT INSERTED.Id
                VALUES (@prov, @num, @conc, @fec, @vto, @base, @piva, @civa, @pirpf, @cirpf, @total, @iban, @fpago, @estado,
                        @ruta, @rutaOrig, @pini, @pfin, @nomOrig, @json, @obs, @usr)";
            cmd.Parameters.AddWithValue("@prov", f.IdProveedor);
            cmd.Parameters.AddWithValue("@num", f.NumeroFactura.Trim());
            cmd.Parameters.AddWithValue("@conc", SqlUtil.DbVal(f.Concepto));
            cmd.Parameters.AddWithValue("@fec", f.FechaFactura.Date);
            cmd.Parameters.AddWithValue("@vto", (object?)f.FechaVencimiento?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@base", (object?)f.BaseImponible ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@piva", (object?)f.PorcIVA ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@civa", (object?)f.CuotaIVA ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pirpf", (object?)f.PorcIRPF ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cirpf", (object?)f.CuotaIRPF ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@total", f.Total);
            cmd.Parameters.AddWithValue("@iban", SqlUtil.DbVal(Validaciones.NormalizarIban(f.IBAN)));
            cmd.Parameters.AddWithValue("@fpago", (byte)f.FormaPago);
            cmd.Parameters.AddWithValue("@estado", (byte)f.Estado);
            cmd.Parameters.AddWithValue("@ruta", f.RutaPdf);
            cmd.Parameters.AddWithValue("@rutaOrig", SqlUtil.DbVal(f.RutaPdfOriginal));
            cmd.Parameters.AddWithValue("@pini", (object?)f.PaginaInicio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pfin", (object?)f.PaginaFin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nomOrig", SqlUtil.DbVal(f.NombreOriginal));
            cmd.Parameters.AddWithValue("@json", SqlUtil.DbVal(f.JsonExtraccionIA));
            cmd.Parameters.AddWithValue("@obs", SqlUtil.DbVal(f.Observaciones));
            cmd.Parameters.AddWithValue("@usr", idUsuario);
            try
            {
                id = (int)(await cmd.ExecuteScalarAsync(ct))!;
            }
            catch (SqlException ex) when (SqlUtil.EsDuplicado(ex))
            {
                throw new ReglaNegocioException($"Ya existe la factura nº {f.NumeroFactura} de este proveedor.");
            }
        }

        await using (var hist = cn.CreateCommand())
        {
            hist.Transaction = tx;
            hist.CommandText = @"INSERT INTO dbo.FacturaEstadoHistorico (IdFactura, EstadoAnterior, EstadoNuevo, IdUsuario, Comentario)
                                 VALUES (@id, NULL, @estado, @usr, N'Alta de la factura')";
            hist.Parameters.AddWithValue("@id", id);
            hist.Parameters.AddWithValue("@estado", (byte)f.Estado);
            hist.Parameters.AddWithValue("@usr", idUsuario);
            await hist.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return id;
    }

    // ------------------------------------------------------------------ Listado

    private const string SelectListado = @"
        SELECT f.Id, f.IdProveedor, p.RazonSocial, p.CIF, p.IBAN AS IbanProveedor, f.NumeroFactura, f.Concepto,
               f.FechaFactura, f.FechaVencimiento, f.BaseImponible, f.PorcIVA, f.CuotaIVA, f.PorcIRPF, f.CuotaIRPF,
               f.Total, f.IBAN, f.FormaPago, f.Estado, f.FechaPago, f.MotivoRechazo, f.RutaPdf, f.RutaPdfOriginal,
               f.Observaciones, f.FechaRegistro
        FROM dbo.FacturaProveedores f
        JOIN dbo.Proveedor p ON p.Id = f.IdProveedor";

    public async Task<List<FacturaListado>> BuscarAsync(FiltroFacturas filtro, CancellationToken ct = default)
    {
        var lista = new List<FacturaListado>();
        if (filtro.Estados.Count == 0) return lista;

        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();

        var estados = new List<string>();
        for (var i = 0; i < filtro.Estados.Count; i++)
        {
            estados.Add("@e" + i);
            cmd.Parameters.AddWithValue("@e" + i, (byte)filtro.Estados[i]);
        }
        var campoFecha = filtro.PorVencimiento ? "f.FechaVencimiento" : "f.FechaFactura";

        cmd.CommandText = SelectListado + $@"
            WHERE f.Estado IN ({string.Join(",", estados)})
              AND (@prov IS NULL OR f.IdProveedor = @prov)
              AND (@fpago IS NULL OR f.FormaPago = @fpago)
              AND (@desde IS NULL OR {campoFecha} >= @desde)
              AND (@hasta IS NULL OR {campoFecha} <= @hasta)
              AND (@t IS NULL OR f.NumeroFactura LIKE '%' + @t + '%' OR f.Concepto LIKE '%' + @t + '%'
                   OR p.RazonSocial LIKE '%' + @t + '%' OR p.CIF LIKE '%' + @t + '%')
            ORDER BY f.FechaFactura DESC, f.Id DESC";
        cmd.Parameters.AddWithValue("@prov", (object?)filtro.IdProveedor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fpago", filtro.FormaPago is { } fp ? (byte)fp : DBNull.Value);
        cmd.Parameters.Add("@desde", System.Data.SqlDbType.Date).Value = (object?)filtro.Desde?.Date ?? DBNull.Value;
        cmd.Parameters.Add("@hasta", System.Data.SqlDbType.Date).Value = (object?)filtro.Hasta?.Date ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@t", SqlUtil.DbVal(filtro.Texto));

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) lista.Add(LeerListado(rd));
        return lista;
    }

    public async Task<FacturaListado?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = SelectListado + " WHERE f.Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? LeerListado(rd) : null;
    }

    public async Task<List<CambioEstado>> HistorialAsync(int idFactura, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        // El nombre de usuario está en DMSTRA (mismo servidor).
        cmd.CommandText = @"SELECT h.Fecha, h.EstadoAnterior, h.EstadoNuevo,
                                   ISNULL(LTRIM(RTRIM(u.NombreUser)), CAST(h.IdUsuario AS varchar(12))), h.Comentario
                            FROM dbo.FacturaEstadoHistorico h
                            LEFT JOIN DMSTRA.dbo.Usuarios u ON u.idUsuario = h.IdUsuario
                            WHERE h.IdFactura = @id
                            ORDER BY h.Fecha DESC, h.Id DESC";
        cmd.Parameters.AddWithValue("@id", idFactura);

        var lista = new List<CambioEstado>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            lista.Add(new CambioEstado(
                rd.GetDateTime(0),
                rd.IsDBNull(1) ? null : (EstadoFactura)rd.GetByte(1),
                (EstadoFactura)rd.GetByte(2),
                rd.GetString(3),
                rd.IsDBNull(4) ? null : rd.GetString(4)));
        return lista;
    }

    // ------------------------------------------------------------------ Edición

    /// <summary>Guarda los datos editables de la factura. Solo se permite en estado Recibida o Validada.</summary>
    public async Task ActualizarAsync(FacturaListado f, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"UPDATE dbo.FacturaProveedores
                            SET NumeroFactura = @num, Concepto = @conc, FechaFactura = @fec, FechaVencimiento = @vto,
                                BaseImponible = @base, PorcIVA = @piva, CuotaIVA = @civa, PorcIRPF = @pirpf,
                                CuotaIRPF = @cirpf, Total = @total, IBAN = @iban, FormaPago = @fpago, Observaciones = @obs
                            WHERE Id = @id AND Estado IN (1, 2)";
        cmd.Parameters.AddWithValue("@id", f.Id);
        cmd.Parameters.AddWithValue("@num", f.NumeroFactura.Trim());
        cmd.Parameters.AddWithValue("@conc", SqlUtil.DbVal(f.Concepto));
        cmd.Parameters.AddWithValue("@fec", f.FechaFactura.Date);
        cmd.Parameters.AddWithValue("@vto", (object?)f.FechaVencimiento?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@base", (object?)f.BaseImponible ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@piva", (object?)f.PorcIVA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@civa", (object?)f.CuotaIVA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pirpf", (object?)f.PorcIRPF ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cirpf", (object?)f.CuotaIRPF ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@total", f.Total);
        cmd.Parameters.AddWithValue("@iban", SqlUtil.DbVal(Validaciones.NormalizarIban(f.IBAN)));
        cmd.Parameters.AddWithValue("@fpago", (byte)f.FormaPago);
        cmd.Parameters.AddWithValue("@obs", SqlUtil.DbVal(f.Observaciones));
        try
        {
            if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                throw new ReglaNegocioException("La factura ya no se puede editar: su estado ha cambiado. Actualiza el listado.");
        }
        catch (SqlException ex) when (SqlUtil.EsDuplicado(ex))
        {
            throw new ReglaNegocioException($"Ya existe otra factura nº {f.NumeroFactura} de este proveedor.");
        }
    }

    // ------------------------------------------------------------------ Estados

    /// <summary>
    /// Aplica una acción de estado a varias facturas (cada una en su transacción). Las que no la admiten
    /// se omiten con el motivo. Flujo: Recibida → Validada → Pagada; Recibida/Validada → Rechazada;
    /// Deshacer vuelve al estado anterior del último cambio (nunca hacia Pagada ni Rechazada).
    /// </summary>
    public async Task<ResultadoCambio> CambiarEstadoAsync(IEnumerable<int> ids, AccionEstado accion, DateTime? fechaPago,
        string? comentario, int idUsuario, CancellationToken ct = default)
    {
        var aplicadas = 0;
        var omitidas = new List<string>();

        await using var cn = Conexion();
        await cn.OpenAsync(ct);

        foreach (var id in ids)
        {
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

            EstadoFactura actual;
            string numero;
            await using (var sel = cn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT Estado, NumeroFactura FROM dbo.FacturaProveedores WITH (UPDLOCK) WHERE Id = @id";
                sel.Parameters.AddWithValue("@id", id);
                await using var rd = await sel.ExecuteReaderAsync(ct);
                if (!await rd.ReadAsync(ct)) { omitidas.Add($"Id {id}: no existe."); continue; }
                actual = (EstadoFactura)rd.GetByte(0);
                numero = rd.GetString(1);
            }

            EstadoFactura? nuevo = null;
            string? motivo = null;
            string? textoHist = comentario;
            switch (accion)
            {
                case AccionEstado.Validar:
                    if (actual == EstadoFactura.Recibida) nuevo = EstadoFactura.Validada;
                    else motivo = $"está {Textos.Estado(actual)}; solo se validan las Recibidas.";
                    break;

                case AccionEstado.Pagar:
                    if (actual == EstadoFactura.Validada)
                    {
                        nuevo = EstadoFactura.Pagada;
                        textoHist = $"Pagada el {fechaPago:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(comentario) ? "" : ". " + comentario.Trim());
                    }
                    else motivo = actual == EstadoFactura.Recibida
                        ? "hay que validarla antes de marcarla pagada."
                        : $"está {Textos.Estado(actual)}.";
                    break;

                case AccionEstado.Rechazar:
                    if (actual is EstadoFactura.Recibida or EstadoFactura.Validada) nuevo = EstadoFactura.Rechazada;
                    else motivo = $"está {Textos.Estado(actual)}; solo se rechazan Recibidas o Validadas.";
                    break;

                case AccionEstado.Deshacer:
                    await using (var ult = cn.CreateCommand())
                    {
                        ult.Transaction = tx;
                        ult.CommandText = @"SELECT TOP 1 EstadoAnterior FROM dbo.FacturaEstadoHistorico
                                            WHERE IdFactura = @id ORDER BY Fecha DESC, Id DESC";
                        ult.Parameters.AddWithValue("@id", id);
                        var r = await ult.ExecuteScalarAsync(ct);
                        var anterior = r is null or DBNull ? (EstadoFactura?)null : (EstadoFactura)(byte)r;
                        if (anterior is null) motivo = "no hay ningún cambio de estado que deshacer.";
                        else if (anterior is EstadoFactura.Pagada or EstadoFactura.Rechazada)
                            motivo = $"volvería a {Textos.Estado(anterior.Value)}; usa el botón correspondiente.";
                        else
                        {
                            nuevo = anterior;
                            textoHist = $"Deshacer ({Textos.Estado(actual)} → {Textos.Estado(anterior.Value)})"
                                        + (string.IsNullOrWhiteSpace(comentario) ? "" : ". " + comentario.Trim());
                        }
                    }
                    break;
            }

            if (nuevo is null)
            {
                omitidas.Add($"Nº {numero}: {motivo}");
                await tx.RollbackAsync(ct);
                continue;
            }

            await using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = @"UPDATE dbo.FacturaProveedores
                                    SET Estado = @n,
                                        FechaPago = CASE WHEN @n = 3 THEN @fp ELSE NULL END,
                                        MotivoRechazo = CASE WHEN @n = 4 THEN @mot ELSE NULL END
                                    WHERE Id = @id";
                upd.Parameters.AddWithValue("@n", (byte)nuevo.Value);
                upd.Parameters.Add("@fp", System.Data.SqlDbType.Date).Value = (object?)fechaPago?.Date ?? DBNull.Value;
                upd.Parameters.AddWithValue("@mot", SqlUtil.DbVal(accion == AccionEstado.Rechazar ? comentario : null));
                upd.Parameters.AddWithValue("@id", id);
                await upd.ExecuteNonQueryAsync(ct);
            }

            await using (var hist = cn.CreateCommand())
            {
                hist.Transaction = tx;
                hist.CommandText = @"INSERT INTO dbo.FacturaEstadoHistorico (IdFactura, EstadoAnterior, EstadoNuevo, IdUsuario, Comentario)
                                     VALUES (@id, @ant, @nue, @usr, @com)";
                hist.Parameters.AddWithValue("@id", id);
                hist.Parameters.AddWithValue("@ant", (byte)actual);
                hist.Parameters.AddWithValue("@nue", (byte)nuevo.Value);
                hist.Parameters.AddWithValue("@usr", idUsuario);
                hist.Parameters.AddWithValue("@com", SqlUtil.DbVal(textoHist is { Length: > 500 } ? textoHist[..500] : textoHist));
                await hist.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            aplicadas++;
        }

        return new ResultadoCambio(aplicadas, omitidas);
    }

    private static FacturaListado LeerListado(SqlDataReader rd)
    {
        decimal? Dec(string c) { var i = rd.GetOrdinal(c); return rd.IsDBNull(i) ? null : rd.GetDecimal(i); }
        DateTime? Fecha(string c) { var i = rd.GetOrdinal(c); return rd.IsDBNull(i) ? null : rd.GetDateTime(i); }

        return new FacturaListado
        {
            Id = rd.GetInt32(rd.GetOrdinal("Id")),
            IdProveedor = rd.GetInt32(rd.GetOrdinal("IdProveedor")),
            Proveedor = rd.Str("RazonSocial") ?? "",
            CIF = rd.Str("CIF") ?? "",
            IbanProveedor = rd.Str("IbanProveedor"),
            NumeroFactura = rd.Str("NumeroFactura") ?? "",
            Concepto = rd.Str("Concepto"),
            FechaFactura = rd.GetDateTime(rd.GetOrdinal("FechaFactura")),
            FechaVencimiento = Fecha("FechaVencimiento"),
            BaseImponible = Dec("BaseImponible"),
            PorcIVA = Dec("PorcIVA"),
            CuotaIVA = Dec("CuotaIVA"),
            PorcIRPF = Dec("PorcIRPF"),
            CuotaIRPF = Dec("CuotaIRPF"),
            Total = rd.GetDecimal(rd.GetOrdinal("Total")),
            IBAN = rd.Str("IBAN"),
            FormaPago = (FormaPago)rd.GetByte(rd.GetOrdinal("FormaPago")),
            Estado = (EstadoFactura)rd.GetByte(rd.GetOrdinal("Estado")),
            FechaPago = Fecha("FechaPago"),
            MotivoRechazo = rd.Str("MotivoRechazo"),
            RutaPdf = rd.Str("RutaPdf") ?? "",
            RutaPdfOriginal = rd.Str("RutaPdfOriginal"),
            Observaciones = rd.Str("Observaciones"),
            FechaRegistro = rd.GetDateTime(rd.GetOrdinal("FechaRegistro")),
        };
    }
}
