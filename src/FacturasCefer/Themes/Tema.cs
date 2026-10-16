using System.Windows;
using System.Windows.Media;

namespace FacturasCefer;

/// <summary>Acceso desde C# a los colores de <c>Themes/Tema.xaml</c>.</summary>
public static class Tema
{
    public static Brush Pincel(string clave) => (Brush)Application.Current.FindResource(clave);

    public static Brush Texto => Pincel("Texto");
    public static Brush OkTexto => Pincel("OkTexto");
    public static Brush OkFondo => Pincel("OkFondo");
    public static Brush OkBorde => Pincel("OkBorde");
    public static Brush ErrorTexto => Pincel("ErrorTexto");
    public static Brush ErrorFondo => Pincel("ErrorFondo");
    public static Brush ErrorBorde => Pincel("ErrorBorde");
    public static Brush AvisoTexto => Pincel("AvisoTexto");
    public static Brush AvisoFondo => Pincel("AvisoFondo");
    public static Brush AvisoBorde => Pincel("AvisoBorde");
    public static Brush InfoTexto => Pincel("InfoTexto");
    public static Brush InfoFondo => Pincel("InfoFondo");
    public static Brush InfoBorde => Pincel("InfoBorde");
    public static Brush DudosoFondo => Pincel("DudosoFondo");

    /// <summary>Estilos de aviso (Border) del tema.</summary>
    public static Style EstiloAviso(TipoAviso tipo) => (Style)Application.Current.FindResource(tipo switch
    {
        TipoAviso.Ok => "AvisoOk",
        TipoAviso.Error => "AvisoError",
        TipoAviso.Atencion => "AvisoAtencion",
        _ => "AvisoInfo",
    });
}

public enum TipoAviso { Info, Ok, Atencion, Error }
