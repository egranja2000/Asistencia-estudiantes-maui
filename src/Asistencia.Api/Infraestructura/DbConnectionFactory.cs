using System.Data;
using Microsoft.Data.SqlClient;

namespace Asistencia.Api.Infraestructura;

public sealed class DbConnectionFactory(IConfiguration configuration)
{
    private readonly string _cs = configuration.GetConnectionString("AsistenciaDB")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:AsistenciaDB");

    public IDbConnection Crear() => new SqlConnection(_cs);
}
