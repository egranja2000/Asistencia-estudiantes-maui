using Asistencia.Api.Infraestructura;
using Asistencia.Api.Modelos;
using Asistencia.Api.Seguridad;
using Asistencia.Shared.Constantes;
using Asistencia.Shared.Dtos;
using Dapper;

namespace Asistencia.Api.Servicios;

/// <summary>
/// SOLO DESARROLLO. Simula que los estudiantes mock (dispositivo "mock-*") marcan el checkpoint
/// activo de una sesión. Pasa por AsistenciaService.MarcarAsync, es decir, por la MISMA validación
/// que usa la app (HMAC, tiempo, matrícula, dispositivo, geocerca, duplicados).
/// Opcionalmente genera intentos inválidos para ver los rechazos en el panel del docente.
/// </summary>
public sealed class SimuladorService(
    DbConnectionFactory db,
    AsistenciaService asistencia,
    CheckpointTokenService tokens,
    TimeProvider reloj)
{
    public sealed record Resultado(string? Error, IReadOnlyDictionary<string, int> PorCodigo, int Total);

    public sealed class EstudianteMock
    {
        public int UsuarioId { get; set; }
        public string DeviceId { get; set; } = "";
    }

    public async Task<Resultado> SimularAsync(SesionRow sesion, int porcentaje, bool incluirErrores)
    {
        var ahora = reloj.GetUtcNow();
        using var cn = db.Crear();

        var cp = await cn.QuerySingleOrDefaultAsync<CheckpointContexto>(
            """
            SELECT TOP 1 k.CheckpointId, k.SesionId, k.Orden, k.Secreto, k.InicioUtc, k.FinUtc, k.RotacionSegundos
            FROM dbo.[Checkpoint] k
            WHERE k.SesionId = @SesionId AND k.Cancelado = 0 AND k.InicioUtc <= @ahora AND k.FinUtc > @ahora
            ORDER BY k.InicioUtc
            """, new { sesion.SesionId, ahora = ahora.UtcDateTime });

        if (cp is null)
            return new Resultado("No hay un checkpoint activo en esta sesión. Lánzalo primero.", new Dictionary<string, int>(), 0);

        // Estudiantes mock matriculados que aún no marcan este checkpoint
        var pendientes = (await cn.QueryAsync<EstudianteMock>(
            """
            SELECT u.UsuarioId, u.DeviceIdVinculado AS DeviceId
            FROM dbo.Matricula m
            JOIN dbo.Usuario u ON u.UsuarioId = m.EstudianteId
            WHERE m.CursoId = @CursoId AND m.Activa = 1 AND u.DeviceIdVinculado LIKE N'mock-%'
              AND NOT EXISTS (SELECT 1 FROM dbo.RegistroCheckpoint r
                              WHERE r.CheckpointId = @CheckpointId AND r.EstudianteId = u.UsuarioId)
            """, new { sesion.CursoId, cp.CheckpointId }))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var cantidad = (int)Math.Round(pendientes.Count * Math.Clamp(porcentaje, 0, 100) / 100.0);
        var validos = pendientes.Take(cantidad).ToList();
        var resto = pendientes.Skip(cantidad).ToList();

        var paso = CheckpointTokenService.Paso(ahora, cp.RotacionSegundos);
        var conGeocerca = sesion.Modalidad == Modalidades.Presencial && sesion.ValidarUbicacion
                          && sesion.Latitud is not null && sesion.Longitud is not null;
        var conteo = new Dictionary<string, int>();

        async Task Marcar(EstudianteMock e, MarcarRequest req, string? deviceId = null)
        {
            var r = await asistencia.MarcarAsync(e.UsuarioId, deviceId ?? e.DeviceId, "simulador", req);
            conteo[r.Codigo] = conteo.GetValueOrDefault(r.Codigo) + 1;
        }

        MarcarRequest Valida(int i)
        {
            // Alterna métodos: online la mitad usa código; presencial casi todos QR
            var usarCodigo = sesion.Modalidad == Modalidades.Online ? i % 2 == 0 : i % 5 == 0;
            var req = usarCodigo
                ? new MarcarRequest { Metodo = MetodosMarcacion.Codigo, Valor = tokens.GenerarCodigo(cp.CheckpointId, paso, cp.Secreto) }
                : new MarcarRequest { Metodo = MetodosMarcacion.QR, Valor = tokens.GenerarPayloadQr(cp.CheckpointId, paso, cp.Secreto) };
            if (conGeocerca) PonerUbicacion(req, sesion, metrosDesplazamiento: Random.Shared.Next(0, 25));
            return req;
        }

        for (var i = 0; i < validos.Count; i++)
            await Marcar(validos[i], Valida(i));

        if (incluirErrores && resto.Count > 0)
        {
            var qrValido = tokens.GenerarPayloadQr(cp.CheckpointId, paso, cp.Secreto);
            var casos = new List<(MarcarRequest Req, bool DeviceAjeno)>
            {
                // QR de hace 2 minutos (foto reenviada por chat)
                (new MarcarRequest { Metodo = MetodosMarcacion.QR,
                    Valor = tokens.GenerarPayloadQr(cp.CheckpointId, paso - 6, cp.Secreto) }, false),
                // QR manipulado (firma alterada)
                (new MarcarRequest { Metodo = MetodosMarcacion.QR, Valor = qrValido[..^2] + "xx" }, false),
                // Código inventado
                (new MarcarRequest { Metodo = MetodosMarcacion.Codigo, Valor = "123456" }, false),
                // Token válido pero desde otro celular (cuenta prestada)
                (Valida(1), true),
            };
            if (conGeocerca)
            {
                var lejos = Valida(1);
                PonerUbicacion(lejos, sesion, metrosDesplazamiento: 1500);                 // desde la casa
                casos.Add((lejos, false));

                var falso = Valida(1);
                falso.UbicacionSimulada = true;                                             // app de fake GPS
                casos.Add((falso, false));
            }

            for (var i = 0; i < casos.Count && i < resto.Count; i++)
                await Marcar(resto[i], casos[i].Req, casos[i].DeviceAjeno ? "celular-de-otra-persona" : null);
        }

        return new Resultado(null, conteo, conteo.Values.Sum());
    }

    /// <summary>Ubicación a N metros (hacia el norte) del centro de la geocerca, con precisión típica de GPS.</summary>
    private static void PonerUbicacion(MarcarRequest req, SesionRow s, int metrosDesplazamiento)
    {
        req.Latitud = (double)s.Latitud!.Value + metrosDesplazamiento / 111_320.0;
        req.Longitud = (double)s.Longitud!.Value;
        req.PrecisionMetros = Random.Shared.Next(5, 20);
    }
}
