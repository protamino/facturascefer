using System.Globalization;
using System.Text.Json;
using FacturasCefer.Config;
using FacturasCefer.Models;

namespace FacturasCefer.Services;

/// <summary>Resultado de importar un PDF.</summary>
public sealed class ResultadoImportacion
{
    public const string Procesada = "procesada";
    public const string Duplicada = "duplicada";
    public const string SinFactura = "sin_factura";
    public const string Error = "error";

    /// <summary>procesada | duplicada | sin_factura | error</summary>
    public string Resultado { get; set; } = Error;
    public List<int> IdsFacturas { get; } = new();
    public List<string> Lineas { get; } = new();
    public string Mensaje => string.Join("\n", Lineas);
}

/// <summary>
/// Importación automática de un PDF (sin interfaz): aplica las mismas reglas que la pantalla de revisión
/// de la app y marca para revisión todo lo que una persona debería mirar.
/// <list type="bullet">
/// <item>Proveedor por CIF; si no existe se da de alta automáticamente (PendienteRevision).</item>
/// <item>Duplicados (mismo proveedor + nº) se omiten.</item>
/// <item>IBAN distinto al del proveedor: NO se cambia el proveedor; la factura queda «Revisar».</item>
/// <item>Cuenta de gasto y forma de pago, las del proveedor; tarjeta → Pagada (como en la app).</item>
/// </list>
/// Con <c>simular = true</c> no se escribe nada (ni proveedores, ni facturas, ni ficheros).
/// </summary>
public sealed class ImportacionAutomaticaService
{
    private readonly AppConfig _cfg;
    private readonly ExtraccionService _ia;
    private readonly ProveedorService _proveedores;
    private readonly FacturaService _facturas;

    public ImportacionAutomaticaService(AppConfig cfg, ExtraccionService ia, ProveedorService proveedores, FacturaService facturas)
    {
        _cfg = cfg;
        _ia = ia;
        _proveedores = proveedores;
        _facturas = facturas;
    }

    public async Task<ResultadoImportacion> ProcesarAsync(string rutaPdf, string nombreOriginal, string? remitente,
        int idUsuario, bool simular, CancellationToken ct = default)
    {
        var r = new ResultadoImportacion();
        var prefijo = simular ? "[SIMULACIÓN] " : "";

        List<FacturaExtraida> facturas;
        int paginas;
        try
        {
            paginas = PdfService.ContarPaginas(rutaPdf);
            facturas = await _ia.ExtraerAsync(rutaPdf, ct);
        }
        catch (Exception ex)
        {
            r.Resultado = ResultadoImportacion.Error;
            r.Lineas.Add($"{prefijo}No se ha podido leer el PDF: {ex.Message}");
            return r;
        }

        if (facturas.Count == 0)
        {
            r.Resultado = ResultadoImportacion.SinFactura;
            r.Lineas.Add($"{prefijo}La IA no ha encontrado ninguna factura en el PDF.");
            return r;
        }

        var original = new OriginalGuardado();
        int guardadas = 0, duplicadas = 0, errores = 0;
        for (var i = 0; i < facturas.Count; i++)
        {
            var ia = facturas[i];
            var etiqueta = facturas.Count > 1 ? $"Factura {i + 1}/{facturas.Count}" : "Factura";
            try
            {
                var res = await ProcesarFacturaAsync(ia, rutaPdf, paginas, nombreOriginal, remitente, original, idUsuario, simular, ct);
                r.Lineas.Add($"{prefijo}{etiqueta}: {res.Texto}");
                switch (res.Tipo)
                {
                    case TipoRes.Guardada: guardadas++; if (res.IdFactura is { } id) r.IdsFacturas.Add(id); break;
                    case TipoRes.Duplicada: duplicadas++; break;
                    default: errores++; break;
                }
            }
            catch (Exception ex)
            {
                errores++;
                r.Lineas.Add($"{prefijo}{etiqueta}: error inesperado: {ex.Message}");
            }
        }

        r.Resultado = guardadas > 0 ? ResultadoImportacion.Procesada
            : duplicadas > 0 && errores == 0 ? ResultadoImportacion.Duplicada
            : ResultadoImportacion.Error;
        return r;
    }

    private enum TipoRes { Guardada, Duplicada, Error }

    private sealed record ResFactura(TipoRes Tipo, string Texto, int? IdFactura = null);

    private async Task<ResFactura> ProcesarFacturaAsync(FacturaExtraida ia, string rutaPdf, int paginas, string nombreOriginal,
        string? remitente, OriginalGuardado original, int idUsuario, bool simular, CancellationToken ct)
    {
        var motivos = new List<string>();

        // ---------- Datos obligatorios
        var numero = (ia.NumeroFactura ?? "").Trim();
        var cif = Validaciones.NormalizarCif(ia.ProveedorCif);
        if (cif.Length == 0) return new(TipoRes.Error, "no se ha encontrado el CIF del proveedor; hay que subirla a mano.");
        if (numero.Length == 0) return new(TipoRes.Error, $"no se ha encontrado el nº de factura ({ia.ProveedorRazonSocial}).");
        if (!DateTime.TryParseExact(ia.FechaFactura, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return new(TipoRes.Error, $"no se ha encontrado la fecha de la factura {numero}.");
        if (ia.Total is not { } total) return new(TipoRes.Error, $"no se ha encontrado el total de la factura {numero}.");

        // ---------- Proveedor (por CIF; alta automática si no existe)
        var formaIa = ia.FormaPago switch
        {
            "domiciliacion" => FormaPago.Domiciliacion,
            "tarjeta" => FormaPago.Tarjeta,
            "transferencia" => FormaPago.Transferencia,
            _ => (FormaPago?)null,
        };
        var ibanFactura = Validaciones.NormalizarIban(ia.Iban);
        var proveedor = await _proveedores.ObtenerPorCifAsync(cif, ct);
        var nuevo = proveedor is null;
        if (proveedor is null)
        {
            var fp = formaIa ?? FormaPago.Transferencia;
            proveedor = new Proveedor
            {
                RazonSocial = string.IsNullOrWhiteSpace(ia.ProveedorRazonSocial) ? $"(sin nombre) {cif}" : ia.ProveedorRazonSocial.Trim(),
                CIF = cif,
                Direccion = ia.ProveedorDireccion,
                CP = ia.ProveedorCp,
                Poblacion = ia.ProveedorPoblacion,
                Provincia = ia.ProveedorProvincia,
                Email = ia.ProveedorEmail ?? remitente,
                Telefono = ia.ProveedorTelefono,
                FormaPago = fp,
                // En domiciliadas el IBAN es la cuenta de cargo de CEFER; en tarjeta no hay IBAN.
                IBAN = fp == FormaPago.Transferencia && Validaciones.IbanValido(ibanFactura) ? ibanFactura : null,
                Tarjeta = fp == FormaPago.Tarjeta ? Validaciones.EnmascararTarjeta(ia.Tarjeta) : null,
                PendienteRevision = true,
                Observaciones = $"Alta automática desde factura recibida por correo ({remitente}). Revisar los datos.",
            };
            if (!simular) proveedor.Id = await _proveedores.CrearAsync(proveedor, idUsuario, ct);
            motivos.Add("proveedor dado de alta automáticamente");
        }
        else
        {
            if (proveedor.Baja) motivos.Add("el proveedor está dado de baja");
            if (formaIa is { } f && f != proveedor.FormaPago)
                motivos.Add($"la factura indica {Textos.FormaPago(f).ToLower()} y el proveedor tiene {Textos.FormaPago(proveedor.FormaPago).ToLower()}");
        }

        var formaPago = proveedor.FormaPago;

        // ---------- Duplicado
        if (!nuevo && await _facturas.ExisteAsync(proveedor.Id, numero, ct))
            return new(TipoRes.Duplicada, $"ya existe la factura nº {numero} de {proveedor.RazonSocial}; se omite.");

        // ---------- Antifraude IBAN (solo transferencias): nunca se cambia el proveedor automáticamente
        var ibanProveedor = Validaciones.NormalizarIban(proveedor.IBAN);
        if (formaPago == FormaPago.Transferencia && ibanFactura.Length > 0)
        {
            if (!Validaciones.IbanValido(ibanFactura)) motivos.Add("el IBAN de la factura no es válido");
            else if (ibanProveedor.Length > 0 && ibanFactura != ibanProveedor)
                motivos.Add($"⚠ IBAN DISTINTO al del proveedor (factura {Validaciones.FormatearIban(ibanFactura)}): posible fraude, confirmar antes de pagar");
            else if (!nuevo && ibanProveedor.Length == 0)
                motivos.Add("el proveedor no tenía IBAN; el de la factura no se ha guardado en su ficha");
        }

        // ---------- Importes y datos dudosos
        var lineas = Desglose.DesdeIa(ia);
        var calculado = Desglose.TotalCalculado(lineas, ia.CuotaIrpf);
        if (Math.Abs(calculado - total) > 0.02m)
            motivos.Add($"bases + IVA + RE − IRPF = {Formato.Importe(calculado)} no cuadra con el total {Formato.Importe(total)}");
        if (ia.CamposDudosos.Count > 0) motivos.Add("datos dudosos: " + string.Join(", ", ia.CamposDudosos));
        if (proveedor.CuentaContable is null) motivos.Add("sin cuenta contable de gasto");

        var desde = Math.Clamp(ia.PaginaInicio, 1, paginas);
        var hasta = Math.Clamp(ia.PaginaFin, desde, paginas);
        DateTime? vto = DateTime.TryParseExact(ia.FechaVencimiento, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

        var factura = new FacturaProveedor
        {
            IdProveedor = proveedor.Id,
            NumeroFactura = numero,
            Concepto = ia.Concepto,
            FechaFactura = fecha,
            FechaVencimiento = vto,
            Impuestos = lineas,
            PorcIRPF = ia.PorcIrpf,
            CuotaIRPF = ia.CuotaIrpf,
            Total = total,
            IBAN = formaPago != FormaPago.Tarjeta && ibanFactura.Length > 0 ? ibanFactura : null,
            FormaPago = formaPago,
            Tarjeta = formaPago == FormaPago.Tarjeta ? Validaciones.EnmascararTarjeta(string.IsNullOrWhiteSpace(ia.Tarjeta) ? proveedor.Tarjeta : ia.Tarjeta) : null,
            CuentaContable = proveedor.CuentaContable,
            Estado = formaPago == FormaPago.Tarjeta ? EstadoFactura.Pagada : EstadoFactura.Recibida,
            FechaPago = formaPago == FormaPago.Tarjeta ? fecha : null,
            NombreOriginal = nombreOriginal,
            JsonExtraccionIA = JsonSerializer.Serialize(ia),
            Origen = OrigenFactura.Correo,
            Revisar = motivos.Count > 0,
            MotivoRevision = motivos.Count > 0 ? string.Join("; ", motivos) : null,
            Observaciones = string.IsNullOrWhiteSpace(remitente) ? null : $"Recibida por correo de {remitente}",
        };

        var resumen = $"{proveedor.RazonSocial} nº {numero} ({fecha:dd/MM/yyyy}, {Formato.Importe(total)} €)" +
                      (motivos.Count > 0 ? $" — revisar: {factura.MotivoRevision}" : "");
        if (simular) return new(TipoRes.Guardada, "se guardaría " + resumen);

        var idFactura = await _facturas.GuardarAsync(factura, rutaPdf, desde, hasta, original, idUsuario, ct);
        return new(TipoRes.Guardada, "guardada " + resumen, idFactura);
    }
}
