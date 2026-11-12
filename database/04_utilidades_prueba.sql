/* =====================================================================
   04_utilidades_prueba.sql  —  Procedimientos y consultas para pruebas
   Esquema "demo": NO desplegar en producción.
   ===================================================================== */
USE AsistenciaDB;
GO
IF SCHEMA_ID('demo') IS NULL EXEC ('CREATE SCHEMA demo');
GO

/* ---------------------------------------------------------------------
   Abre (o reabre) un checkpoint AHORA en una sesión.
   Cancela el que esté activo. Útil para repetir pruebas sin la web.
     EXEC demo.sp_AbrirCheckpoint @SesionId = 1, @Minutos = 30;
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE demo.sp_AbrirCheckpoint
    @SesionId INT,
    @Minutos  INT = 30,
    @Rotacion SMALLINT = 20
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ahora DATETIME2(0) = SYSUTCDATETIME();

    UPDATE dbo.[Checkpoint] SET Cancelado = 1
    WHERE SesionId = @SesionId AND Cancelado = 0 AND InicioUtc <= @Ahora AND FinUtc > @Ahora;

    INSERT INTO dbo.[Checkpoint] (SesionId, Orden, Secreto, InicioUtc, FinUtc, RotacionSegundos, Origen)
    SELECT @SesionId, ISNULL(MAX(Orden), 0) + 1, CRYPT_GEN_RANDOM(32), @Ahora, DATEADD(MINUTE, @Minutos, @Ahora), @Rotacion, N'Manual'
    FROM dbo.[Checkpoint] WHERE SesionId = @SesionId;

    UPDATE dbo.SesionClase
    SET Estado = N'Abierta',
        FinUtc = CASE WHEN FinUtc < DATEADD(MINUTE, @Minutos, @Ahora) THEN DATEADD(MINUTE, @Minutos, @Ahora) ELSE FinUtc END
    WHERE SesionId = @SesionId;

    SELECT TOP 1 CheckpointId, Orden, InicioUtc, FinUtc FROM dbo.[Checkpoint]
    WHERE SesionId = @SesionId ORDER BY CheckpointId DESC;
END
GO

/* ---------------------------------------------------------------------
   Pone la geocerca de una sesión presencial en TU ubicación actual,
   para probar la validación GPS desde casa u oficina.
   (Obtén lat/long en Google Maps: clic derecho sobre tu ubicación)
     EXEC demo.sp_ConfigurarGeocerca @SesionId = 3, @Lat = -2.145800, @Lng = -79.965200, @Radio = 60;
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE demo.sp_ConfigurarGeocerca
    @SesionId INT,
    @Lat      DECIMAL(9,6),
    @Lng      DECIMAL(9,6),
    @Radio    INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.SesionClase
    SET ValidarUbicacion = 1, Latitud = @Lat, Longitud = @Lng, RadioMetros = @Radio
    WHERE SesionId = @SesionId AND Modalidad = N'Presencial';

    IF @@ROWCOUNT = 0 RAISERROR (N'La sesión no existe o no es presencial.', 16, 1);
END
GO

/* ---------------------------------------------------------------------
   Libera el celular de Carlos, Lucía y Diego (o de un correo puntual)
     EXEC demo.sp_DesvincularDemo;
     EXEC demo.sp_DesvincularDemo @Email = N'lucia@demo.edu';
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE demo.sp_DesvincularDemo
    @Email NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Usuario
    SET DeviceIdVinculado = NULL, FechaVinculacionUtc = NULL
    WHERE (@Email IS NULL AND Email IN (N'carlos@demo.edu', N'lucia@demo.edu', N'diego@demo.edu'))
       OR Email = @Email;
    SELECT Email, DeviceIdVinculado FROM dbo.Usuario WHERE Email LIKE N'%@demo.edu' AND Rol = N'Estudiante' AND Identificacion LIKE N'090%';
END
GO

/* ---------------------------------------------------------------------
   Estado general: checkpoints abiertos ahora y su avance
     EXEC demo.sp_Estado;
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE demo.sp_Estado
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ahora DATETIME2(0) = SYSUTCDATETIME();

    SELECT N'Checkpoints abiertos ahora' AS Seccion;
    SELECT s.SesionId, c.Codigo, s.Modalidad, k.CheckpointId, k.Orden,
           DATEDIFF(SECOND, @Ahora, k.FinUtc) AS SegundosRestantes,
           (SELECT COUNT(*) FROM dbo.RegistroCheckpoint r WHERE r.CheckpointId = k.CheckpointId) AS Registrados,
           (SELECT COUNT(*) FROM dbo.Matricula m WHERE m.CursoId = s.CursoId AND m.Activa = 1) AS Matriculados
    FROM dbo.[Checkpoint] k
    JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
    JOIN dbo.Curso c ON c.CursoId = s.CursoId
    WHERE k.Cancelado = 0 AND s.Estado <> N'Cerrada' AND k.InicioUtc <= @Ahora AND k.FinUtc > @Ahora;

    SELECT N'Próximos checkpoints automáticos' AS Seccion;
    SELECT s.SesionId, c.Codigo, k.Orden, DATEADD(HOUR, -5, k.InicioUtc) AS InicioLocal,
           DATEDIFF(MINUTE, @Ahora, k.InicioUtc) AS EnMinutos
    FROM dbo.[Checkpoint] k
    JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
    JOIN dbo.Curso c ON c.CursoId = s.CursoId
    WHERE k.Cancelado = 0 AND k.InicioUtc > @Ahora
    ORDER BY k.InicioUtc;

    SELECT N'Últimas 15 marcaciones' AS Seccion;
    SELECT TOP 15 DATEADD(HOUR, -5, r.FechaHoraUtc) AS HoraLocal, u.Email, c.Codigo, k.Orden, r.Metodo,
           r.DistanciaMetros, r.DeviceId
    FROM dbo.RegistroCheckpoint r
    JOIN dbo.Usuario u ON u.UsuarioId = r.EstudianteId
    JOIN dbo.[Checkpoint] k ON k.CheckpointId = r.CheckpointId
    JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
    JOIN dbo.Curso c ON c.CursoId = s.CursoId
    ORDER BY r.RegistroId DESC;

    SELECT N'Últimos 15 rechazos' AS Seccion;
    SELECT TOP 15 DATEADD(HOUR, -5, i.FechaUtc) AS HoraLocal, u.Email, i.CheckpointId, i.Metodo, i.CodigoError, i.Detalle
    FROM dbo.IntentoRechazado i
    JOIN dbo.Usuario u ON u.UsuarioId = i.EstudianteId
    ORDER BY i.IntentoId DESC;
END
GO

/* =====================================================================
   CONSULTAS DE VERIFICACIÓN (ejecutar por partes)
   ===================================================================== */

-- 1. Asistencia de las sesiones de hoy (lo mismo que muestra la web)
SELECT v.SesionId, v.Curso, v.Modalidad, v.Estudiante, v.Validados, v.TotalCheckpoints,
       v.EstadoCalculado, v.EstadoAjuste, v.EstadoFinal
FROM dbo.vw_AsistenciaSesion v
WHERE CAST(DATEADD(HOUR, -5, v.InicioUtc) AS DATE) = CAST(DATEADD(HOUR, -5, SYSUTCDATETIME()) AS DATE)
ORDER BY v.SesionId, v.Estudiante;

-- 2. Porcentaje por curso: bajo 80 % deberían quedar los perfiles bajos (Diego, est07, est14, est21)
--    y, en BD-201 (online), también los medios (est05, est15, est20)
SELECT c.Codigo, p.Estudiante, p.Sesiones, p.Presentes, p.Parciales, p.Ausentes, p.Justificados, p.Porcentaje
FROM dbo.vw_PorcentajeAsistencia p
JOIN dbo.Curso c ON c.CursoId = p.CursoId
ORDER BY c.Codigo, p.Porcentaje;

-- 3. Distribución de estados del historial (sanidad de los datos mock)
SELECT Curso, Modalidad, EstadoFinal, COUNT(*) AS Cantidad
FROM dbo.vw_AsistenciaSesion
WHERE EstadoSesion = N'Cerrada'
GROUP BY Curso, Modalidad, EstadoFinal
ORDER BY Curso, Modalidad, EstadoFinal;

-- 4. Integridad: no debe devolver filas
SELECT N'Marcación fuera de la ventana del checkpoint' AS Problema, r.RegistroId
FROM dbo.RegistroCheckpoint r JOIN dbo.[Checkpoint] k ON k.CheckpointId = r.CheckpointId
WHERE r.FechaHoraUtc < k.InicioUtc OR r.FechaHoraUtc > DATEADD(SECOND, 10, k.FinUtc)
UNION ALL
SELECT N'Marcación de estudiante no matriculado', r.RegistroId
FROM dbo.RegistroCheckpoint r
JOIN dbo.[Checkpoint] k ON k.CheckpointId = r.CheckpointId
JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
WHERE NOT EXISTS (SELECT 1 FROM dbo.Matricula m WHERE m.CursoId = s.CursoId AND m.EstudianteId = r.EstudianteId)
UNION ALL
SELECT N'Geocerca excedida', r.RegistroId
FROM dbo.RegistroCheckpoint r
JOIN dbo.[Checkpoint] k ON k.CheckpointId = r.CheckpointId
JOIN dbo.SesionClase s ON s.SesionId = k.SesionId
WHERE s.ValidarUbicacion = 1 AND r.DistanciaMetros > s.RadioMetros * 2;

-- 5. Estado en vivo
EXEC demo.sp_Estado;
