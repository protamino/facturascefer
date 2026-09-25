using System.Numerics;
using System.Text;

namespace FacturasCefer.Services;

/// <summary>Normalización y validación de CIF/NIF/NIE e IBAN.</summary>
public static class Validaciones
{
    private const string LetrasNif = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string LetrasCifControl = "JABCDEFGHI";

    /// <summary>Mayúsculas, sin espacios, guiones ni puntos. Quita el prefijo "ES" de VAT intracomunitario.</summary>
    public static string NormalizarCif(string? cif)
    {
        if (string.IsNullOrWhiteSpace(cif)) return "";
        var sb = new StringBuilder();
        foreach (var c in cif.ToUpperInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        var s = sb.ToString();
        return s.Length == 11 && s.StartsWith("ES") ? s[2..] : s;
    }

    /// <summary>Mayúsculas y sin espacios.</summary>
    public static string NormalizarIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return "";
        var sb = new StringBuilder();
        foreach (var c in iban.ToUpperInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    public static string FormatearIban(string? iban)
    {
        var s = NormalizarIban(iban);
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>True si es un NIF, NIE o CIF español con dígito de control correcto.</summary>
    public static bool CifValido(string? valor)
    {
        var s = NormalizarCif(valor);
        if (s.Length != 9) return false;

        // NIF: 8 dígitos + letra
        if (char.IsDigit(s[0]))
            return s[..8].All(char.IsDigit) && s[8] == LetrasNif[int.Parse(s[..8]) % 23];

        // NIE: X/Y/Z + 7 dígitos + letra
        if ("XYZ".Contains(s[0]))
        {
            if (!s[1..8].All(char.IsDigit)) return false;
            var num = int.Parse("XYZ".IndexOf(s[0]) + s[1..8]);
            return s[8] == LetrasNif[num % 23];
        }

        // CIF: letra + 7 dígitos + control (dígito o letra)
        if (!"ABCDEFGHJNPQRSUVW".Contains(s[0]) || !s[1..8].All(char.IsDigit)) return false;
        var suma = 0;
        for (var i = 0; i < 7; i++)
        {
            var d = s[1 + i] - '0';
            if (i % 2 == 0)
            {
                d *= 2;
                suma += d / 10 + d % 10;
            }
            else suma += d;
        }
        var control = (10 - suma % 10) % 10;
        var digito = (char)('0' + control);
        var letra = LetrasCifControl[control];

        if ("PQRSNW".Contains(s[0])) return s[8] == letra;
        if ("ABEH".Contains(s[0])) return s[8] == digito;
        return s[8] == digito || s[8] == letra;
    }

    /// <summary>True si el IBAN tiene formato y dígitos de control (mod 97) correctos.</summary>
    public static bool IbanValido(string? valor)
    {
        var s = NormalizarIban(valor);
        if (s.Length < 15 || s.Length > 34) return false;
        if (!char.IsLetter(s[0]) || !char.IsLetter(s[1]) || !char.IsDigit(s[2]) || !char.IsDigit(s[3])) return false;
        if (s.StartsWith("ES") && s.Length != 24) return false;

        var reordenado = s[4..] + s[..4];
        var numerico = new StringBuilder();
        foreach (var c in reordenado)
        {
            if (char.IsDigit(c)) numerico.Append(c);
            else if (c is >= 'A' and <= 'Z') numerico.Append(c - 'A' + 10);
            else return false;
        }
        return BigInteger.Parse(numerico.ToString()) % 97 == 1;
    }
}
