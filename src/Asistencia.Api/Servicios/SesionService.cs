using System.Data;
using System.Security.Cryptography;
using Asistencia.Api.Configuracion;
using Asistencia.Api.Infraestructura;
using Asistencia.Api.Modelos;
using Asistencia.Api.Seguridad;
using Asistencia.Api.ViewModels;
using Asistencia.Shared.Constantes;
using Dapper;
using Microsoft.Extensions.Options;

namespace Asistencia.Api.Servicios;

/// <summary>Operaciones del docente sobre sesiones y checkpoints.</summary>
public sealed class SesionService(
    DbConnectionFactory db,
    CheckpointTokenService tokens,
    ZonaHoraria zona,
    IOptions<CheckpointOptions> opciones,
    TimeProvider reloj)
{
    private readonly CheckpointOptions _op = opciones.Value;

    private const string SqlSesion =
        """
        SELECT s.SesionId, s.CursoId, c.Nombre AS Curso, c.Codigo AS CodigoCurso, s.Tema, s.Modalidad,
               s.InicioUtc, s.FinUtc, s.Estado, s.UrlReunion, s.ValidarUbicacion, s.Latitud, s.Longitud, s.RadioMetros,
               (SELECT COUNT(*) FROM dbo.Matricula m WHERE m.CursoId = s.CursoId AND m.Activa = 1) AS Matriculados,
               (SELECT COUNT(*) FROM dbo.[Checkpoint] k WHERE k.SesionId = s.SesionId AND k.Cancelado = 0) AS Checkpoints
        FROM dbo.SesionClase s
        JOIN dbo.Curso c ON c.CursoId = s.CursoId
        """;

    public int DuracionSugerida(string modalidad) =>
        modalidad == Modalidades.Online ? _op.DuracionOnlineSegundos : _op.DuracionPresencialSegundos;

    public async Task<SesionRow?> ObtenerAsync(int sesionId, int docenteId)
    {
        using var cn = db.Crear();
        return await ObtenerAsync(cn, sesionId, docenteId);
    }

    private static Task<SesionRow?> ObtenerAsync(IDbConnection cn, int sesionId, int docenteId) =>
        cn.QuerySingleOrDefaultAsync<SesionRow>(
            SqlSesion + " WHERE s.SesionId = @sesionId AND c.DocenteId = @docenteId", new { sesionId, docenteId });

    public async Task<IReadOnlyList<SesionRow>> ListarPorCursoAsync(int cursoId)
    {
        using var cn = db.Crear();
        var filas = await cn.QueryAsync<SesionRow>(SqlSesion + " WHERE s.CursoId = @cursoId ORDER BY s.InicioUtc DESC", new { cursoId });
        return filas.AsList();
    }

    public async Task<DetalleSesionViewModel?> DetalleAsync(int sesionId, int docenteId)
    {
        using var cn = db.Crear();
        var sesion = await ObtenerAsync(cn, sesionId, docenteId);
        if (sesion is null) return null;

        var checkpoints = await cn.QueryAsync<CheckpointRow>(
            """
            SELECT k.CheckpointId, k.Orden, k.InicioUtc, k.FinUtc, k.RotacionSegundos, k.Origen, k.Cancelado,
                   (SELECT COUNT(*) FROM dbo.RegistroCheckpoint r WHERE r.CheckpointId = k.CheckpointId) AS Registrados
            FROM dbo.[Checkpoint] k
            WHERE k.SesionId = @sesionId
            ORDER BY k.InicioUtc
            """, new { sesionId });

        var asistencia = await cn.QueryAsync<AsistenciaFila>(
            """
            SELECT EstudianteId, Identificacion, Estudiante, DeviceIdVinculado, TotalCheckpoints, Validados,
                   PrimeraMarcaUtc, EstadoCalculado, EstadoAjuste, MotivoAjuste, EstadoFinal
            FROM dbo.vw_AsistenciaSesion
            WHERE SesionId = @sesionId
            ORDER BY Estudiante
            """, new { sesionId });

        var intentos = await cn.QueryAsync<IntentoResumen>(
            """
            SELECT i.CodigoError, COUNT(*) AS Cantidad
            FROM dbo.IntentoRechazado i
            JOIN dbo.[Checkpoint] k ON k.CheckpointId = i.CheckpointId
            WHERE k.SesionId = @sesionId
            GROUP BY i.CodigoError
            ORDER BY Cantidad DESC
            """, new { sesionId });

        return new DetalleSesionViewModel
        {
            Sesion = sesion,
            Checkpoints = checkpoints.AsList(),
            Asistencia = asistencia.AsList(),
            Intentos = intentos.AsList(),
            DuracionSugeridaSegundos = DuracionSugerida(sesion.Modalidad)
        };
    }

    /// <summary>Crea la sesión y, si se pide, programa checkpoints automáticos en momentos aleatorios.</summary>
    public async Task<(int? SesionId, string? Error)> CrearAsync(int docenteId, CrearSesionViewModel m)
    {
        var inicio = zona.AUtc(m.Fecha.ToDateTime(m.HoraInicio));
        var fin = zona.AUtc(m.Fecha.ToDateTime(m.HoraFin));
        if (fin <= inicio) return (null, "La hora de fin debe ser posterior a la de inicio.");

        var online = m.Modalidad == Modalidades.Online;
        if (!online && m.Modalidad != Modalidades.Presencial) return (null, "Modalidad no válida.");

        var validarUbicacion = !online && m.ValidarUbicacion;
        if (validarUbicacion && (m.Latitud is null || m.Longitud is null || m.RadioMetros is null or <= 0))
            return (null, "Para validar ubicación indica latitud, longitud y radio del aula.");

        // Horarios de checkpoints automáticos (se calculan antes de abrir la transacción)
        var programados = new List<DateTime>();
        if (m.CheckpointsAutomaticos > 0)
        {
            var dur = TimeSpan.FromSeconds(m.DuracionCheckpointSegundos);
            var desde = inicio.AddMinutes(5);
            var hasta = fin - dur - TimeSpan.FromMinutes(1);
            if (hasta <= desde) { desde = inicio; hasta = fin - dur; }
            if (hasta <= desde) return (null, "La sesión es demasiado corta para los checkpoints automáticos.");

            // Divide la clase en N tramos y elige un instante aleatorio dentro de cada uno:
            // impredecible para el estudiante pero repartido (inicio, mitad, final).
            var tramo = (hasta - desde) / m.CheckpointsAutomaticos;
            for (var i = 0; i < m.CheckpointsAutomaticos; i++)
            {
                var offset = RandomNumberGenerator.GetInt32(0, Math.Max(1, (int)tramo.TotalSeconds));
                programados.Add(desde + tramo * i + TimeSpan.FromSeconds(offset));
            }
        }

        using var cn = db.Crear();
        cn.Open();

        var curso = await cn.QuerySingleOrDefaultAsync<int?>(
            "SELECT CursoId FROM dbo.Curso WHERE CursoId = @CursoId AND DocenteId = @docenteId AND Activo = 1",
            new { m.CursoId, docenteId });
        if (curso is null) return (null, "Curso no encontrado.");

        using var tx = cn.BeginTransaction();
        var sesionId = await cn.ExecuteScalarAsync<int>(
            """
            INSERT INTO dbo.SesionClase
                (CursoId, Tema, Modalidad, InicioUtc, FinUtc, UrlReunion, ValidarUbicacion, Latitud, Longitud, RadioMetros, CreadoPor)
            OUTPUT INSERTED.SesionId
            VALUES
                (@CursoId, @Tema, @Modalidad, @inicio, @fin, @url, @validarUbicacion, @lat, @lng, @radio, @docenteId)
            """,
            new
            {
                m.CursoId, m.Tema, m.Modalidad, inicio, fin,
                url = online ? m.UrlReunion : null,
                validarUbicacion,
                lat = validarUbicacion ? m.Latitud : null,
                lng = validarUbicacion ? m.Longitud : null,
                radio = validarUbicacion ? m.RadioMetros : null,
                docenteId
            }, tx);

        byte orden = 1;
        foreach (var t in programados)
            await InsertarCheckpointAsync(cn, tx, sesionId, orden++, t, t.AddSeconds(m.DuracionCheckpointSegundos),
                m.RotacionSegundos, "Automatico");

        tx.Commit();
        return (sesionId, null);
    }

    /// <summary>El docente lanza un checkpoint ahora mismo.</summary>
    public async Task<string?> LanzarCheckpointAsync(int sesionId, int docenteId, int duracionSegundos, int rotacionSegundos)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        using var cn = db.Crear();
        cn.Open();

        var sesion = await ObtenerAsync(cn, sesionId, docenteId);
        if (sesion is null) return "Sesión no encontrada.";
        if (sesion.Estado == "Cerrada") return "La sesión está cerrada.";

        using var tx = cn.BeginTransaction(IsolationLevel.Serializable);
        var activo = await cn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM dbo.[Checkpoint]
            WHERE SesionId = @sesionId AND Cancelado = 0 AND InicioUtc <= @ahora AND FinUtc > @ahora
            """, new { sesionId, ahora }, tx);
        if (activo > 0) return "Ya hay un checkpoint activo en esta sesión.";

        var orden = await cn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(Orden), 0) + 1 FROM dbo.[Checkpoint] WHERE SesionId = @sesionId", new { sesionId }, tx);
        if (orden > 255) return "Se alcanzó el máximo de checkpoints.";

        duracionSegundos = Math.Clamp(duracionSegundos <= 0 ? DuracionSugerida(sesion.Modalidad) : duracionSegundos, 20, 1800);
        rotacionSegundos = Math.Clamp(rotacionSegundos <= 0 ? _op.RotacionSegundos : rotacionSegundos, 10, 120);

        await InsertarCheckpointAsync(cn, tx, sesionId, (byte)orden, ahora, ahora.AddSeconds(duracionSegundos),
            rotacionSegundos, "Manual");
        await cn.ExecuteAsync(
            "UPDATE dbo.SesionClase SET Estado = 'Abierta' WHERE SesionId = @sesionId AND Estado = 'Programada'",
            new { sesionId }, tx);

        tx.Commit();
        return null;
    }

    public async Task CancelarCheckpointAsync(int checkpointId, int docenteId)
    {
        using var cn = db.Crear();
        await cn.ExecuteAsync(
            """
            UPDATE k SET Cancelado = 1
            FROM dbo.[Checkpoint] k
            JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
            JOIN dbo.Curso c       ON c.CursoId = s.CursoId
            WHERE k.CheckpointId = @checkpointId AND c.DocenteId = @docenteId AND s.Estado <> 'Cerrada'
            """, new { checkpointId, docenteId });
    }

    /// <summary>Cierra la sesión: corta el checkpoint activo y cancela los automáticos que no empezaron.</summary>
    public async Task CerrarAsync(int sesionId, int docenteId)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        using var cn = db.Crear();
        cn.Open();
        if (await ObtenerAsync(cn, sesionId, docenteId) is null) return;

        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync(
            """
            UPDATE dbo.[Checkpoint] SET Cancelado = 1
            WHERE SesionId = @sesionId AND Cancelado = 0 AND InicioUtc > @ahora;

            UPDATE dbo.[Checkpoint]
            SET FinUtc = CASE WHEN DATEADD(SECOND, -1, @ahora) > InicioUtc THEN @ahora ELSE DATEADD(SECOND, 1, InicioUtc) END
            WHERE SesionId = @sesionId AND Cancelado = 0 AND InicioUtc <= @ahora AND FinUtc > @ahora;

            UPDATE dbo.SesionClase SET Estado = 'Cerrada' WHERE SesionId = @sesionId;
            """, new { sesionId, ahora }, tx);
        tx.Commit();
    }

    /// <summary>Ajuste manual del docente. estado vacío = quitar ajuste y volver al calculado.</summary>
    public async Task AjustarAsync(int sesionId, int estudianteId, string? estado, string? motivo, int docenteId)
    {
        using var cn = db.Crear();
        if (await ObtenerAsync(cn, sesionId, docenteId) is null) return;

        if (string.IsNullOrWhiteSpace(estado))
        {
            await cn.ExecuteAsync("DELETE dbo.AjusteAsistencia WHERE SesionId = @sesionId AND EstudianteId = @estudianteId",
                new { sesionId, estudianteId });
            return;
        }

        await cn.ExecuteAsync(
            """
            MERGE dbo.AjusteAsistencia WITH (HOLDLOCK) AS t
            USING (SELECT @sesionId AS SesionId, @estudianteId AS EstudianteId) AS s
               ON t.SesionId = s.SesionId AND t.EstudianteId = s.EstudianteId
            WHEN MATCHED THEN
                UPDATE SET Estado = @estado, Motivo = @motivo, UsuarioId = @docenteId, FechaUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (SesionId, EstudianteId, Estado, Motivo, UsuarioId) VALUES (@sesionId, @estudianteId, @estado, @motivo, @docenteId);
            """, new { sesionId, estudianteId, estado, motivo, docenteId });
    }

    /// <summary>Estado actual para la pantalla de proyección (QR + código + cuenta regresiva).</summary>
    public async Task<EstadoProyeccion?> EstadoProyeccionAsync(int sesionId, int docenteId)
    {
        var ahora = reloj.GetUtcNow();
        using var cn = db.Crear();
        var sesion = await ObtenerAsync(cn, sesionId, docenteId);
        if (sesion is null) return null;
        if (sesion.Estado == "Cerrada") return new EstadoProyeccion("Cerrada", false, Matriculados: sesion.Matriculados);

        var cp = await cn.QuerySingleOrDefaultAsync<CheckpointContexto>(
            """
            SELECT TOP 1 k.CheckpointId, k.SesionId, k.Orden, k.Secreto, k.InicioUtc, k.FinUtc, k.RotacionSegundos
            FROM dbo.[Checkpoint] k
            WHERE k.SesionId = @sesionId AND k.Cancelado = 0 AND k.InicioUtc <= @ahora AND k.FinUtc > @ahora
            ORDER BY k.InicioUtc
            """, new { sesionId, ahora = ahora.UtcDateTime });

        if (cp is null) return new EstadoProyeccion(sesion.Estado, false, Matriculados: sesion.Matriculados);

        // Un checkpoint automático que empieza abre la sesión
        if (sesion.Estado == "Programada")
            await cn.ExecuteAsync("UPDATE dbo.SesionClase SET Estado = 'Abierta' WHERE SesionId = @sesionId AND Estado = 'Programada'",
                new { sesionId });

        var registrados = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.RegistroCheckpoint WHERE CheckpointId = @CheckpointId", new { cp.CheckpointId });

        var paso = CheckpointTokenService.Paso(ahora, cp.RotacionSegundos);
        return new EstadoProyeccion(
            EstadoSesion: "Abierta",
            Activo: true,
            CheckpointId: cp.CheckpointId,
            Orden: cp.Orden,
            Paso: paso,
            PayloadQr: tokens.GenerarPayloadQr(cp.CheckpointId, paso, cp.Secreto),
            Codigo: tokens.GenerarCodigo(cp.CheckpointId, paso, cp.Secreto),
            SegundosToken: CheckpointTokenService.SegundosHastaSiguientePaso(ahora, cp.RotacionSegundos),
            RotacionSegundos: cp.RotacionSegundos,
            SegundosCheckpoint: (int)Math.Max(0, (cp.FinUtc - ahora.UtcDateTime).TotalSeconds),
            Registrados: registrados,
            Matriculados: sesion.Matriculados);
    }

    private static Task InsertarCheckpointAsync(IDbConnection cn, IDbTransaction tx, int sesionId, byte orden,
        DateTime inicioUtc, DateTime finUtc, int rotacion, string origen) =>
        cn.ExecuteAsync(
            """
            INSERT INTO dbo.[Checkpoint] (SesionId, Orden, Secreto, InicioUtc, FinUtc, RotacionSegundos, Origen)
            VALUES (@sesionId, @orden, @secreto, @inicioUtc, @finUtc, @rotacion, @origen)
            """,
            new { sesionId, orden, secreto = CheckpointTokenService.NuevoSecreto(), inicioUtc, finUtc, rotacion, origen }, tx);
}
