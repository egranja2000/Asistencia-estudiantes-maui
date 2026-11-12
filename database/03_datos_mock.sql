/* =====================================================================
   03_datos_mock.sql  —  Datos de prueba realistas
   ---------------------------------------------------------------------
   ⚠  BORRA TODOS LOS DATOS de AsistenciaDB y los regenera.
      (reemplaza a 02_datos_demo.sql; no hace falta ejecutar ambos)

   Las fechas son RELATIVAS a la hora en que ejecutas el script:
   ejecútalo el mismo día (idealmente justo antes) de probar.

   Contraseña de todos los usuarios:  Demo123*

   Qué genera:
     • 2 docentes, 25 estudiantes (3 "reales" para la app + 22 mock)
     • 4 cursos (3 del docente demo, 1 de otro docente → prueba de aislamiento)
     • 6 semanas de sesiones CERRADAS con checkpoints, marcaciones,
       intentos rechazados y ajustes manuales (perfiles de asistencia
       alto / medio / bajo para ver alertas < 80 %)
     • HOY: sesiones en curso con checkpoints activos y programados
   ===================================================================== */
USE AsistenciaDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------
   1. Limpieza
   --------------------------------------------------------------------- */
DELETE FROM dbo.AjusteAsistencia;
DELETE FROM dbo.IntentoRechazado;
DELETE FROM dbo.RegistroCheckpoint;
DELETE FROM dbo.[Checkpoint];
DELETE FROM dbo.SesionClase;
DELETE FROM dbo.Matricula;
DELETE FROM dbo.Curso;
DELETE FROM dbo.Usuario;

DECLARE @AhoraUtc DATETIME2(0) = SYSUTCDATETIME();
DECLARE @HoyLocal DATE         = CAST(DATEADD(HOUR, -5, @AhoraUtc) AS DATE);   -- Ecuador UTC-5
DECLARE @Hash NVARCHAR(200)    =
    N'PBKDF2-SHA256$100000$ZXuPPMHuTJaxsmeO5XEUvw==$QY37UVL+l1y3cm321OHHzWpalUyrOIX5a+qbSREna60=';  -- Demo123*

/* ---------------------------------------------------------------------
   2. Usuarios
   --------------------------------------------------------------------- */
-- Docentes y los 3 estudiantes para probar con el celular (SIN dispositivo vinculado)
INSERT INTO dbo.Usuario (Identificacion, Nombres, Apellidos, Email, PasswordHash, Rol) VALUES
 (N'0900000001', N'María',  N'Espinoza', N'docente@demo.edu',  @Hash, N'Docente'),
 (N'0900000009', N'Jorge',  N'Villacís', N'docente2@demo.edu', @Hash, N'Docente'),
 (N'0900000002', N'Carlos', N'Andrade',  N'carlos@demo.edu',   @Hash, N'Estudiante'),
 (N'0900000003', N'Lucía',  N'Benítez',  N'lucia@demo.edu',    @Hash, N'Estudiante'),
 (N'0900000004', N'Diego',  N'Cedeño',   N'diego@demo.edu',    @Hash, N'Estudiante');

-- 22 estudiantes mock, vinculados a dispositivos ficticios "mock-<cédula>" (los usa el simulador)
INSERT INTO dbo.Usuario (Identificacion, Nombres, Apellidos, Email, PasswordHash, Rol, DeviceIdVinculado, FechaVinculacionUtc)
SELECT CONCAT(N'09500000', RIGHT(CONCAT(N'0', v.N), 2)),
       v.Nombres, v.Apellidos,
       CONCAT(N'est', RIGHT(CONCAT(N'0', v.N), 2), N'@demo.edu'),
       @Hash, N'Estudiante',
       CONCAT(N'mock-09500000', RIGHT(CONCAT(N'0', v.N), 2)),
       DATEADD(DAY, -50, @AhoraUtc)
FROM (VALUES
    ( 1, N'Andrea',    N'Mora'),       ( 2, N'Bryan',     N'Zambrano'),   ( 3, N'Camila',   N'Vera'),
    ( 4, N'Daniel',    N'Macías'),     ( 5, N'Emilia',    N'Castro'),     ( 6, N'Fernando', N'León'),
    ( 7, N'Gabriela',  N'Salazar'),    ( 8, N'Héctor',    N'Guerrero'),   ( 9, N'Isabel',   N'Paredes'),
    (10, N'Joel',      N'Navarrete'),  (11, N'Karla',     N'Alvarado'),   (12, N'Luis',     N'Montalvo'),
    (13, N'Mishell',   N'Rivera'),     (14, N'Nicolás',   N'Ortiz'),      (15, N'Paula',    N'Chávez'),
    (16, N'Ricardo',   N'Intriago'),   (17, N'Sofía',     N'Burgos'),     (18, N'Tomás',    N'Quimí'),
    (19, N'Valentina', N'Suárez'),     (20, N'Xavier',    N'Pincay'),     (21, N'Yuliana',  N'Torres'),
    (22, N'Zoe',       N'Carrasco')
) AS v(N, Nombres, Apellidos);

-- Perfil de asistencia de cada estudiante (probabilidad % de marcar un checkpoint)
CREATE TABLE #Perfil (UsuarioId INT PRIMARY KEY, N INT NOT NULL, Prob INT NOT NULL);
INSERT INTO #Perfil (UsuarioId, N, Prob)
SELECT u.UsuarioId, x.N,
       CASE u.Email
            WHEN N'carlos@demo.edu' THEN 97      -- alto
            WHEN N'lucia@demo.edu'  THEN 82      -- medio
            WHEN N'diego@demo.edu'  THEN 60      -- bajo (aparecerá en alerta)
            ELSE CASE WHEN x.N % 7 = 0 THEN 55   -- bajo
                      WHEN x.N % 5 = 0 THEN 76   -- medio
                      ELSE 94 END                -- alto
       END
FROM dbo.Usuario u
CROSS APPLY (SELECT CASE WHEN u.Identificacion LIKE N'095%'
                         THEN CAST(RIGHT(u.Identificacion, 2) AS INT)
                         ELSE 100 + CAST(RIGHT(u.Identificacion, 1) AS INT) END AS N) x   -- carlos=102, lucia=103, diego=104
WHERE u.Rol = N'Estudiante';

DECLARE @Doc1 INT = (SELECT UsuarioId FROM dbo.Usuario WHERE Email = N'docente@demo.edu');
DECLARE @Doc2 INT = (SELECT UsuarioId FROM dbo.Usuario WHERE Email = N'docente2@demo.edu');

/* ---------------------------------------------------------------------
   3. Cursos y matrículas
   --------------------------------------------------------------------- */
INSERT INTO dbo.Curso (Codigo, Nombre, Periodo, DocenteId, AulaLatitud, AulaLongitud, RadioMetros) VALUES
 (N'PROG-101', N'Programación Móvil con .NET MAUI', N'2026-2', @Doc1, -2.145800, -79.965200, 50),
 (N'BD-201',   N'Bases de Datos con SQL Server',    N'2026-2', @Doc1, NULL,       NULL,       50),
 (N'WEB-301',  N'Desarrollo Web con ASP.NET Core',  N'2026-2', @Doc1, -2.146300, -79.964800, 40),
 (N'MAT-110',  N'Matemática Discreta',              N'2026-2', @Doc2, -2.144900, -79.966100, 50);

DECLARE @Prog INT = (SELECT CursoId FROM dbo.Curso WHERE Codigo = N'PROG-101');
DECLARE @Bd   INT = (SELECT CursoId FROM dbo.Curso WHERE Codigo = N'BD-201');
DECLARE @Web  INT = (SELECT CursoId FROM dbo.Curso WHERE Codigo = N'WEB-301');
DECLARE @Mat  INT = (SELECT CursoId FROM dbo.Curso WHERE Codigo = N'MAT-110');

-- PROG-101: 18 est. | BD-201: 17 est. | WEB-301: 13 est. (Diego NO matriculado) | MAT-110: 12 est. (otro docente)
INSERT INTO dbo.Matricula (CursoId, EstudianteId, FechaUtc)
SELECT c.CursoId, p.UsuarioId, DATEADD(DAY, -60, @AhoraUtc)
FROM #Perfil p
CROSS JOIN dbo.Curso c
WHERE (c.CursoId = @Prog AND (p.N > 100 OR p.N % 3 <> 0))
   OR (c.CursoId = @Bd   AND (p.N > 100 OR p.N % 3 <> 1))
   OR (c.CursoId = @Web  AND (p.N IN (102, 103) OR (p.N < 100 AND p.N % 2 = 0)))
   OR (c.CursoId = @Mat  AND p.N BETWEEN 1 AND 12);

/* ---------------------------------------------------------------------
   4. Historial: 6 semanas de sesiones cerradas
      Lun/Mié PROG-101 presencial 07:00 (con geocerca)
      Mar/Jue BD-201   online     19:00
      Vie     WEB-301  presencial/online alternando semanas, 14:00
      Lun/Jue MAT-110  presencial 10:00 (otro docente)
   --------------------------------------------------------------------- */
;WITH Dias AS (
    SELECT TOP (42) DATEADD(DAY, -CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT), @HoyLocal) AS Fecha
    FROM sys.all_objects
), Calendario AS (
    SELECT Fecha,
           DATEDIFF(DAY, '19000101', Fecha) % 7       AS Dia,      -- 0 = lunes (1900-01-01 fue lunes)
           (DATEDIFF(DAY, '19000101', Fecha) / 7) % 2 AS SemanaPar
    FROM Dias
), Programa AS (
    SELECT c.Fecha, x.CursoId, x.HoraLocal, x.Doc, x.Url,
           CASE x.Modalidad WHEN N'Mixta'
                THEN CASE WHEN c.SemanaPar = 0 THEN N'Presencial' ELSE N'Online' END
                ELSE x.Modalidad END AS Modalidad,
           x.Geocerca
    FROM Calendario c
    JOIN (VALUES
        (0, @Prog, N'Presencial', 7,  @Doc1, CAST(NULL AS NVARCHAR(500)), 1),
        (2, @Prog, N'Presencial', 7,  @Doc1, NULL,                         1),
        (1, @Bd,   N'Online',     19, @Doc1, N'https://meet.google.com/abc-defg-hij', 0),
        (3, @Bd,   N'Online',     19, @Doc1, N'https://meet.google.com/abc-defg-hij', 0),
        (4, @Web,  N'Mixta',      14, @Doc1, N'https://teams.microsoft.com/l/meetup-join/demo', 1),
        (0, @Mat,  N'Presencial', 10, @Doc2, NULL,                         0),
        (3, @Mat,  N'Presencial', 10, @Doc2, NULL,                         0)
    ) AS x(Dia, CursoId, Modalidad, HoraLocal, Doc, Url, Geocerca) ON x.Dia = c.Dia
)
INSERT INTO dbo.SesionClase
    (CursoId, Tema, Modalidad, InicioUtc, FinUtc, Estado, UrlReunion, ValidarUbicacion, Latitud, Longitud, RadioMetros, CreadoPor, CreadoUtc)
SELECT p.CursoId,
       CHOOSE(DATEDIFF(DAY, '19000101', p.Fecha) % 6 + 1,
              N'Introducción y repaso', N'Taller práctico', N'Revisión de proyecto',
              N'Evaluación parcial', N'Laboratorio guiado', N'Exposiciones'),
       p.Modalidad,
       DATEADD(HOUR, p.HoraLocal + 5,     CAST(p.Fecha AS DATETIME2(0))),
       DATEADD(HOUR, p.HoraLocal + 5 + 2, CAST(p.Fecha AS DATETIME2(0))),
       N'Cerrada',
       CASE WHEN p.Modalidad = N'Online' THEN p.Url END,
       CASE WHEN p.Modalidad = N'Presencial' AND p.Geocerca = 1 THEN 1 ELSE 0 END,
       CASE WHEN p.Modalidad = N'Presencial' AND p.Geocerca = 1 THEN c.AulaLatitud  END,
       CASE WHEN p.Modalidad = N'Presencial' AND p.Geocerca = 1 THEN c.AulaLongitud END,
       CASE WHEN p.Modalidad = N'Presencial' AND p.Geocerca = 1 THEN c.RadioMetros  END,
       p.Doc,
       DATEADD(DAY, -2, CAST(p.Fecha AS DATETIME2(0)))
FROM Programa p
JOIN dbo.Curso c ON c.CursoId = p.CursoId;

-- Checkpoints del historial: presencial = 1 manual de 5 min; online = 3 automáticos de 60 s en minutos "aleatorios"
INSERT INTO dbo.[Checkpoint] (SesionId, Orden, Secreto, InicioUtc, FinUtc, RotacionSegundos, Origen, CreadoUtc)
SELECT s.SesionId, k.Orden, CRYPT_GEN_RANDOM(32), t.Inicio, DATEADD(SECOND, k.Dur, t.Inicio), 20, k.Origen, s.CreadoUtc
FROM dbo.SesionClase s
JOIN (VALUES
    (N'Presencial', 1,   5, 300, N'Manual'),
    (N'Online',     1,   8,  60, N'Automatico'),
    (N'Online',     2,  50,  60, N'Automatico'),
    (N'Online',     3, 100,  60, N'Automatico')
) AS k(Modalidad, Orden, Minuto, Dur, Origen) ON k.Modalidad = s.Modalidad
CROSS APPLY (SELECT DATEADD(MINUTE,
                 k.Minuto + CAST(ABS(CAST(CHECKSUM(s.SesionId, k.Orden) AS BIGINT)) % 10 AS INT),
                 s.InicioUtc) AS Inicio) t
WHERE s.Estado = N'Cerrada';

/* ---------------------------------------------------------------------
   5. Sesiones de HOY (relativas a la hora de ejecución)
   --------------------------------------------------------------------- */
DECLARE @S1 INT, @S2 INT, @S3 INT, @S4 INT, @S5 INT;

-- S1 · PROG-101 presencial EN CURSO, sin geocerca (se puede probar desde cualquier lugar)
--      checkpoint #1 ya cerrado (con marcas de los mock) + checkpoint #2 ACTIVO durante 2 horas
INSERT INTO dbo.SesionClase (CursoId, Tema, Modalidad, InicioUtc, FinUtc, Estado, ValidarUbicacion, CreadoPor, CreadoUtc)
VALUES (@Prog, N'PRUEBA · Presencial en curso (checkpoint activo 2 h)', N'Presencial',
        DATEADD(MINUTE, -30, @AhoraUtc), DATEADD(HOUR, 3, @AhoraUtc), N'Abierta', 0, @Doc1, DATEADD(HOUR, -1, @AhoraUtc));
SET @S1 = SCOPE_IDENTITY();
INSERT INTO dbo.[Checkpoint] (SesionId, Orden, Secreto, InicioUtc, FinUtc, RotacionSegundos, Origen) VALUES
 (@S1, 1, CRYPT_GEN_RANDOM(32), DATEADD(MINUTE, -25, @AhoraUtc), DATEADD(MINUTE, -20, @AhoraUtc), 20, N'Manual'),
 (@S1, 2, CRYPT_GEN_RANDOM(32), DATEADD(MINUTE, -1, @AhoraUtc),  DATEADD(HOUR, 2, @AhoraUtc),     20, N'Manual');

-- S2 · BD-201 online EN CURSO: #1 ya pasó, #2 se abre en ~12 min, #3 en ~70 min (automáticos de 60 s)
INSERT INTO dbo.SesionClase (CursoId, Tema, Modalidad, InicioUtc, FinUtc, Estado, UrlReunion, ValidarUbicacion, CreadoPor, CreadoUtc)
VALUES (@Bd, N'PRUEBA · Online en curso (checkpoints automáticos)', N'Online',
        DATEADD(MINUTE, -10, @AhoraUtc), DATEADD(HOUR, 3, @AhoraUtc), N'Abierta',
        N'https://meet.google.com/abc-defg-hij', 0, @Doc1, DATEADD(HOUR, -1, @AhoraUtc));
SET @S2 = SCOPE_IDENTITY();
INSERT INTO dbo.[Checkpoint] (SesionId, Orden, Secreto, InicioUtc, FinUtc, RotacionSegundos, Origen) VALUES
 (@S2, 1, CRYPT_GEN_RANDOM(32), DATEADD(MINUTE, -6, @AhoraUtc),  DATEADD(MINUTE, -5, @AhoraUtc), 20, N'Automatico'),
 (@S2, 2, CRYPT_GEN_RANDOM(32), DATEADD(MINUTE, 12, @AhoraUtc),  DATEADD(MINUTE, 13, @AhoraUtc), 20, N'Automatico'),
 (@S2, 3, CRYPT_GEN_RANDOM(32), DATEADD(MINUTE, 70, @AhoraUtc),  DATEADD(MINUTE, 71, @AhoraUtc), 20, N'Automatico');

-- S3 · WEB-301 presencial CON geocerca, programada para dentro de 1 h (sin checkpoints: lánzalo desde la web)
INSERT INTO dbo.SesionClase (CursoId, Tema, Modalidad, InicioUtc, FinUtc, Estado, ValidarUbicacion, Latitud, Longitud, RadioMetros, CreadoPor)
VALUES (@Web, N'PRUEBA · Presencial con geocerca', N'Presencial',
        DATEADD(HOUR, 1, @AhoraUtc), DATEADD(HOUR, 3, @AhoraUtc), N'Programada', 1, -2.146300, -79.964800, 40, @Doc1);
SET @S3 = SCOPE_IDENTITY();

-- S4 · PROG-101 mañana 07:00 (programada)
INSERT INTO dbo.SesionClase (CursoId, Tema, Modalidad, InicioUtc, FinUtc, Estado, ValidarUbicacion, Latitud, Longitud, RadioMetros, CreadoPor)
VALUES (@Prog, N'Laboratorio guiado', N'Presencial',
        DATEADD(HOUR, 12, CAST(DATEADD(DAY, 1, @HoyLocal) AS DATETIME2(0))),
        DATEADD(HOUR, 14, CAST(DATEADD(DAY, 1, @HoyLocal) AS DATETIME2(0))),
        N'Programada', 1, -2.145800, -79.965200, 50, @Doc1);
SET @S4 = SCOPE_IDENTITY();

-- S5 · MAT-110 (OTRO docente) en curso: el docente demo NO debe poder verla
INSERT INTO dbo.SesionClase (CursoId, Tema, Modalidad, InicioUtc, FinUtc, Estado, ValidarUbicacion, CreadoPor)
VALUES (@Mat, N'PRUEBA · Sesión de otro docente', N'Presencial',
        DATEADD(MINUTE, -15, @AhoraUtc), DATEADD(HOUR, 2, @AhoraUtc), N'Abierta', 0, @Doc2);
SET @S5 = SCOPE_IDENTITY();
INSERT INTO dbo.[Checkpoint] (SesionId, Orden, Secreto, InicioUtc, FinUtc, RotacionSegundos, Origen)
VALUES (@S5, 1, CRYPT_GEN_RANDOM(32), DATEADD(MINUTE, -10, @AhoraUtc), DATEADD(HOUR, 1, @AhoraUtc), 20, N'Manual');

/* ---------------------------------------------------------------------
   6. Marcaciones
      - Historial: todos los estudiantes según su perfil
      - Hoy: solo los mock en checkpoints ya terminados
        (Carlos, Lucía y Diego quedan libres para probar con el celular)
      En online, los de perfil bajo/medio "se van" de la clase: menor
      probabilidad en los checkpoints 2 y 3 → aparecen como Parcial.
   --------------------------------------------------------------------- */
INSERT INTO dbo.RegistroCheckpoint
    (CheckpointId, EstudianteId, FechaHoraUtc, Metodo, DeviceId, Latitud, Longitud, PrecisionMetros, DistanciaMetros, IpOrigen)
SELECT k.CheckpointId,
       m.EstudianteId,
       DATEADD(SECOND, CAST(h.H % DATEDIFF(SECOND, k.InicioUtc, k.FinUtc) AS INT), k.InicioUtc),
       CASE WHEN s.Modalidad = N'Online' AND h.H % 3 = 0 THEN N'Codigo' ELSE N'QR' END,
       COALESCE(u.DeviceIdVinculado, CONCAT(N'hist-', u.Identificacion)),
       CASE WHEN s.ValidarUbicacion = 1 THEN s.Latitud  + (h.H % 41 - 20) * 0.00001 END,
       CASE WHEN s.ValidarUbicacion = 1 THEN s.Longitud + (h.H % 37 - 18) * 0.00001 END,
       CASE WHEN s.ValidarUbicacion = 1 THEN 6 + h.H % 25 END,
       CASE WHEN s.ValidarUbicacion = 1
            THEN SQRT(SQUARE((h.H % 41 - 20) * 1.11) + SQUARE((h.H % 37 - 18) * 1.11)) END,
       CONCAT(N'10.0.0.', h.H % 250)
FROM dbo.[Checkpoint] k
JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
JOIN dbo.Matricula m   ON m.CursoId = s.CursoId AND m.Activa = 1
JOIN dbo.Usuario u     ON u.UsuarioId = m.EstudianteId
JOIN #Perfil p         ON p.UsuarioId = m.EstudianteId
CROSS APPLY (SELECT ABS(CAST(CHECKSUM(k.CheckpointId, m.EstudianteId, 7919) AS BIGINT)) AS H) h
WHERE k.Cancelado = 0
  AND k.FinUtc <= @AhoraUtc
  AND (s.Estado = N'Cerrada' OR u.DeviceIdVinculado LIKE N'mock-%')
  AND h.H % 100 < p.Prob - CASE WHEN s.Modalidad = N'Online' AND p.Prob < 80 THEN (k.Orden - 1) * 15 ELSE 0 END;

/* ---------------------------------------------------------------------
   7. Intentos rechazados (1 de cada 4 que no marcó lo intentó mal)
   --------------------------------------------------------------------- */
INSERT INTO dbo.IntentoRechazado (EstudianteId, CheckpointId, Metodo, CodigoError, Detalle, DeviceId, IpOrigen, FechaUtc)
SELECT m.EstudianteId, k.CheckpointId,
       CASE WHEN s.Modalidad = N'Online' THEN N'Codigo' ELSE N'QR' END,
       e.Codigo, e.Detalle,
       COALESCE(u.DeviceIdVinculado, CONCAT(N'hist-', u.Identificacion)),
       N'10.0.0.99',
       DATEADD(SECOND, 40, k.InicioUtc)
FROM dbo.[Checkpoint] k
JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
JOIN dbo.Matricula m   ON m.CursoId = s.CursoId AND m.Activa = 1
JOIN dbo.Usuario u     ON u.UsuarioId = m.EstudianteId
CROSS APPLY (SELECT ABS(CAST(CHECKSUM(k.CheckpointId, m.EstudianteId, 104729) AS BIGINT)) AS H) h
CROSS APPLY (SELECT
        CASE WHEN s.ValidarUbicacion = 1 THEN N'FUERA_DE_RANGO'
             WHEN s.Modalidad = N'Online' AND h.H % 3 = 0 THEN N'CODIGO_INVALIDO'
             WHEN s.Modalidad = N'Online' THEN N'TOKEN_EXPIRADO'
             ELSE N'QR_INVALIDO' END AS Codigo,
        CASE WHEN s.ValidarUbicacion = 1 THEN CONCAT(N'Estás a ', 80 + h.H % 900, N' m del aula (máximo 60 m).')
             WHEN s.Modalidad = N'Online' AND h.H % 3 = 0 THEN N'Código incorrecto o expirado.'
             WHEN s.Modalidad = N'Online' THEN N'El QR expiró.'
             ELSE N'El código QR no es válido para esta app.' END AS Detalle) e
WHERE s.Estado = N'Cerrada'
  AND h.H % 4 = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.RegistroCheckpoint r
                  WHERE r.CheckpointId = k.CheckpointId AND r.EstudianteId = m.EstudianteId);

/* ---------------------------------------------------------------------
   8. Ajustes manuales del docente
      - 1 de cada 3 ausencias totales → Justificado
      - 1 de cada 5 parciales online  → Presente (falla de conexión)
   --------------------------------------------------------------------- */
;WITH Marcas AS (
    SELECT s.SesionId, s.Modalidad, c.DocenteId, s.FinUtc, m.EstudianteId,
           (SELECT COUNT(*) FROM dbo.RegistroCheckpoint r
             JOIN dbo.[Checkpoint] k ON k.CheckpointId = r.CheckpointId
            WHERE k.SesionId = s.SesionId AND r.EstudianteId = m.EstudianteId) AS Validados,
           (SELECT COUNT(*) FROM dbo.[Checkpoint] k WHERE k.SesionId = s.SesionId AND k.Cancelado = 0) AS Total,
           ABS(CAST(CHECKSUM(s.SesionId, m.EstudianteId, 31) AS BIGINT)) AS H
    FROM dbo.SesionClase s
    JOIN dbo.Curso c     ON c.CursoId = s.CursoId
    JOIN dbo.Matricula m ON m.CursoId = s.CursoId AND m.Activa = 1
    WHERE s.Estado = N'Cerrada'
)
INSERT INTO dbo.AjusteAsistencia (SesionId, EstudianteId, Estado, Motivo, UsuarioId, FechaUtc)
SELECT SesionId, EstudianteId,
       CASE WHEN Validados = 0 THEN N'Justificado' ELSE N'Presente' END,
       CASE WHEN Validados = 0
            THEN CHOOSE(CAST(H % 3 AS INT) + 1,
                        N'Calamidad doméstica (documento entregado)',
                        N'Representación institucional: torneo deportivo',
                        N'Permiso firmado por coordinación')
            ELSE N'Falla de internet; confirmó presencia por chat' END,
       DocenteId,
       DATEADD(DAY, 1, FinUtc)
FROM Marcas
WHERE (Validados = 0 AND H % 3 = 0)
   OR (Modalidad = N'Online' AND Validados BETWEEN 1 AND Total - 1 AND H % 5 = 0);

COMMIT TRANSACTION;

/* ---------------------------------------------------------------------
   9. Resumen
   --------------------------------------------------------------------- */
PRINT CONCAT(N'S1 (presencial en curso, checkpoint activo) = SesionId ', @S1);
PRINT CONCAT(N'S2 (online en curso, automáticos)           = SesionId ', @S2);
PRINT CONCAT(N'S3 (presencial con geocerca, programada)    = SesionId ', @S3);
PRINT CONCAT(N'S4 (mañana)                                 = SesionId ', @S4);
PRINT CONCAT(N'S5 (otro docente, no visible)               = SesionId ', @S5);

SELECT N'Usuarios' AS Tabla, COUNT(*) AS Filas FROM dbo.Usuario
UNION ALL SELECT N'Cursos', COUNT(*) FROM dbo.Curso
UNION ALL SELECT N'Matrículas', COUNT(*) FROM dbo.Matricula
UNION ALL SELECT N'Sesiones', COUNT(*) FROM dbo.SesionClase
UNION ALL SELECT N'Checkpoints', COUNT(*) FROM dbo.[Checkpoint]
UNION ALL SELECT N'Marcaciones', COUNT(*) FROM dbo.RegistroCheckpoint
UNION ALL SELECT N'Intentos rechazados', COUNT(*) FROM dbo.IntentoRechazado
UNION ALL SELECT N'Ajustes manuales', COUNT(*) FROM dbo.AjusteAsistencia;

SELECT s.SesionId, c.Codigo, s.Tema, s.Modalidad, s.Estado,
       DATEADD(HOUR, -5, s.InicioUtc) AS InicioLocal,
       DATEADD(HOUR, -5, s.FinUtc)    AS FinLocal
FROM dbo.SesionClase s
JOIN dbo.Curso c ON c.CursoId = s.CursoId
WHERE s.SesionId IN (@S1, @S2, @S3, @S4, @S5)
ORDER BY s.SesionId;

DROP TABLE #Perfil;
GO
