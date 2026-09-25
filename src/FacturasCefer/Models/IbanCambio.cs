namespace FacturasCefer.Models;

/// <summary>Fila de dbo.ProveedorIbanHistorico.</summary>
public sealed record IbanCambio(DateTime Fecha, string? IbanAnterior, string? IbanNuevo, int IdUsuario, int? IdFacturaOrigen)
{
    public string AnteriorFormateado => Services.Validaciones.FormatearIban(IbanAnterior);
    public string NuevoFormateado => Services.Validaciones.FormatearIban(IbanNuevo);
}
