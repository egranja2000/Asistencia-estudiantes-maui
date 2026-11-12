using Microsoft.Data.SqlClient;

namespace Asistencia.Api.Infraestructura;

public static class SqlErrores
{
    /// <summary>2627 = violación de UNIQUE/PK, 2601 = índice único.</summary>
    public static bool EsDuplicado(SqlException ex) => ex.Number is 2627 or 2601;
}
