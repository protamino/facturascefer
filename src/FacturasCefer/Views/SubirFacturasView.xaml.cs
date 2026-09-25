using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FacturasCefer.Dialogs;
using FacturasCefer.Models;
using FacturasCefer.Services;
using Microsoft.Web.WebView2.Core;

namespace FacturasCefer.Views;

/// <summary>
/// Subida de PDFs (arrastrar / Ctrl+V / Añadir), extracción con IA y revisión factura a factura
/// con el PDF a la izquierda y el formulario a la derecha.
/// </summary>
public partial class SubirFacturasView : UserControl
{
    private static readonly Brush FondoDudoso = new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xC4));

    private readonly Queue<PdfEnRevision> _cola = new();
    private PdfEnRevision? _pdf;
    private Proveedor? _proveedor;
    private string? _cifBuscado;
    private int _busquedaProveedor;
    private string? _rutaVisor;
    private int _seqPreview;
    private string? _ultimoResumen;
    private Task<bool>? _initVisor;

    private static string CarpetaTemp => Path.Combine(Path.GetTempPath(), "FacturasCefer", "cola");

    public SubirFacturasView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => _initVisor ??= InicializarVisorAsync();
        LimpiarTempAntiguos();
    }

    /// <summary>True si hay facturas sin guardar ni descartar (para avisar al cerrar).</summary>
    public bool HayPendientes => _cola.Count > 0 || (_pdf is not null && !_pdf.Terminado);

    // ------------------------------------------------------------------ Subida

    private void Zona_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Zona_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] rutas) AgregarArchivos(rutas);
        else
            MessageBox.Show(Window.GetWindow(this),
                "Solo se pueden soltar ficheros. Si arrastras un adjunto desde Outlook, guárdalo antes en una carpeta.",
                "Subir facturas", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+V con ficheros copiados (en un TextBox con texto en el portapapeles, se pega el texto normal).
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsFileDropList())
        {
            AgregarArchivos(Clipboard.GetFileDropList().Cast<string>());
            e.Handled = true;
        }
    }

    private void BtnAnadir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Añadir facturas",
            Filter = "Facturas PDF (*.pdf)|*.pdf",
            Multiselect = true,
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true) AgregarArchivos(dlg.FileNames);
    }

    private void AgregarArchivos(IEnumerable<string> rutas)
    {
        var rechazados = new List<string>();
        foreach (var ruta in rutas)
        {
            if (Directory.Exists(ruta) || !File.Exists(ruta)) { rechazados.Add(Path.GetFileName(ruta)); continue; }
            if (!EsPdf(ruta)) { rechazados.Add(Path.GetFileName(ruta)); continue; }
            try
            {
                // Copia local: el original puede estar en red, en uso o borrarse mientras se revisa.
                Directory.CreateDirectory(CarpetaTemp);
                var local = Path.Combine(CarpetaTemp, Guid.NewGuid().ToString("N") + ".pdf");
                File.Copy(ruta, local);
                _cola.Enqueue(new PdfEnRevision(local, Path.GetFileName(ruta)));
            }
            catch (Exception ex)
            {
                App.MostrarError(ex, Window.GetWindow(this));
            }
        }

        if (rechazados.Count > 0)
            MessageBox.Show(Window.GetWindow(this),
                "Solo se admiten ficheros PDF. No se han añadido:\n\n" + string.Join("\n", rechazados),
                "Subir facturas", MessageBoxButton.OK, MessageBoxImage.Warning);

        ActualizarCola();
        if (_pdf is null) _ = SiguientePdfAsync();
    }

    private static bool EsPdf(string ruta)
    {
        try
        {
            using var fs = File.OpenRead(ruta);
            var cab = new byte[5];
            return fs.Read(cab, 0, 5) == 5 && System.Text.Encoding.ASCII.GetString(cab) == "%PDF-";
        }
        catch
        {
            return false;
        }
    }

    private void ActualizarCola()
    {
        var partes = new List<string>();
        if (_cola.Count > 0) partes.Add($"{_cola.Count} PDF en cola.");
        if (_ultimoResumen is not null) partes.Add(_ultimoResumen);
        TxtCola.Text = string.Join("   ", partes);
        TxtCola.Visibility = partes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ------------------------------------------------------------------ Flujo de PDFs

    private async Task SiguientePdfAsync()
    {
        if (_pdf is not null) BorrarTemporales(_pdf);
        _pdf = _cola.Count > 0 ? _cola.Dequeue() : null;
        ActualizarCola();

        if (_pdf is null)
        {
            PanelRevision.Visibility = Visibility.Collapsed;
            TxtVacio.Visibility = Visibility.Visible;
            return;
        }

        PanelRevision.Visibility = Visibility.Visible;
        TxtVacio.Visibility = Visibility.Collapsed;
        TxtNombrePdf.Text = _pdf.NombreOriginal;

        try
        {
            _pdf.Paginas = PdfService.ContarPaginas(_pdf.RutaLocal);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"No se puede leer «{_pdf.NombreOriginal}» (¿PDF dañado o protegido con contraseña?).\n\n{ex.Message}",
                "Subir facturas", MessageBoxButton.OK, MessageBoxImage.Warning);
            await SiguientePdfAsync();
            return;
        }

        await MostrarEnVisorAsync(_pdf.RutaLocal);
        await ExtraerAsync();
    }

    private async Task ExtraerAsync()
    {
        var pdf = _pdf!;
        OcultarAviso();
        TxtPosicion.Text = "";
        TxtEstadoFactura.Text = "";
        SetOcupado(true, $"Leyendo «{pdf.NombreOriginal}» con IA…");

        List<FacturaExtraida>? facturas = null;
        string? error = null;
        try
        {
            facturas = await App.Extraccion.ExtraerAsync(pdf.RutaLocal);
        }
        catch (ReglaNegocioException ex)
        {
            error = ex.Message;
        }
        catch (Exception ex)
        {
            App.Log("ia-error.log", ex);
            error = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            SetOcupado(false);
        }

        if (!ReferenceEquals(pdf, _pdf)) return; // se descartó mientras la IA trabajaba

        pdf.Facturas.Clear();
        if (facturas is { Count: > 0 })
        {
            foreach (var f in facturas)
            {
                f.PaginaInicio = Math.Clamp(f.PaginaInicio, 1, pdf.Paginas);
                f.PaginaFin = Math.Clamp(f.PaginaFin, f.PaginaInicio, pdf.Paginas);
                pdf.Facturas.Add(new FacturaEnRevision(f, JsonSerializer.Serialize(f)));
            }
            if (facturas.Count > 1)
                MostrarAviso($"Se han detectado {facturas.Count} facturas en este PDF. Revisa y guarda cada una.", reintentar: false);
        }
        else
        {
            pdf.Facturas.Add(new FacturaEnRevision(new FacturaExtraida { PaginaInicio = 1, PaginaFin = pdf.Paginas }, null));
            MostrarAviso(error is not null
                    ? "No se ha podido leer con IA: " + error + "\nPuedes reintentar o rellenar los datos a mano."
                    : "La IA no ha encontrado ninguna factura en este PDF. Rellena los datos a mano o descártalo.",
                reintentar: error is not null);
        }

        pdf.Indice = 0;
        await MostrarFacturaAsync();
    }

    private async void BtnReintentarIA_Click(object sender, RoutedEventArgs e)
    {
        if (_pdf is null) return;
        if (_pdf.Facturas.Any(f => f.Estado == EstadoRevision.Guardada))
        {
            MessageBox.Show(Window.GetWindow(this), "Ya hay facturas guardadas de este PDF; no se puede volver a leer.",
                "Reintentar IA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await ExtraerAsync();
    }

    // ------------------------------------------------------------------ Mostrar factura

    private async Task MostrarFacturaAsync()
    {
        var pdf = _pdf!;
        var fr = pdf.Actual!;
        var ia = fr.Ia;

        TxtPosicion.Text = $"Factura {pdf.Indice + 1} de {pdf.Facturas.Count}";
        TxtEstadoFactura.Text = fr.Estado switch
        {
            EstadoRevision.Guardada => $"✔ Guardada (id {fr.IdFactura})",
            EstadoRevision.Descartada => "Descartada",
            _ => "Pendiente de revisar",
        };
        BtnAnterior.IsEnabled = pdf.Indice > 0;
        BtnSiguiente.IsEnabled = pdf.Indice < pdf.Facturas.Count - 1;

        TxtCif.Text = ia.ProveedorCif ?? "";
        TxtIban.Text = Validaciones.FormatearIban(ia.Iban);
        FormaPagoActual = FormaPagoIa(ia) ?? FormaPago.Transferencia;
        TxtNumero.Text = ia.NumeroFactura ?? "";
        DpFecha.SelectedDate = ParseFecha(ia.FechaFactura);
        DpVencimiento.SelectedDate = ParseFecha(ia.FechaVencimiento);
        TxtConcepto.Text = ia.Concepto ?? "";
        TxtBase.Text = Formato.Importe(ia.BaseImponible);
        TxtPorcIva.Text = Formato.Porcentaje(ia.PorcIva);
        TxtCuotaIva.Text = Formato.Importe(ia.CuotaIva);
        TxtPorcIrpf.Text = Formato.Porcentaje(ia.PorcIrpf);
        TxtCuotaIrpf.Text = Formato.Importe(ia.CuotaIrpf);
        TxtTotal.Text = Formato.Importe(ia.Total);
        TxtPagDesde.Text = ia.PaginaInicio.ToString();
        TxtPagHasta.Text = ia.PaginaFin.ToString();
        TxtTotalPaginas.Text = $"(el PDF tiene {pdf.Paginas})";
        TxtObservaciones.Text = fr.Observaciones ?? "";
        TxtError.Text = "";

        MarcarDudosos(ia.CamposDudosos);

        var editable = fr.Estado == EstadoRevision.Pendiente;
        Formulario.IsEnabled = editable;
        BtnGuardar.IsEnabled = editable;
        BtnDescartar.IsEnabled = editable;

        _cifBuscado = null;
        await BuscarProveedorAsync();
        ActualizarCuadre();
        await MostrarPaginasEnVisorAsync();
    }

    /// <summary>Vuelca el formulario en la factura actual (para no perder cambios al navegar).</summary>
    private void LeerFormulario()
    {
        var fr = _pdf?.Actual;
        if (fr is null || fr.Estado != EstadoRevision.Pendiente) return;
        var ia = fr.Ia;
        ia.ProveedorCif = TxtCif.Text.Trim();
        ia.Iban = Validaciones.NormalizarIban(TxtIban.Text);
        ia.FormaPago = FormaPagoActual == FormaPago.Domiciliacion ? "domiciliacion" : "transferencia";
        ia.NumeroFactura = TxtNumero.Text.Trim();
        ia.FechaFactura = DpFecha.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
        ia.FechaVencimiento = DpVencimiento.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
        ia.Concepto = TxtConcepto.Text.Trim();
        if (Formato.TryImporte(TxtBase.Text, out var v)) ia.BaseImponible = v;
        if (Formato.TryImporte(TxtPorcIva.Text, out v)) ia.PorcIva = v;
        if (Formato.TryImporte(TxtCuotaIva.Text, out v)) ia.CuotaIva = v;
        if (Formato.TryImporte(TxtPorcIrpf.Text, out v)) ia.PorcIrpf = v;
        if (Formato.TryImporte(TxtCuotaIrpf.Text, out v)) ia.CuotaIrpf = v;
        if (Formato.TryImporte(TxtTotal.Text, out v)) ia.Total = v;
        if (LeerPaginas(out var d, out var h)) { ia.PaginaInicio = d; ia.PaginaFin = h; }
        fr.Observaciones = TxtObservaciones.Text;
    }

    private void MarcarDudosos(IEnumerable<string> campos)
    {
        var mapa = new Dictionary<string, Control>
        {
            ["proveedor_cif"] = TxtCif, ["iban"] = TxtIban, ["numero_factura"] = TxtNumero,
            ["fecha_factura"] = DpFecha, ["fecha_vencimiento"] = DpVencimiento, ["concepto"] = TxtConcepto,
            ["base_imponible"] = TxtBase, ["porc_iva"] = TxtPorcIva, ["cuota_iva"] = TxtCuotaIva,
            ["porc_irpf"] = TxtPorcIrpf, ["cuota_irpf"] = TxtCuotaIrpf, ["total"] = TxtTotal,
        };
        foreach (var c in mapa.Values)
        {
            c.ClearValue(BackgroundProperty);
            c.ClearValue(ToolTipProperty);
        }
        foreach (var campo in campos)
        {
            if (!mapa.TryGetValue(campo, out var c)) continue;
            c.Background = FondoDudoso;
            c.ToolTip = "La IA no ha encontrado este dato o no está segura: revísalo.";
        }
    }

    private void BtnAnterior_Click(object sender, RoutedEventArgs e) => _ = IrAAsync(_pdf!.Indice - 1);
    private void BtnSiguiente_Click(object sender, RoutedEventArgs e) => _ = IrAAsync(_pdf!.Indice + 1);

    private async Task IrAAsync(int indice)
    {
        if (_pdf is null || indice < 0 || indice >= _pdf.Facturas.Count) return;
        LeerFormulario();
        _pdf.Indice = indice;
        await MostrarFacturaAsync();
    }

    // ------------------------------------------------------------------ Proveedor e IBAN

    private void TxtCif_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Validaciones.NormalizarCif(TxtCif.Text) != _cifBuscado) _ = BuscarProveedorAsync();
    }

    private void BtnBuscarProveedor_Click(object sender, RoutedEventArgs e) => _ = BuscarProveedorAsync();

    private async Task BuscarProveedorAsync()
    {
        var busqueda = ++_busquedaProveedor;
        var cif = Validaciones.NormalizarCif(TxtCif.Text);
        _cifBuscado = cif;
        Proveedor? p = null;

        if (cif.Length > 0)
        {
            try
            {
                p = await App.Proveedores.ObtenerPorCifAsync(cif);
            }
            catch (Exception ex)
            {
                App.MostrarError(ex, Window.GetWindow(this));
            }
        }
        if (busqueda != _busquedaProveedor) return;

        _proveedor = p;
        var razonIa = _pdf?.Actual?.Ia.ProveedorRazonSocial;
        if (p is not null)
        {
            PanelProveedor.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9));
            TxtProveedor.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
            TxtProveedor.Text = $"✔ {p.RazonSocial} ({p.CIF})" + (p.Baja ? " — DADO DE BAJA" : "");
            BtnAltaProveedor.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanelProveedor.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xE5));
            TxtProveedor.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x53, 0x00));
            TxtProveedor.Text = cif.Length == 0
                ? "Indica el CIF del proveedor" + (string.IsNullOrWhiteSpace(razonIa) ? "." : $" ({razonIa}).")
                : $"Proveedor no registrado: {(string.IsNullOrWhiteSpace(razonIa) ? cif : $"{razonIa} ({cif})")}.";
            BtnAltaProveedor.Visibility = Visibility.Visible;
        }
        if (p is not null) FormaPagoActual = p.FormaPago;
        ActualizarAvisoFormaPago();
        ActualizarPanelIban();
    }

    private void BtnAltaProveedor_Click(object sender, RoutedEventArgs e)
    {
        var fr = _pdf?.Actual;
        if (fr is null) return;
        LeerFormulario();
        var ia = fr.Ia;
        var iban = Validaciones.NormalizarIban(TxtIban.Text);
        var nuevo = new Proveedor
        {
            RazonSocial = ia.ProveedorRazonSocial ?? "",
            CIF = Validaciones.NormalizarCif(TxtCif.Text),
            Direccion = ia.ProveedorDireccion,
            CP = ia.ProveedorCp,
            Poblacion = ia.ProveedorPoblacion,
            Provincia = ia.ProveedorProvincia,
            FormaPago = FormaPagoActual,
            // En domiciliadas el IBAN de la factura es la cuenta de cargo de CEFER: no es del proveedor.
            IBAN = FormaPagoActual == FormaPago.Transferencia && Validaciones.IbanValido(iban) ? iban : null,
            Email = ia.ProveedorEmail,
            Telefono = ia.ProveedorTelefono,
        };
        var dlg = new ProveedorDialog(nuevo, esNuevo: true,
            aviso: "Proveedor nuevo detectado en la factura. Revisa los datos antes de darlo de alta.")
        { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        TxtCif.Text = dlg.Resultado!.CIF;
        _ = BuscarProveedorAsync();
    }

    private void TxtIban_TextChanged(object sender, TextChangedEventArgs e) => ActualizarPanelIban();

    private FormaPago FormaPagoActual
    {
        get => CmbFormaPago.SelectedIndex == 1 ? FormaPago.Domiciliacion : FormaPago.Transferencia;
        set => CmbFormaPago.SelectedIndex = value == FormaPago.Domiciliacion ? 1 : 0;
    }

    private static FormaPago? FormaPagoIa(FacturaExtraida ia) => ia.FormaPago switch
    {
        "domiciliacion" => FormaPago.Domiciliacion,
        "transferencia" => FormaPago.Transferencia,
        _ => null,
    };

    private void CmbFormaPago_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LblIban is null) return; // durante InitializeComponent
        LblIban.Text = FormaPagoActual == FormaPago.Domiciliacion
            ? "Cuenta de cargo (cuenta de CEFER donde se cobra el recibo)"
            : "IBAN del proveedor";
        ActualizarAvisoFormaPago();
        ActualizarPanelIban();
    }

    /// <summary>Avisa si la forma de pago que indica la factura no es la que tiene el proveedor.</summary>
    private void ActualizarAvisoFormaPago()
    {
        var ia = _pdf?.Actual?.Ia;
        var enFactura = ia is null ? null : FormaPagoIa(ia);
        if (_proveedor is null || enFactura is null || enFactura == _proveedor.FormaPago)
        {
            TxtAvisoFormaPago.Visibility = Visibility.Collapsed;
            return;
        }
        TxtAvisoFormaPago.Text =
            $"⚠ La factura indica {Textos.FormaPago(enFactura.Value).ToLower()}, pero el proveedor está dado de alta " +
            $"con {Textos.FormaPago(_proveedor.FormaPago).ToLower()}. Revisa la ficha del proveedor si ha cambiado.";
        TxtAvisoFormaPago.Visibility = Visibility.Visible;
    }

    private void ActualizarPanelIban()
    {
        var ibanFactura = Validaciones.NormalizarIban(TxtIban.Text);
        var ibanProveedor = Validaciones.NormalizarIban(_proveedor?.IBAN);

        if (_proveedor is null || ibanFactura.Length == 0 || ibanFactura == ibanProveedor ||
            FormaPagoActual == FormaPago.Domiciliacion)
        {
            PanelIban.Visibility = Visibility.Collapsed;
            ChkActualizarIban.IsChecked = false;
            return;
        }

        PanelIban.Visibility = Visibility.Visible;
        if (ibanProveedor.Length == 0)
        {
            PanelIban.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF4, 0xFB));
            TxtIbanAviso.Foreground = Brushes.Black;
            TxtIbanAviso.Text = "El proveedor no tiene IBAN registrado.";
            ChkActualizarIban.Content = "Guardar este IBAN en la ficha del proveedor";
            ChkActualizarIban.IsChecked = Validaciones.IbanValido(ibanFactura);
        }
        else
        {
            PanelIban.Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEA));
            TxtIbanAviso.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x1A, 0x1A));
            TxtIbanAviso.Text =
                "⚠ El IBAN de la factura NO coincide con el del proveedor.\n" +
                $"Factura:     {Validaciones.FormatearIban(ibanFactura)}\n" +
                $"Proveedor: {Validaciones.FormatearIban(ibanProveedor)}\n" +
                "Puede ser un intento de fraude por cambio de cuenta: confírmalo con el proveedor por un canal conocido antes de pagar.";
            ChkActualizarIban.Content = "Actualizar el IBAN del proveedor con el de esta factura";
            ChkActualizarIban.IsChecked = false;
        }
    }

    // ------------------------------------------------------------------ Importes y páginas

    private void Importe_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && Formato.TryImporte(tb.Text, out var v) && v is not null) tb.Text = Formato.Importe(v);
        ActualizarCuadre();
    }

    private void ActualizarCuadre()
    {
        TxtCuadre.Text = "";
        if (!Formato.TryImporte(TxtBase.Text, out var b) || !Formato.TryImporte(TxtTotal.Text, out var t) || b is null || t is null) return;
        Formato.TryImporte(TxtCuotaIva.Text, out var iva);
        Formato.TryImporte(TxtCuotaIrpf.Text, out var irpf);
        var calculado = b.Value + (iva ?? 0) - (irpf ?? 0);
        if (Math.Abs(calculado - t.Value) <= 0.02m)
        {
            TxtCuadre.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
            TxtCuadre.Text = "✔ Base + IVA − IRPF cuadra con el total";
        }
        else
        {
            TxtCuadre.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x53, 0x00));
            TxtCuadre.Text = $"⚠ Base + IVA − IRPF = {Formato.Importe(calculado)} (no cuadra)";
        }
    }

    private void Paginas_LostFocus(object sender, RoutedEventArgs e) => _ = MostrarPaginasEnVisorAsync();

    private bool LeerPaginas(out int desde, out int hasta)
    {
        var n = _pdf?.Paginas ?? 0;
        return int.TryParse(TxtPagDesde.Text, out desde) & int.TryParse(TxtPagHasta.Text, out hasta)
               && desde >= 1 && hasta <= n && desde <= hasta;
    }

    // ------------------------------------------------------------------ Visor

    private async Task<bool> InicializarVisorAsync()
    {
        try
        {
            var datos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FacturasCefer", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, datos);
            await Visor.EnsureCoreWebView2Async(env);
            return true;
        }
        catch (Exception ex)
        {
            App.Log("webview2-error.log", ex);
            Visor.Visibility = Visibility.Collapsed;
            TxtVisorError.Text = "No se puede mostrar el PDF aquí (falta el componente Microsoft Edge WebView2).\n" +
                                 "Usa «Abrir en visor externo».";
            TxtVisorError.Visibility = Visibility.Visible;
            return false;
        }
    }

    /// <summary>Muestra en el visor solo las páginas de la factura actual (lo que se guardará).</summary>
    private async Task MostrarPaginasEnVisorAsync()
    {
        if (_pdf is null) return;
        if (!LeerPaginas(out var d, out var h) || (d == 1 && h == _pdf.Paginas))
        {
            await MostrarEnVisorAsync(_pdf.RutaLocal);
            return;
        }
        try
        {
            var prev = Path.Combine(CarpetaTemp, $"{Path.GetFileNameWithoutExtension(_pdf.RutaLocal)}-p{++_seqPreview}.pdf");
            PdfService.ExtraerPaginas(_pdf.RutaLocal, d, h, prev);
            await MostrarEnVisorAsync(prev);
        }
        catch (Exception ex)
        {
            App.Log("preview-error.log", ex);
            await MostrarEnVisorAsync(_pdf.RutaLocal);
        }
    }

    private async Task MostrarEnVisorAsync(string ruta)
    {
        _rutaVisor = ruta;
        _initVisor ??= InicializarVisorAsync();
        if (await _initVisor && Visor.CoreWebView2 is not null)
            Visor.CoreWebView2.Navigate(new Uri(ruta).AbsoluteUri);
    }

    private void BtnAbrirExterno_Click(object sender, RoutedEventArgs e)
    {
        if (_rutaVisor is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(_rutaVisor) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, Window.GetWindow(this));
        }
    }

    // ------------------------------------------------------------------ Guardar / descartar

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var pdf = _pdf;
        var fr = pdf?.Actual;
        if (pdf is null || fr is null || fr.Estado != EstadoRevision.Pendiente) return;
        var owner = Window.GetWindow(this);
        TxtError.Text = "";
        LeerFormulario();

        // --- Validaciones que bloquean
        var proveedor = _proveedor;
        if (proveedor is null) { Error("Falta el proveedor: búscalo por su CIF o dalo de alta.", TxtCif); return; }
        var numero = TxtNumero.Text.Trim();
        if (numero.Length == 0) { Error("El nº de factura es obligatorio.", TxtNumero); return; }
        if (DpFecha.SelectedDate is not DateTime fecha) { Error("La fecha de factura es obligatoria.", DpFecha); return; }

        if (!Formato.TryImporte(TxtBase.Text, out var baseImp)) { Error("La base imponible no es un importe válido.", TxtBase); return; }
        if (!Formato.TryImporte(TxtPorcIva.Text, out var porcIva)) { Error("El % de IVA no es válido.", TxtPorcIva); return; }
        if (!Formato.TryImporte(TxtCuotaIva.Text, out var cuotaIva)) { Error("El IVA no es un importe válido.", TxtCuotaIva); return; }
        if (!Formato.TryImporte(TxtPorcIrpf.Text, out var porcIrpf)) { Error("El % de IRPF no es válido.", TxtPorcIrpf); return; }
        if (!Formato.TryImporte(TxtCuotaIrpf.Text, out var cuotaIrpf)) { Error("La retención IRPF no es un importe válido.", TxtCuotaIrpf); return; }
        if (!Formato.TryImporte(TxtTotal.Text, out var total) || total is null) { Error("El total es obligatorio y debe ser un importe válido.", TxtTotal); return; }
        if (!LeerPaginas(out var desde, out var hasta)) { Error($"Rango de páginas no válido (1 a {pdf.Paginas}).", TxtPagDesde); return; }

        // --- Avisos que se pueden confirmar
        var iban = Validaciones.NormalizarIban(TxtIban.Text);
        if (proveedor.Baja && !Confirmar($"El proveedor {proveedor.RazonSocial} está dado de baja.\n\n¿Guardar la factura igualmente?"))
            return;
        if (iban.Length > 0 && !Validaciones.IbanValido(iban) &&
            !Confirmar("El IBAN de la factura no es válido (dígitos de control incorrectos).\n\n¿Guardar igualmente?"))
            return;
        if (baseImp is not null)
        {
            var calculado = baseImp.Value + (cuotaIva ?? 0) - (cuotaIrpf ?? 0);
            if (Math.Abs(calculado - total.Value) > 0.02m &&
                !Confirmar($"Base + IVA − IRPF = {Formato.Importe(calculado)} €, pero el total es {Formato.Importe(total)} €.\n\n¿Guardar igualmente?"))
                return;
        }

        var actualizarIban = PanelIban.Visibility == Visibility.Visible && ChkActualizarIban.IsChecked == true;
        var ibanProveedor = Validaciones.NormalizarIban(proveedor.IBAN);
        if (actualizarIban && ibanProveedor.Length > 0 && MessageBox.Show(owner,
                "Vas a CAMBIAR el IBAN del proveedor:\n\n" +
                $"  Actual: {Validaciones.FormatearIban(ibanProveedor)}\n" +
                $"  Nuevo:  {Validaciones.FormatearIban(iban)}\n\n" +
                "Los cambios de cuenta bancaria son una vía habitual de fraude. " +
                "Confírmalo con el proveedor por un canal conocido antes de aceptar.\n\n¿Confirmar el cambio?",
                "Cambio de IBAN", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        var factura = new FacturaProveedor
        {
            IdProveedor = proveedor.Id,
            NumeroFactura = numero,
            Concepto = TxtConcepto.Text.Trim(),
            FechaFactura = fecha,
            FechaVencimiento = DpVencimiento.SelectedDate,
            BaseImponible = baseImp,
            PorcIVA = porcIva,
            CuotaIVA = cuotaIva,
            PorcIRPF = porcIrpf,
            CuotaIRPF = cuotaIrpf,
            Total = total.Value,
            IBAN = iban.Length > 0 ? iban : null,
            FormaPago = FormaPagoActual,
            NombreOriginal = pdf.NombreOriginal,
            JsonExtraccionIA = fr.JsonIa,
            Observaciones = TxtObservaciones.Text,
        };

        SetOcupado(true, "Guardando la factura…");
        try
        {
            if (await App.Facturas.ExisteAsync(proveedor.Id, numero))
            {
                Error($"Ya existe la factura nº {numero} de {proveedor.RazonSocial}. Si es una copia, descártala.", TxtNumero);
                return;
            }
            fr.IdFactura = await App.Facturas.GuardarAsync(factura, pdf.RutaLocal, desde, hasta, pdf.Original, App.Usuario.IdUsuario);
            fr.Estado = EstadoRevision.Guardada;
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message, TxtNumero);
            return;
        }
        catch (Exception ex)
        {
            App.MostrarError(ex, owner);
            return;
        }
        finally
        {
            SetOcupado(false);
        }

        if (actualizarIban)
        {
            try
            {
                var p = proveedor.Clone();
                p.IBAN = iban;
                await App.Proveedores.ActualizarAsync(p, App.Usuario.IdUsuario, fr.IdFactura);
            }
            catch (Exception ex)
            {
                App.Log("app-error.log", ex);
                MessageBox.Show(owner, "La factura se ha guardado, pero no se ha podido actualizar el IBAN del proveedor:\n\n" + ex.Message,
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        await AvanzarAsync();
    }

    private async void BtnDescartar_Click(object sender, RoutedEventArgs e)
    {
        var fr = _pdf?.Actual;
        if (fr is null || fr.Estado != EstadoRevision.Pendiente) return;
        if (!Confirmar("¿Descartar esta factura? No se guardará.")) return;
        fr.Estado = EstadoRevision.Descartada;
        await AvanzarAsync();
    }

    private async void BtnDescartarPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_pdf is null) return;
        var pendientes = _pdf.Facturas.Count(f => f.Estado == EstadoRevision.Pendiente);
        if (pendientes > 0 && !Confirmar($"¿Descartar las {pendientes} factura(s) pendientes de este PDF? No se guardarán."))
            return;
        foreach (var f in _pdf.Facturas.Where(f => f.Estado == EstadoRevision.Pendiente))
            f.Estado = EstadoRevision.Descartada;
        await AvanzarAsync();
    }

    /// <summary>Pasa a la siguiente factura pendiente del PDF o, si no quedan, al siguiente PDF de la cola.</summary>
    private async Task AvanzarAsync()
    {
        var pdf = _pdf!;
        var n = pdf.Facturas.Count;
        for (var k = 1; k <= n; k++)
        {
            var i = (pdf.Indice + k) % n;
            if (pdf.Facturas[i].Estado != EstadoRevision.Pendiente) continue;
            pdf.Indice = i;
            await MostrarFacturaAsync();
            return;
        }

        var guardadas = pdf.Facturas.Count(f => f.Estado == EstadoRevision.Guardada);
        var descartadas = pdf.Facturas.Count(f => f.Estado == EstadoRevision.Descartada);
        _ultimoResumen = $"Último PDF ({pdf.NombreOriginal}): {guardadas} guardada(s), {descartadas} descartada(s).";
        await SiguientePdfAsync();
    }

    // ------------------------------------------------------------------ Utilidades

    private void SetOcupado(bool ocupado, string? texto = null)
    {
        PanelIA.Visibility = ocupado ? Visibility.Visible : Visibility.Collapsed;
        if (texto is not null) TxtIA.Text = texto;
        var fr = _pdf?.Actual;
        var editable = !ocupado && fr?.Estado == EstadoRevision.Pendiente;
        Formulario.IsEnabled = editable;
        BtnGuardar.IsEnabled = editable;
        BtnDescartar.IsEnabled = editable;
        BtnDescartarPdf.IsEnabled = !ocupado;
        BtnReintentarIA.IsEnabled = !ocupado;
        BtnAnterior.IsEnabled = !ocupado && _pdf is not null && _pdf.Indice > 0;
        BtnSiguiente.IsEnabled = !ocupado && _pdf is not null && _pdf.Indice < _pdf.Facturas.Count - 1;
    }

    private void MostrarAviso(string texto, bool reintentar)
    {
        TxtAviso.Text = texto;
        BtnReintentarIA.Visibility = reintentar ? Visibility.Visible : Visibility.Collapsed;
        PanelAviso.Visibility = Visibility.Visible;
    }

    private void OcultarAviso() => PanelAviso.Visibility = Visibility.Collapsed;

    private void Error(string msg, Control foco)
    {
        TxtError.Text = msg;
        foco.Focus();
    }

    private bool Confirmar(string msg) =>
        MessageBox.Show(Window.GetWindow(this), msg, "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning)
        == MessageBoxResult.Yes;

    private static DateTime? ParseFecha(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static void BorrarTemporales(PdfEnRevision pdf)
    {
        var baseName = Path.GetFileNameWithoutExtension(pdf.RutaLocal);
        try
        {
            foreach (var f in Directory.GetFiles(CarpetaTemp, baseName + "*.pdf"))
            {
                try { File.Delete(f); } catch { /* el visor puede tenerlo abierto; se limpia al arrancar */ }
            }
        }
        catch { /* ignore */ }
    }

    private static void LimpiarTempAntiguos()
    {
        try
        {
            if (!Directory.Exists(CarpetaTemp)) return;
            foreach (var f in Directory.GetFiles(CarpetaTemp))
            {
                try
                {
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1)) File.Delete(f);
                }
                catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }
    }
}
