using FacturasCefer.Config;
using FacturasCefer.Models;
using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

/// <summary>
/// Autentica usuario/contraseña contra DMSTRA.dbo.Usuarios (mismo store que SIC/CEFERTESTIGO).
/// Devuelve el idUsuario, que se guarda en los campos de auditoría (IdUsuarioAlta, IdUsuarioRegistro…).
/// </summary>
public sealed class AuthService
{
    private readonly AppConfig _cfg;

    public AuthService(AppConfig cfg) => _cfg = cfg;

    public sealed record LoginResult(Usuario? Usuario, bool UsuarioEncontrado)
    {
        public bool Ok => Usuario is not null;
    }

    /// <summary>Valida credenciales contra DMSTRA.dbo.Usuarios.</summary>
    public async Task<LoginResult> LoginAsync(string usuario, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usuario)) return new LoginResult(null, false);

        await using var cn = new SqlConnection(_cfg.Dmstra.ConnectionString);
        await cn.OpenAsync(ct);

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT idUsuario, NombreUser, Pwd
                            FROM dbo.Usuarios
                            WHERE NombreUser = @u AND ISNULL(Baja, 0) = 0";
        cmd.Parameters.AddWithValue("@u", usuario.Trim());

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return new LoginResult(null, false);

        var idUsuario = rd.GetInt32(0);
        var nombreUser = rd.GetString(1).Trim();
        var pwd = rd.IsDBNull(2) ? "" : rd.GetString(2);

        return SeronoCrypto.PasswordCoincide(pwd, password)
            ? new LoginResult(new Usuario(idUsuario, nombreUser), true)
            : new LoginResult(null, true);
    }
}
