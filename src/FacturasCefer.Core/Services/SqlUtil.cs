using Microsoft.Data.SqlClient;

namespace FacturasCefer.Services;

internal static class SqlUtil
{
    public static string? Str(this SqlDataReader rd, string col)
    {
        var i = rd.GetOrdinal(col);
        return rd.IsDBNull(i) ? null : rd.GetString(i);
    }

    public static object DbVal(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

    /// <summary>Violación de UNIQUE / PRIMARY KEY.</summary>
    public static bool EsDuplicado(SqlException ex) => ex.Number is 2627 or 2601;
}

/// <summary>Error de negocio con mensaje apto para mostrar al usuario.</summary>
public sealed class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}
