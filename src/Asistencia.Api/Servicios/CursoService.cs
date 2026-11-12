using Asistencia.Api.Infraestructura;
using Asistencia.Api.Modelos;
using Dapper;

namespace Asistencia.Api.Servicios;

public sealed class CursoService(DbConnectionFactory db, TimeProvider reloj)
{
    private const string SqlCurso =
        """
        SELECT c.CursoId, c.Codigo, c.Nombre, c.Periodo, c.AulaLatitud, c.AulaLongitud, c.RadioMetros,
               (SELECT COUNT(*) FROM dbo.Matricula m WHERE m.CursoId = c.CursoId AND m.Activa = 1) AS Matriculados,
               (SELECT COUNT(*) FROM dbo.SesionClase s WHERE s.CursoId = c.CursoId) AS Sesiones,
               (SELECT MIN(s.InicioUtc) FROM dbo.SesionClase s
                 WHERE s.CursoId = c.CursoId AND s.Estado <> 'Cerrada' AND s.FinUtc > @ahora) AS ProximaSesionUtc
        FROM dbo.Curso c
        """;

    public async Task<IReadOnlyList<CursoRow>> ListarAsync(int docenteId)
    {
        using var cn = db.Crear();
        var filas = await cn.QueryAsync<CursoRow>(
            SqlCurso + " WHERE c.DocenteId = @docenteId AND c.Activo = 1 ORDER BY c.Periodo DESC, c.Nombre",
            new { docenteId, ahora = reloj.GetUtcNow().UtcDateTime });
        return filas.AsList();
    }

    public async Task<CursoRow?> ObtenerAsync(int cursoId, int docenteId)
    {
        using var cn = db.Crear();
        return await cn.QuerySingleOrDefaultAsync<CursoRow>(
            SqlCurso + " WHERE c.CursoId = @cursoId AND c.DocenteId = @docenteId",
            new { cursoId, docenteId, ahora = reloj.GetUtcNow().UtcDateTime });
    }

    public async Task<IReadOnlyList<PorcentajeFila>> PorcentajesAsync(int cursoId)
    {
        using var cn = db.Crear();
        var filas = await cn.QueryAsync<PorcentajeFila>(
            """
            SELECT m.EstudianteId, u.Identificacion, u.Apellidos + N' ' + u.Nombres AS Estudiante,
                   ISNULL(p.Sesiones, 0) AS Sesiones, ISNULL(p.Presentes, 0) AS Presentes, ISNULL(p.Parciales, 0) AS Parciales,
                   ISNULL(p.Ausentes, 0) AS Ausentes, ISNULL(p.Justificados, 0) AS Justificados, p.Porcentaje
            FROM dbo.Matricula m
            JOIN dbo.Usuario u ON u.UsuarioId = m.EstudianteId
            LEFT JOIN dbo.vw_PorcentajeAsistencia p ON p.CursoId = m.CursoId AND p.EstudianteId = m.EstudianteId
            WHERE m.CursoId = @cursoId AND m.Activa = 1
            ORDER BY Estudiante
            """, new { cursoId });
        return filas.AsList();
    }

    /// <summary>Permite al estudiante iniciar sesión en un celular nuevo.</summary>
    public async Task DesvincularDispositivoAsync(int cursoId, int estudianteId, int docenteId)
    {
        using var cn = db.Crear();
        await cn.ExecuteAsync(
            """
            UPDATE u SET DeviceIdVinculado = NULL, FechaVinculacionUtc = NULL
            FROM dbo.Usuario u
            WHERE u.UsuarioId = @estudianteId
              AND EXISTS (SELECT 1 FROM dbo.Matricula m JOIN dbo.Curso c ON c.CursoId = m.CursoId
                          WHERE m.CursoId = @cursoId AND m.EstudianteId = @estudianteId AND c.DocenteId = @docenteId)
            """, new { cursoId, estudianteId, docenteId });
    }
}
