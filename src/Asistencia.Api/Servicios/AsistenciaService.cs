using System.Data;
using System.Text.RegularExpressions;
using Asistencia.Api.Configuracion;
using Asistencia.Api.Infraestructura;
using Asistencia.Api.Modelos;
using Asistencia.Api.Seguridad;
using Asistencia.Shared.Constantes;
using Asistencia.Shared.Dtos;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Asistencia.Api.Servicios;

/// <summary>
/// Validación de marcaciones del estudiante (QR o código de 6 dígitos).
/// Orden de validaciones:
///   1. Dispositivo vinculado al estudiante
///   2. Token (firma HMAC + paso de tiempo vigente)
///   3. Checkpoint abierto y sesión no cerrada
///   4. Matrícula activa en el curso
///   5. No registrado previamente
///   6. Geocerca (solo Presencial con ValidarUbicacion)
///   7. Inserción (constraints UNIQUE como última defensa)
/// </summary>
public sealed partial class AsistenciaService(
    DbConnectionFactory db,
    CheckpointTokenService tokens,
    IOptions<CheckpointOptions> opciones,
    TimeProvider reloj,
    ILogger<AsistenciaService> log)
{
    private readonly CheckpointOptions _op = opciones.Value;

    private const string SqlContexto =
        """
        SELECT k.CheckpointId, k.SesionId, k.Orden, k.Secreto, k.InicioUtc, k.FinUtc, k.RotacionSegundos, k.Cancelado,
               s.Estado AS EstadoSesion, s.Modalidad, s.ValidarUbicacion, s.Latitud, s.Longitud, s.RadioMetros, s.Tema,
               c.CursoId, c.Nombre AS Curso
        FROM dbo.[Checkpoint] k
        JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
        JOIN dbo.Curso c       ON c.CursoId  = s.CursoId
        """;

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex RegexCodigo();

    public async Task<ResultadoMarcacion> MarcarAsync(int estudianteId, string deviceId, string? ip, MarcarRequest req)
    {
        var ahora = reloj.GetUtcNow();
        using var cn = db.Crear();

        // 1. Dispositivo
        var vinculado = await cn.ExecuteScalarAsync<string?>(
            "SELECT DeviceIdVinculado FROM dbo.Usuario WHERE UsuarioId = @estudianteId AND Activo = 1",
            new { estudianteId });
        if (vinculado is null || vinculado != deviceId)
            return await RechazarAsync(cn, estudianteId, null, req, deviceId, ip,
                CodigosResultado.DispositivoNoVinculado,
                "Este dispositivo no está vinculado a tu cuenta. Vuelve a iniciar sesión.");

        // 2. Resolver checkpoint y validar token
        var (cp, error) = req.Metodo switch
        {
            MetodosMarcacion.QR => await ResolverPorQrAsync(cn, req.Valor, ahora),
            MetodosMarcacion.Codigo => await ResolverPorCodigoAsync(cn, estudianteId, req.Valor, ahora),
            _ => (null, ResultadoMarcacion.Falla(CodigosResultado.QrInvalido, "Método no soportado."))
        };
        if (error is not null || cp is null)
            return await RechazarAsync(cn, estudianteId, cp?.CheckpointId, req, deviceId, ip,
                error!.Codigo, error.Mensaje);

        // 3. Ventana del checkpoint
        var ahoraUtc = ahora.UtcDateTime;
        if (cp.Cancelado || cp.EstadoSesion == "Cerrada" ||
            ahoraUtc < cp.InicioUtc || ahoraUtc > cp.FinUtc.AddSeconds(_op.GraciaSegundos))
            return await RechazarAsync(cn, estudianteId, cp.CheckpointId, req, deviceId, ip,
                CodigosResultado.CheckpointCerrado, "Este checkpoint ya no está abierto.");

        // 4. Matrícula
        var matriculado = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Matricula WHERE CursoId = @CursoId AND EstudianteId = @estudianteId AND Activa = 1",
            new { cp.CursoId, estudianteId });
        if (matriculado == 0)
            return await RechazarAsync(cn, estudianteId, cp.CheckpointId, req, deviceId, ip,
                CodigosResultado.NoMatriculado, $"No estás matriculado en {cp.Curso}.");

        // 5. ¿Ya registrado?
        var previo = await cn.ExecuteScalarAsync<DateTime?>(
            "SELECT FechaHoraUtc FROM dbo.RegistroCheckpoint WHERE CheckpointId = @CheckpointId AND EstudianteId = @estudianteId",
            new { cp.CheckpointId, estudianteId });
        if (previo is not null)
            return Ok(cp, CodigosResultado.YaRegistrado, "Tu asistencia ya estaba registrada.", previo.Value);

        // 6. Geocerca (solo presencial)
        double? distancia = null;
        if (cp.Modalidad == Modalidades.Presencial && cp.ValidarUbicacion && cp.Latitud is not null && cp.Longitud is not null)
        {
            if (req.Latitud is null || req.Longitud is null)
                // No se registra como intento rechazado: es el "handshake" para que la app pida el GPS.
                return ResultadoMarcacion.Falla(CodigosResultado.UbicacionRequerida,
                    "Esta clase requiere validar tu ubicación.");

            if (req.UbicacionSimulada)
                return await RechazarAsync(cn, estudianteId, cp.CheckpointId, req, deviceId, ip,
                    CodigosResultado.UbicacionSimulada, "Se detectó una ubicación simulada (fake GPS).");

            var precision = req.PrecisionMetros ?? 0;
            if (precision > _op.PrecisionMaximaMetros)
                return await RechazarAsync(cn, estudianteId, cp.CheckpointId, req, deviceId, ip,
                    CodigosResultado.FueraDeRango,
                    $"La señal GPS es imprecisa ({precision:0} m). Acércate a una ventana e intenta de nuevo.");

            var radio = cp.RadioMetros ?? 50;
            distancia = Geo.DistanciaMetros((double)cp.Latitud, (double)cp.Longitud, req.Latitud.Value, req.Longitud.Value);
            var permitido = radio + Math.Min(precision, radio); // tolerancia por el error del GPS
            if (distancia > permitido)
                return await RechazarAsync(cn, estudianteId, cp.CheckpointId, req, deviceId, ip,
                    CodigosResultado.FueraDeRango,
                    $"Estás a {distancia:0} m del aula (máximo {permitido:0} m).");
        }

        // 7. Registrar
        try
        {
            await cn.ExecuteAsync(
                """
                INSERT INTO dbo.RegistroCheckpoint
                    (CheckpointId, EstudianteId, FechaHoraUtc, Metodo, DeviceId, Latitud, Longitud, PrecisionMetros, DistanciaMetros, IpOrigen)
                VALUES
                    (@CheckpointId, @estudianteId, @fecha, @Metodo, @deviceId, @Latitud, @Longitud, @PrecisionMetros, @distancia, @ip)
                """,
                new
                {
                    cp.CheckpointId, estudianteId, fecha = ahoraUtc, req.Metodo, deviceId,
                    Latitud = (decimal?)req.Latitud, Longitud = (decimal?)req.Longitud,
                    PrecisionMetros = (decimal?)req.PrecisionMetros, distancia = (decimal?)distancia, ip
                });
        }
        catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
        {
            if (ex.Message.Contains("UQ_Registro_Device", StringComparison.OrdinalIgnoreCase))
                return await RechazarAsync(cn, estudianteId, cp.CheckpointId, req, deviceId, ip,
                    CodigosResultado.DispositivoYaUsado, "Este dispositivo ya registró asistencia en este checkpoint.");

            return Ok(cp, CodigosResultado.YaRegistrado, "Tu asistencia ya estaba registrada.", ahoraUtc);
        }

        return Ok(cp, CodigosResultado.Registrado, "¡Asistencia registrada!", ahoraUtc);
    }

    /// <summary>Checkpoints abiertos ahora en los cursos del estudiante (para avisarle en la app).</summary>
    public async Task<IReadOnlyList<CheckpointActivoDto>> CheckpointsActivosAsync(int estudianteId)
    {
        using var cn = db.Crear();
        var filas = await cn.QueryAsync<CheckpointActivoDto>(
            """
            SELECT k.CheckpointId, c.Nombre AS Curso, s.Tema, s.Modalidad, CAST(k.Orden AS INT) AS Orden,
                   DATEDIFF(SECOND, @ahora, k.FinUtc) AS SegundosRestantes,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.RegistroCheckpoint r
                                          WHERE r.CheckpointId = k.CheckpointId AND r.EstudianteId = @estudianteId)
                             THEN 1 ELSE 0 END AS BIT) AS YaRegistrado
            FROM dbo.[Checkpoint] k
            JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
            JOIN dbo.Curso c       ON c.CursoId = s.CursoId
            JOIN dbo.Matricula m   ON m.CursoId = c.CursoId AND m.EstudianteId = @estudianteId AND m.Activa = 1
            WHERE k.Cancelado = 0 AND s.Estado <> 'Cerrada'
              AND k.InicioUtc <= @ahora AND k.FinUtc > @ahora
            ORDER BY k.FinUtc
            """, new { estudianteId, ahora = reloj.GetUtcNow().UtcDateTime });
        return filas.AsList();
    }

    public async Task<IReadOnlyList<HistorialItemDto>> HistorialAsync(int estudianteId, int dias)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        using var cn = db.Crear();
        var filas = await cn.QueryAsync<HistorialItemDto>(
            """
            SELECT SesionId, InicioUtc, Curso, Tema, Modalidad, Validados, TotalCheckpoints, EstadoFinal
            FROM dbo.vw_AsistenciaSesion
            WHERE EstudianteId = @estudianteId AND InicioUtc BETWEEN @desde AND @ahora
            ORDER BY InicioUtc DESC
            """, new { estudianteId, desde = ahora.AddDays(-Math.Clamp(dias, 1, 365)), ahora });
        // SQL devuelve DateTime "Unspecified": se marca como UTC para que el JSON lleve la "Z".
        return filas.Select(h => h with { InicioUtc = DateTime.SpecifyKind(h.InicioUtc, DateTimeKind.Utc) }).ToList();
    }

    // ------------------------------------------------------------------

    private async Task<(CheckpointContexto?, ResultadoMarcacion?)> ResolverPorQrAsync(
        IDbConnection cn, string payload, DateTimeOffset ahora)
    {
        var invalido = ResultadoMarcacion.Falla(CodigosResultado.QrInvalido, "El código QR no es válido para esta app.");

        if (!CheckpointTokenService.TryParsearQr(payload, out var checkpointId, out var paso, out var firma))
            return (null, invalido);

        var cp = await cn.QuerySingleOrDefaultAsync<CheckpointContexto>(
            SqlContexto + " WHERE k.CheckpointId = @checkpointId", new { checkpointId });
        if (cp is null || !tokens.FirmaValida(cp.CheckpointId, paso, cp.Secreto, firma))
            return (null, invalido);

        var pasoActual = CheckpointTokenService.Paso(ahora, cp.RotacionSegundos);
        if (paso > pasoActual || paso < pasoActual - _op.PasosTolerancia)
            return (cp, ResultadoMarcacion.Falla(CodigosResultado.TokenExpirado,
                "El QR expiró. Escanea el código que está en pantalla ahora."));

        return (cp, null);
    }

    private async Task<(CheckpointContexto?, ResultadoMarcacion?)> ResolverPorCodigoAsync(
        IDbConnection cn, int estudianteId, string codigo, DateTimeOffset ahora)
    {
        codigo = codigo.Replace(" ", "").Trim();
        if (!RegexCodigo().IsMatch(codigo))
            return (null, ResultadoMarcacion.Falla(CodigosResultado.CodigoInvalido, "El código debe tener 6 dígitos."));

        // Candidatos: checkpoints abiertos en cursos donde el estudiante está matriculado.
        var candidatos = await cn.QueryAsync<CheckpointContexto>(
            SqlContexto +
            """

            JOIN dbo.Matricula m ON m.CursoId = c.CursoId AND m.EstudianteId = @estudianteId AND m.Activa = 1
            WHERE k.Cancelado = 0 AND s.Estado <> 'Cerrada'
              AND k.InicioUtc <= @ahora AND DATEADD(SECOND, @gracia, k.FinUtc) >= @ahora
            """, new { estudianteId, ahora = ahora.UtcDateTime, gracia = _op.GraciaSegundos });

        foreach (var cp in candidatos)
        {
            var pasoActual = CheckpointTokenService.Paso(ahora, cp.RotacionSegundos);
            for (var p = pasoActual; p >= pasoActual - _op.PasosTolerancia; p--)
                if (tokens.CodigoValido(cp.CheckpointId, p, cp.Secreto, codigo))
                    return (cp, null);
        }

        return (null, ResultadoMarcacion.Falla(CodigosResultado.CodigoInvalido,
            "Código incorrecto o expirado. Ingresa el código que está en pantalla ahora."));
    }

    private static ResultadoMarcacion Ok(CheckpointContexto cp, string codigo, string mensaje, DateTime fechaUtc) =>
        new(true, codigo, mensaje, cp.Curso, cp.Tema, cp.Modalidad, cp.Orden, DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc));

    private async Task<ResultadoMarcacion> RechazarAsync(
        IDbConnection cn, int estudianteId, int? checkpointId, MarcarRequest req,
        string deviceId, string? ip, string codigo, string mensaje)
    {
        try
        {
            await cn.ExecuteAsync(
                """
                INSERT INTO dbo.IntentoRechazado (EstudianteId, CheckpointId, Metodo, CodigoError, Detalle, DeviceId, IpOrigen)
                VALUES (@estudianteId, @checkpointId, @Metodo, @codigo, @detalle, @deviceId, @ip)
                """,
                new
                {
                    estudianteId, checkpointId, Metodo = req.Metodo.Length > 10 ? req.Metodo[..10] : req.Metodo,
                    codigo, detalle = mensaje.Length > 300 ? mensaje[..300] : mensaje, deviceId, ip
                });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "No se pudo registrar el intento rechazado");
        }
        return ResultadoMarcacion.Falla(codigo, mensaje);
    }
}
