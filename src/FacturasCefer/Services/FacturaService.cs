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

/// <summary>Alta de facturas: copia de PDFs a la carpeta de red + INSERT en FacturaProveedores e histórico.</summary>
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
                 PorcIRPF, CuotaIRPF, Total, IBAN, Estado, RutaPdf, RutaPdfOriginal, PaginaInicio, PaginaFin,
                 NombreOriginal, JsonExtraccionIA, Observaciones, IdUsuarioRegistro)
                OUTPUT INSERTED.Id
                VALUES (@prov, @num, @conc, @fec, @vto, @base, @piva, @civa, @pirpf, @cirpf, @total, @iban, @estado,
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
}
