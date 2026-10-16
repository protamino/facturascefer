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

    /// <summary>Facturas del proveedor que podrían ser la misma (nº escrito de otra forma, o misma fecha e importe).</summary>
    public async Task<List<PosibleDuplicado>> PosiblesDuplicadosAsync(int idProveedor, string numero, DateTime fecha,
        decimal total, int? excluirId = null, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT Id, NumeroFactura, FechaFactura, Total, Estado FROM dbo.FacturaProveedores
                            WHERE IdProveedor = @p AND (@ex IS NULL OR Id <> @ex)";
        cmd.Parameters.AddWithValue("@p", idProveedor);
        cmd.Parameters.AddWithValue("@ex", (object?)excluirId ?? DBNull.Value);
        var existentes = new List<(int, string, DateTime, decimal, EstadoFactura)>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            existentes.Add((rd.GetInt32(0), rd.GetString(1), rd.GetDateTime(2), rd.GetDecimal(3), (EstadoFactura)rd.GetByte(4)));
        return Duplicados.Buscar(existentes, numero, fecha, total);
    }

    /// <summary>Facturas registradas a partir de un PDF con esta huella (el mismo fichero subido antes).</summary>
    public async Task<List<FacturaMismoPdf>> FacturasDelMismoPdfAsync(string hash, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT f.Id, p.RazonSocial, f.NumeroFactura, f.FechaRegistro
                            FROM dbo.FacturaProveedores f JOIN dbo.Proveedor p ON p.Id = f.IdProveedor
                            WHERE f.HashPdf = @h ORDER BY f.Id";
        cmd.Parameters.AddWithValue("@h", hash);
        var lista = new List<FacturaMismoPdf>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            lista.Add(new FacturaMismoPdf(rd.GetInt32(0), rd.GetString(1), rd.GetString(2), rd.GetDateTime(3)));
        return lista;
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

            AplicarTotalesDesglose(f);
            f.HashPdf ??= Duplicados.Huella(pdfSubido);
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
                 PorcIRPF, CuotaIRPF, Total, IBAN, FormaPago, Tarjeta, CuentaContable, Estado, FechaPago, RutaPdf, RutaPdfOriginal, PaginaInicio, PaginaFin,
                 NombreOriginal, JsonExtraccionIA, Observaciones, Origen, Revisar, MotivoRevision, HashPdf, IdUsuarioRegistro)
                OUTPUT INSERTED.Id
                VALUES (@prov, @num, @conc, @fec, @vto, @base, @piva, @civa, @pirpf, @cirpf, @total, @iban, @fpago, @tarj, @cta, @estado, @fpag,
                        @ruta, @rutaOrig, @pini, @pfin, @nomOrig, @json, @obs, @orig, @rev, @motrev, @hash, @usr)";
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
            cmd.Parameters.AddWithValue("@tarj", SqlUtil.DbVal(Validaciones.EnmascararTarjeta(f.Tarjeta)));
            cmd.Parameters.AddWithValue("@cta", SqlUtil.DbVal(f.CuentaContable));
            cmd.Parameters.AddWithValue("@estado", (byte)f.Estado);
            cmd.Parameters.Add("@fpag", System.Data.SqlDbType.Date).Value =
                f.Estado == EstadoFactura.Pagada ? (object?)f.FechaPago?.Date ?? f.FechaFactura.Date : DBNull.Value;
            cmd.Parameters.AddWithValue("@ruta", f.RutaPdf);
            cmd.Parameters.AddWithValue("@rutaOrig", SqlUtil.DbVal(f.RutaPdfOriginal));
            cmd.Parameters.AddWithValue("@pini", (object?)f.PaginaInicio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pfin", (object?)f.PaginaFin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nomOrig", SqlUtil.DbVal(f.NombreOriginal));
            cmd.Parameters.AddWithValue("@json", SqlUtil.DbVal(f.JsonExtraccionIA));
            cmd.Parameters.AddWithValue("@obs", SqlUtil.DbVal(f.Observaciones));
            cmd.Parameters.AddWithValue("@orig", (byte)f.Origen);
            cmd.Parameters.AddWithValue("@rev", f.Revisar);
            cmd.Parameters.AddWithValue("@motrev", SqlUtil.DbVal(f.MotivoRevision is { Length: > 500 } m ? m[..500] : f.MotivoRevision));
            cmd.Parameters.AddWithValue("@usr", idUsuario);
            cmd.Parameters.AddWithValue("@hash", SqlUtil.DbVal(f.HashPdf));
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
                                 VALUES (@id, NULL, @estado, @usr, @com)";
            var tarjeta = Validaciones.EnmascararTarjeta(f.Tarjeta);
            var com = f.Estado == EstadoFactura.Pagada
                ? $"Alta: pagada con tarjeta{(tarjeta.Length > 0 ? " " + tarjeta : "")} el {f.FechaPago ?? f.FechaFactura:dd/MM/yyyy}"
                : "Alta de la factura";
            if (f.Origen == OrigenFactura.Correo) com = "Importada automáticamente (correo). " + com;
            hist.Parameters.AddWithValue("@com", com);
            hist.Parameters.AddWithValue("@id", id);
            hist.Parameters.AddWithValue("@estado", (byte)f.Estado);
            hist.Parameters.AddWithValue("@usr", idUsuario);
            await hist.ExecuteNonQueryAsync(ct);
        }

        await InsertarImpuestosAsync(cn, tx, id, f.Impuestos, ct);

        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Cabecera (base, % IVA, cuota IVA) = suma del desglose; % solo si hay un único tipo.</summary>
    private static void AplicarTotalesDesglose(FacturaProveedor f)
    {
        if (f.Impuestos.Count == 0) return;
        f.BaseImponible = Desglose.Base(f.Impuestos);
        f.CuotaIVA = Desglose.Iva(f.Impuestos);
        f.PorcIVA = Desglose.PorcIvaUnico(f.Impuestos);
    }

    private static async Task InsertarImpuestosAsync(SqlConnection cn, SqlTransaction tx, int idFactura,
        IReadOnlyList<FacturaImpuesto> lineas, CancellationToken ct)
    {
        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            await using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO dbo.FacturaImpuesto
                                    (IdFactura, Orden, BaseImponible, PorcIVA, CuotaIVA, PorcRE, CuotaRE, CuentaContable)
                                VALUES (@f, @o, @b, @piva, @civa, @pre, @cre, @cta)";
            cmd.Parameters.AddWithValue("@f", idFactura);
            cmd.Parameters.AddWithValue("@o", (short)(i + 1));
            cmd.Parameters.AddWithValue("@b", l.BaseImponible);
            cmd.Parameters.AddWithValue("@piva", l.PorcIVA);
            cmd.Parameters.AddWithValue("@civa", l.CuotaIVA);
            cmd.Parameters.AddWithValue("@pre", (object?)l.PorcRE ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cre", (object?)l.CuotaRE ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cta", SqlUtil.DbVal(l.CuentaContable));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Desglose de impuestos de una factura, por orden.</summary>
    public async Task<List<FacturaImpuesto>> ImpuestosAsync(int idFactura, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        return await LeerImpuestosAsync(cn, new[] { idFactura }, ct) is var d && d.TryGetValue(idFactura, out var l)
            ? l : new List<FacturaImpuesto>();
    }

    /// <summary>Desgloses de varias facturas (para la exportación).</summary>
    internal static async Task<Dictionary<int, List<FacturaImpuesto>>> LeerImpuestosAsync(SqlConnection cn,
        IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var resultado = new Dictionary<int, List<FacturaImpuesto>>();
        if (ids.Count == 0) return resultado;
        foreach (var lote in ids.Chunk(1000))
        {
            await using var cmd = cn.CreateCommand();
            var ps = new List<string>();
            for (var i = 0; i < lote.Length; i++)
            {
                ps.Add("@i" + i);
                cmd.Parameters.AddWithValue("@i" + i, lote[i]);
            }
            cmd.CommandText = $@"SELECT Id, IdFactura, Orden, BaseImponible, PorcIVA, CuotaIVA, PorcRE, CuotaRE, CuentaContable
                                 FROM dbo.FacturaImpuesto WHERE IdFactura IN ({string.Join(",", ps)})
                                 ORDER BY IdFactura, Orden";
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var idF = rd.GetInt32(1);
                if (!resultado.TryGetValue(idF, out var lista)) resultado[idF] = lista = new List<FacturaImpuesto>();
                lista.Add(new FacturaImpuesto
                {
                    Id = rd.GetInt32(0),
                    Orden = rd.GetInt16(2),
                    BaseImponible = rd.GetDecimal(3),
                    PorcIVA = rd.GetDecimal(4),
                    CuotaIVA = rd.GetDecimal(5),
                    PorcRE = rd.IsDBNull(6) ? null : rd.GetDecimal(6),
                    CuotaRE = rd.IsDBNull(7) ? null : rd.GetDecimal(7),
                    CuentaContable = rd.IsDBNull(8) ? null : rd.GetString(8),
                });
            }
        }
        return resultado;
    }

    // ------------------------------------------------------------------ Listado

    private const string SelectListado = @"
        SELECT f.Id, f.IdProveedor, p.RazonSocial, p.CIF, p.IBAN AS IbanProveedor, f.NumeroFactura, f.Concepto,
               f.FechaFactura, f.FechaVencimiento, f.BaseImponible, f.PorcIVA, f.CuotaIVA, f.PorcIRPF, f.CuotaIRPF,
               f.Total, f.IBAN, f.FormaPago, f.Tarjeta, f.CuentaContable, cc.Descripcion AS CuentaDescripcion, f.Estado, f.FechaPago, f.MotivoRechazo, f.RutaPdf, f.RutaPdfOriginal,
               f.Observaciones, f.FechaRegistro, f.Origen, f.Revisar, f.MotivoRevision
        FROM dbo.FacturaProveedores f
        JOIN dbo.Proveedor p ON p.Id = f.IdProveedor
        LEFT JOIN dbo.CuentaContable cc ON cc.Codigo = f.CuentaContable";

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
              AND (@cta IS NULL OR (@cta = '' AND f.CuentaContable IS NULL) OR f.CuentaContable = @cta)
              AND (@orig IS NULL OR f.Origen = @orig)
              AND (@solorev = 0 OR f.Revisar = 1)
              AND (@venc = 0 OR (f.Estado IN (1, 2) AND f.FechaVencimiento IS NOT NULL AND (
                       (@venc IN (1, 3) AND f.FechaVencimiento < @hoy)
                    OR (@venc IN (2, 3) AND f.FechaVencimiento BETWEEN @hoy AND @limite))))
              AND (@desde IS NULL OR {campoFecha} >= @desde)
              AND (@hasta IS NULL OR {campoFecha} <= @hasta)
              AND (@t IS NULL OR f.NumeroFactura LIKE '%' + @t + '%' OR f.Concepto LIKE '%' + @t + '%'
                   OR p.RazonSocial LIKE '%' + @t + '%' OR p.CIF LIKE '%' + @t + '%')
            ORDER BY f.FechaFactura DESC, f.Id DESC";
        cmd.Parameters.AddWithValue("@prov", (object?)filtro.IdProveedor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fpago", filtro.FormaPago is { } fp ? (byte)fp : DBNull.Value);
        cmd.Parameters.AddWithValue("@cta", (object?)filtro.CuentaContable ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orig", filtro.Origen is { } o ? (byte)o : DBNull.Value);
        cmd.Parameters.AddWithValue("@solorev", filtro.SoloRevisar);
        cmd.Parameters.AddWithValue("@venc", (int)filtro.Vencimiento);
        cmd.Parameters.Add("@hoy", System.Data.SqlDbType.Date).Value = DateTime.Today;
        cmd.Parameters.Add("@limite", System.Data.SqlDbType.Date).Value = DateTime.Today.AddDays(FacturaListado.DiasAvisoVencimiento);
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

    /// <summary>Historial completo: cambios de estado y cambios de datos, del más reciente al más antiguo.</summary>
    public async Task<List<EventoHistorial>> HistorialAsync(int idFactura, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        // El nombre de usuario está en DMSTRA (mismo servidor).
        cmd.CommandText = @"SELECT h.Fecha, h.EstadoAnterior, h.EstadoNuevo,
                                   ISNULL(LTRIM(RTRIM(u.NombreUser)), CAST(h.IdUsuario AS varchar(12))), h.Comentario, h.Id
                            FROM dbo.FacturaEstadoHistorico h
                            LEFT JOIN DMSTRA.dbo.Usuarios u ON u.idUsuario = h.IdUsuario
                            WHERE h.IdFactura = @id;

                            SELECT c.Fecha, c.Campo, c.ValorAnterior, c.ValorNuevo,
                                   ISNULL(LTRIM(RTRIM(u.NombreUser)), CAST(c.IdUsuario AS varchar(12))), c.Id
                            FROM dbo.FacturaCambio c
                            LEFT JOIN DMSTRA.dbo.Usuarios u ON u.idUsuario = c.IdUsuario
                            WHERE c.IdFactura = @id;";
        cmd.Parameters.AddWithValue("@id", idFactura);

        var eventos = new List<(EventoHistorial Ev, int Orden)>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            var cambio = new CambioEstado(
                rd.GetDateTime(0),
                rd.IsDBNull(1) ? null : (EstadoFactura)rd.GetByte(1),
                (EstadoFactura)rd.GetByte(2),
                rd.GetString(3),
                rd.IsDBNull(4) ? null : rd.GetString(4));
            eventos.Add((new EventoHistorial(cambio.Fecha, cambio.Cambio, cambio.Usuario, cambio.Comentario), rd.GetInt32(5)));
        }
        await rd.NextResultAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            string V(int i) => rd.IsDBNull(i) ? "(vacío)" : rd.GetString(i);
            eventos.Add((new EventoHistorial(rd.GetDateTime(0), "✎ " + rd.GetString(1), rd.GetString(4),
                $"{V(2)}  →  {V(3)}"), rd.GetInt32(5)));
        }
        return eventos.OrderByDescending(e => e.Ev.Fecha).ThenByDescending(e => e.Orden).Select(e => e.Ev).ToList();
    }

    // ------------------------------------------------------------------ Auditoría de cambios

    /// <summary>Cambio de un dato de la factura (campo, valor anterior → nuevo).</summary>
    private sealed record Cambio(string Campo, string? Antes, string? Despues);

    private static async Task RegistrarCambiosAsync(SqlConnection cn, SqlTransaction? tx, int idFactura, int idUsuario,
        IEnumerable<Cambio> cambios, CancellationToken ct)
    {
        static object Txt(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : (s.Length > 400 ? s[..397] + "…" : s);
        foreach (var c in cambios)
        {
            await using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO dbo.FacturaCambio (IdFactura, IdUsuario, Campo, ValorAnterior, ValorNuevo)
                                VALUES (@id, @usr, @campo, @ant, @nue)";
            cmd.Parameters.AddWithValue("@id", idFactura);
            cmd.Parameters.AddWithValue("@usr", idUsuario);
            cmd.Parameters.AddWithValue("@campo", c.Campo);
            cmd.Parameters.AddWithValue("@ant", Txt(c.Antes));
            cmd.Parameters.AddWithValue("@nue", Txt(c.Despues));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static string? Fecha(DateTime? d) => d?.ToString("dd/MM/yyyy");
    private static string? Imp(decimal? d) => d is null ? null : Formato.Importe(d) + " €";
    private static string? Pct(decimal? d) => d is null ? null : Formato.Porcentaje(d) + " %";
    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? TextoDesglose(IEnumerable<FacturaImpuesto> lineas) =>
        string.Join("; ", lineas.Select(l =>
            $"{Formato.Importe(l.BaseImponible)} al {Formato.Porcentaje(l.PorcIVA)} %" +
            (l.PorcRE is { } re ? $" + RE {Formato.Porcentaje(re)} %" : "") +
            (l.CuentaContable is { } c ? $" ({c})" : ""))) is { Length: > 0 } t ? t : null;

    /// <summary>Diferencias entre la factura guardada y la editada.</summary>
    private static List<Cambio> Diferencias(FacturaListado a, FacturaListado n, IReadOnlyList<FacturaImpuesto> impAntes,
        IReadOnlyList<FacturaImpuesto> impDespues)
    {
        var lista = new List<Cambio>();
        void Comparar(string campo, string? antes, string? despues)
        {
            if (!string.Equals(antes ?? "", despues ?? "", StringComparison.Ordinal)) lista.Add(new Cambio(campo, antes, despues));
        }
        Comparar("Nº factura", Texto(a.NumeroFactura), Texto(n.NumeroFactura));
        Comparar("Fecha", Fecha(a.FechaFactura), Fecha(n.FechaFactura));
        Comparar("Vencimiento", Fecha(a.FechaVencimiento), Fecha(n.FechaVencimiento));
        Comparar("Concepto", Texto(a.Concepto), Texto(n.Concepto));
        Comparar("Forma de pago", Textos.FormaPago(a.FormaPago), Textos.FormaPago(n.FormaPago));
        Comparar("IBAN", Texto(Validaciones.FormatearIban(a.IBAN)), Texto(Validaciones.FormatearIban(n.IBAN)));
        Comparar("Tarjeta", Texto(a.Tarjeta), Texto(Validaciones.EnmascararTarjeta(n.Tarjeta)));
        Comparar("Cuenta contable", Texto(a.CuentaContable), Texto(n.CuentaContable));
        if (impDespues.Count > 0) Comparar("Desglose de IVA", TextoDesglose(impAntes), TextoDesglose(impDespues));
        Comparar("% IRPF", Pct(a.PorcIRPF), Pct(n.PorcIRPF));
        Comparar("IRPF", Imp(a.CuotaIRPF), Imp(n.CuotaIRPF));
        Comparar("Total", Imp(a.Total), Imp(n.Total));
        Comparar("Observaciones", Texto(a.Observaciones), Texto(n.Observaciones));
        return lista;
    }

    /// <summary>Tarjetas ya usadas (facturas y habituales de proveedores), para sugerirlas.</summary>
    public async Task<List<string>> TarjetasUsadasAsync(CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT Tarjeta FROM dbo.FacturaProveedores WHERE Tarjeta IS NOT NULL
                            UNION
                            SELECT Tarjeta FROM dbo.Proveedor WHERE Tarjeta IS NOT NULL
                            ORDER BY Tarjeta";
        var lista = new List<string>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) lista.Add(rd.GetString(0));
        return lista;
    }

    // ------------------------------------------------------------------ Edición

    /// <summary>Guarda los datos editables de la factura. Solo se permite en estado Recibida o Validada.</summary>
    /// <remarks>Cada dato que cambia queda en dbo.FacturaCambio con el usuario (auditoría).</remarks>
    public async Task ActualizarAsync(FacturaListado f, IReadOnlyList<FacturaImpuesto> impuestos, int idUsuario,
        CancellationToken ct = default)
    {
        if (impuestos.Count > 0)
        {
            f.BaseImponible = Desglose.Base(impuestos);
            f.CuotaIVA = Desglose.Iva(impuestos);
            f.PorcIVA = Desglose.PorcIvaUnico(impuestos);
        }

        var antes = await ObtenerAsync(f.Id, ct)
                    ?? throw new ReglaNegocioException("La factura ya no existe.");
        var impAntes = await ImpuestosAsync(f.Id, ct);
        var cambios = Diferencias(antes, f, impAntes, impuestos);

        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"UPDATE dbo.FacturaProveedores
                            SET NumeroFactura = @num, Concepto = @conc, FechaFactura = @fec, FechaVencimiento = @vto,
                                BaseImponible = @base, PorcIVA = @piva, CuotaIVA = @civa, PorcIRPF = @pirpf,
                                CuotaIRPF = @cirpf, Total = @total, IBAN = @iban, FormaPago = @fpago, Tarjeta = @tarj, CuentaContable = @cta, Observaciones = @obs
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
        cmd.Parameters.AddWithValue("@tarj", SqlUtil.DbVal(Validaciones.EnmascararTarjeta(f.Tarjeta)));
        cmd.Parameters.AddWithValue("@cta", SqlUtil.DbVal(f.CuentaContable));
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

        if (impuestos.Count > 0)
        {
            await using (var del = cn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM dbo.FacturaImpuesto WHERE IdFactura = @id";
                del.Parameters.AddWithValue("@id", f.Id);
                await del.ExecuteNonQueryAsync(ct);
            }
            await InsertarImpuestosAsync(cn, tx, f.Id, impuestos, ct);
        }
        await RegistrarCambiosAsync(cn, tx, f.Id, idUsuario, cambios, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Asigna la cuenta contable a varias facturas, en cualquier estado (también Pagadas).</summary>
    /// <remarks>Por lotes de 1000 (límite de parámetros de SQL Server). Cada cambio queda auditado.</remarks>
    public async Task<int> AsignarCuentaAsync(IEnumerable<int> ids, string? cuenta, int idUsuario, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return 0;
        cuenta = string.IsNullOrWhiteSpace(cuenta) ? null : cuenta.Trim();

        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        var total = 0;
        foreach (var lote in lista.Chunk(1000))
        {
            var ps = string.Join(",", lote.Select((_, i) => "@i" + i));
            var anteriores = new Dictionary<int, string?>();
            await using (var sel = cn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = $"SELECT Id, CuentaContable FROM dbo.FacturaProveedores WITH (UPDLOCK) WHERE Id IN ({ps})";
                for (var i = 0; i < lote.Length; i++) sel.Parameters.AddWithValue("@i" + i, lote[i]);
                await using var rd = await sel.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) anteriores[rd.GetInt32(0)] = rd.IsDBNull(1) ? null : rd.GetString(1).Trim();
            }
            await using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = $"UPDATE dbo.FacturaProveedores SET CuentaContable = @c WHERE Id IN ({ps})";
                for (var i = 0; i < lote.Length; i++) upd.Parameters.AddWithValue("@i" + i, lote[i]);
                upd.Parameters.AddWithValue("@c", SqlUtil.DbVal(cuenta));
                total += await upd.ExecuteNonQueryAsync(ct);
            }
            foreach (var (id, anterior) in anteriores.Where(a => a.Value != cuenta))
                await RegistrarCambiosAsync(cn, tx, id, idUsuario, new[] { new Cambio("Cuenta contable", anterior, cuenta) }, ct);
        }
        await tx.CommitAsync(ct);
        return total;
    }

    /// <summary>Quita la marca «Revisar» (la factura ya la ha revisado una persona).</summary>
    public async Task MarcarRevisadaAsync(int idFactura, int idUsuario, CancellationToken ct = default)
    {
        await using var cn = Conexion();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        string? motivo;
        await using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"UPDATE dbo.FacturaProveedores SET Revisar = 0
                                OUTPUT DELETED.MotivoRevision WHERE Id = @id AND Revisar = 1";
            cmd.Parameters.AddWithValue("@id", idFactura);
            var r = await cmd.ExecuteScalarAsync(ct);
            motivo = r is null or DBNull ? null : (string)r;
            if (r is null) { await tx.CommitAsync(ct); return; } // ya estaba revisada
        }
        await RegistrarCambiosAsync(cn, tx, idFactura, idUsuario,
            new[] { new Cambio("Revisión", "⚑ Pendiente" + (motivo is null ? "" : ": " + motivo), "Revisada") }, ct);
        await tx.CommitAsync(ct);
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
                        if (anterior is null && actual == EstadoFactura.Pagada)
                        {
                            // Alta directa como pagada (tarjeta): vuelve a Recibida para corregirla.
                            nuevo = EstadoFactura.Recibida;
                            textoHist = "Deshacer alta como pagada (→ Recibida)"
                                        + (string.IsNullOrWhiteSpace(comentario) ? "" : ". " + comentario.Trim());
                        }
                        else if (anterior is null) motivo = "no hay ningún cambio de estado que deshacer.";
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
                                        MotivoRechazo = CASE WHEN @n = 4 THEN @mot ELSE NULL END,
                                        Revisar = CASE WHEN @n = 2 THEN 0 ELSE Revisar END  -- validar = revisada
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
            Tarjeta = rd.Str("Tarjeta"),
            CuentaContable = rd.Str("CuentaContable"),
            CuentaDescripcion = rd.Str("CuentaDescripcion"),
            Estado = (EstadoFactura)rd.GetByte(rd.GetOrdinal("Estado")),
            FechaPago = Fecha("FechaPago"),
            MotivoRechazo = rd.Str("MotivoRechazo"),
            RutaPdf = rd.Str("RutaPdf") ?? "",
            RutaPdfOriginal = rd.Str("RutaPdfOriginal"),
            Observaciones = rd.Str("Observaciones"),
            FechaRegistro = rd.GetDateTime(rd.GetOrdinal("FechaRegistro")),
            Origen = (OrigenFactura)rd.GetByte(rd.GetOrdinal("Origen")),
            Revisar = rd.GetBoolean(rd.GetOrdinal("Revisar")),
            MotivoRevision = rd.Str("MotivoRevision"),
        };
    }
}
