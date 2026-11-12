using Asistencia.Api.Infraestructura;
using Asistencia.Api.Modelos;
using Asistencia.Api.Seguridad;
using Asistencia.Shared.Dtos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Asistencia.Api.Servicios;

public sealed class AuthService(DbConnectionFactory db, PasswordHasher hasher, JwtService jwt, TimeProvider reloj)
{
    private const string MensajeCredenciales = "Usuario o contraseña incorrectos.";

    private static Task<UsuarioRow?> BuscarAsync(System.Data.IDbConnection cn, string usuario) =>
        cn.QuerySingleOrDefaultAsync<UsuarioRow>(
            """
            SELECT UsuarioId, Identificacion, Nombres, Apellidos, Email, PasswordHash, Rol, DeviceIdVinculado, Activo
            FROM dbo.Usuario
            WHERE Email = @usuario OR Identificacion = @usuario
            """, new { usuario = usuario.Trim() });

    /// <summary>Login del estudiante desde la app. Vincula el dispositivo en el primer ingreso.</summary>
    public async Task<(LoginResponse? Respuesta, string? Error)> LoginEstudianteAsync(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId) || req.DeviceId.Length is < 8 or > 64)
            return (null, "Identificador de dispositivo no válido.");

        using var cn = db.Crear();
        var u = await BuscarAsync(cn, req.Usuario);
        if (u is null || !u.Activo || !hasher.Verificar(req.Password, u.PasswordHash))
            return (null, MensajeCredenciales);

        if (u.Rol != "Estudiante")
            return (null, "Esta app es solo para estudiantes. Los docentes ingresan por el portal web.");

        if (u.DeviceIdVinculado is null)
        {
            try
            {
                var filas = await cn.ExecuteAsync(
                    """
                    UPDATE dbo.Usuario
                    SET DeviceIdVinculado = @DeviceId, FechaVinculacionUtc = @ahora
                    WHERE UsuarioId = @UsuarioId AND DeviceIdVinculado IS NULL
                    """, new { req.DeviceId, u.UsuarioId, ahora = reloj.GetUtcNow().UtcDateTime });

                if (filas == 0) // otro login ganó la carrera: releer
                    u.DeviceIdVinculado = await cn.ExecuteScalarAsync<string?>(
                        "SELECT DeviceIdVinculado FROM dbo.Usuario WHERE UsuarioId = @UsuarioId", new { u.UsuarioId });
                else
                    u.DeviceIdVinculado = req.DeviceId;
            }
            catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
            {
                return (null, "Este dispositivo ya está vinculado a la cuenta de otro estudiante.");
            }
        }

        if (u.DeviceIdVinculado != req.DeviceId)
            return (null, "Tu cuenta está vinculada a otro dispositivo. Pide a tu docente que la desvincule.");

        var (token, expira) = jwt.CrearTokenEstudiante(u.UsuarioId, u.NombreCompleto, req.DeviceId);
        return (new LoginResponse(token, expira, u.UsuarioId, u.NombreCompleto, u.Identificacion), null);
    }

    /// <summary>Login del docente en el portal web (cookie).</summary>
    public async Task<UsuarioRow?> ValidarDocenteAsync(string usuario, string password)
    {
        using var cn = db.Crear();
        var u = await BuscarAsync(cn, usuario);
        if (u is null || !u.Activo || u.Rol is not ("Docente" or "Admin")) return null;
        return hasher.Verificar(password, u.PasswordHash) ? u : null;
    }
}
