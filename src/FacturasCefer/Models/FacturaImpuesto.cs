using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FacturasCefer.Models;

/// <summary>
/// Línea de impuesto de una factura (dbo.FacturaImpuesto): una por tipo de IVA / recargo de equivalencia,
/// y opcionalmente por cuenta de gasto. La cabecera de la factura guarda la suma de las líneas.
/// </summary>
public sealed class FacturaImpuesto : INotifyPropertyChanged
{
    private decimal _base;
    private decimal _porcIva;
    private decimal _cuotaIva;
    private decimal? _porcRe;
    private decimal? _cuotaRe;
    private string? _cuenta;

    public int Id { get; set; }
    public short Orden { get; set; }

    public decimal BaseImponible { get => _base; set => Set(ref _base, value); }
    public decimal PorcIVA { get => _porcIva; set => Set(ref _porcIva, value); }
    public decimal CuotaIVA { get => _cuotaIva; set => Set(ref _cuotaIva, value); }

    /// <summary>Recargo de equivalencia: solo si la factura lo indica.</summary>
    public decimal? PorcRE { get => _porcRe; set => Set(ref _porcRe, value); }
    public decimal? CuotaRE { get => _cuotaRe; set => Set(ref _cuotaRe, value); }

    /// <summary>Cuenta de gasto de la línea. Null = la cuenta de la factura.</summary>
    public string? CuentaContable { get => _cuenta; set => Set(ref _cuenta, string.IsNullOrWhiteSpace(value) ? null : value.Trim()); }

    public FacturaImpuesto Clone() => new()
    {
        Id = Id, Orden = Orden, BaseImponible = BaseImponible, PorcIVA = PorcIVA, CuotaIVA = CuotaIVA,
        PorcRE = PorcRE, CuotaRE = CuotaRE, CuentaContable = CuentaContable,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return;
        campo = valor;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}

/// <summary>Totales de un desglose, para la cabecera de la factura y el cuadre.</summary>
public static class Desglose
{
    public static decimal Base(IEnumerable<FacturaImpuesto> l) => l.Sum(x => x.BaseImponible);
    public static decimal Iva(IEnumerable<FacturaImpuesto> l) => l.Sum(x => x.CuotaIVA);
    public static decimal Re(IEnumerable<FacturaImpuesto> l) => l.Sum(x => x.CuotaRE ?? 0);

    /// <summary>% IVA de cabecera: el tipo si todas las líneas tienen el mismo; null si hay varios.</summary>
    public static decimal? PorcIvaUnico(IReadOnlyCollection<FacturaImpuesto> l) =>
        l.Select(x => x.PorcIVA).Distinct().Count() == 1 ? l.First().PorcIVA : null;

    /// <summary>Base + IVA + recargo − retención.</summary>
    public static decimal TotalCalculado(IEnumerable<FacturaImpuesto> l, decimal? retencion)
    {
        var lista = l.ToList();
        return Base(lista) + Iva(lista) + Re(lista) - (retencion ?? 0);
    }
}
