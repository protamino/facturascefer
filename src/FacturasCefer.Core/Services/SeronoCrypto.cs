using System.Security.Cryptography;
using System.Text;

namespace FacturasCefer.Services;

/// <summary>
/// Port a C# del cifrado legado de contraseñas de SIC (ver serono-crypto.ts de CEFERTESTIGO).
/// AES-256-CBC, PKCS7, entrada en hex. Dos variantes derivadas de la constante "SERONO":
///   - "serono" (UTF-16LE): key = UTF16LE("SERONOAAAAAAAAAA"), IV = UTF16LE("SERONOAA").
///   - "merck"  (ASCII):    key = ASCII("SERONO".PadRight(32,'A')), IV = ASCII("SERONO".PadRight(16,'A')).
/// No se sabe con cuál está cifrado DMSTRA.dbo.Usuarios.Pwd, así que se prueban ambas.
/// </summary>
public static class SeronoCrypto
{
    private static readonly byte[] SeronoKey = Encoding.Unicode.GetBytes("SERONOAAAAAAAAAA"); // 16 chars * 2 = 32 bytes
    private static readonly byte[] SeronoIv = Encoding.Unicode.GetBytes("SERONOAA");           // 8 chars * 2 = 16 bytes
    private static readonly byte[] MerckKey = Encoding.ASCII.GetBytes("SERONO".PadRight(32, 'A'));
    private static readonly byte[] MerckIv = Encoding.ASCII.GetBytes("SERONO".PadRight(16, 'A'));

    private static string? Decrypt(string hex, byte[] key, byte[] iv, Encoding encoding)
    {
        try
        {
            var data = Convert.FromHexString(hex.Trim());
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            var outBytes = dec.TransformFinalBlock(data, 0, data.Length);
            return encoding.GetString(outBytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True si alguna variante descifra <paramref name="hexPwd"/> exactamente a <paramref name="password"/>.</summary>
    public static bool PasswordCoincide(string hexPwd, string password)
    {
        if (string.IsNullOrEmpty(hexPwd)) return false;
        return Decrypt(hexPwd, SeronoKey, SeronoIv, Encoding.Unicode) == password
            || Decrypt(hexPwd, MerckKey, MerckIv, Encoding.ASCII) == password;
    }
}
