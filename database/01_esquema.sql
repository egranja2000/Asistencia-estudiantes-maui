/* =====================================================================
   AsistenciaDB - Esquema
   Registro de asistencia con checkpoints dinámicos (QR + código 6 dígitos)
   Modalidad Presencial / Online
   Compatible con SQL Server 2019+
   Todas las fechas se guardan en UTC (DATETIME2).
   ===================================================================== */

IF DB_ID('AsistenciaDB') IS NULL
    CREATE DATABASE AsistenciaDB;
GO
USE AsistenciaDB;
GO

/* ---------------------------------------------------------------------
   Usuario: docentes, estudiantes y administradores
   DeviceIdVinculado: un estudiante = un dispositivo (antifraude)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Usuario (
    UsuarioId            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuario PRIMARY KEY,
    Identificacion       NVARCHAR(20)  NOT NULL,
    Nombres              NVARCHAR(100) NOT NULL,
    Apellidos            NVARCHAR(100) NOT NULL,
    Email                NVARCHAR(150) NOT NULL,
    PasswordHash         NVARCHAR(200) NOT NULL,
    Rol                  NVARCHAR(15)  NOT NULL
        CONSTRAINT CK_Usuario_Rol CHECK (Rol IN ('Docente','Estudiante','Admin')),
    DeviceIdVinculado    NVARCHAR(64)  NULL,
    FechaVinculacionUtc  DATETIME2(0)  NULL,
    Activo               BIT           NOT NULL CONSTRAINT DF_Usuario_Activo DEFAULT (1),
    CreadoUtc            DATETIME2(0)  NOT NULL CONSTRAINT DF_Usuario_Creado DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Usuario_Email          UNIQUE (Email),
    CONSTRAINT UQ_Usuario_Identificacion UNIQUE (Identificacion)
);
GO
-- Un mismo dispositivo no puede estar vinculado a dos usuarios
CREATE UNIQUE INDEX UX_Usuario_DeviceId
    ON dbo.Usuario (DeviceIdVinculado)
    WHERE DeviceIdVinculado IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   Curso: la ubicación del aula es la geocerca por defecto de sus sesiones
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Curso (
    CursoId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Curso PRIMARY KEY,
    Codigo         NVARCHAR(20)  NOT NULL,
    Nombre         NVARCHAR(150) NOT NULL,
    Periodo        NVARCHAR(20)  NOT NULL,
    DocenteId      INT           NOT NULL CONSTRAINT FK_Curso_Docente REFERENCES dbo.Usuario(UsuarioId),
    AulaLatitud    DECIMAL(9,6)  NULL,
    AulaLongitud   DECIMAL(9,6)  NULL,
    RadioMetros    INT           NOT NULL CONSTRAINT DF_Curso_Radio DEFAULT (50),
    Activo         BIT           NOT NULL CONSTRAINT DF_Curso_Activo DEFAULT (1),
    CONSTRAINT UQ_Curso_Codigo_Periodo UNIQUE (Codigo, Periodo)
);
GO
CREATE INDEX IX_Curso_Docente ON dbo.Curso (DocenteId) WHERE Activo = 1;
GO

CREATE TABLE dbo.Matricula (
    CursoId        INT          NOT NULL CONSTRAINT FK_Matricula_Curso REFERENCES dbo.Curso(CursoId),
    EstudianteId   INT          NOT NULL CONSTRAINT FK_Matricula_Estudiante REFERENCES dbo.Usuario(UsuarioId),
    FechaUtc       DATETIME2(0) NOT NULL CONSTRAINT DF_Matricula_Fecha DEFAULT (SYSUTCDATETIME()),
    Activa         BIT          NOT NULL CONSTRAINT DF_Matricula_Activa DEFAULT (1),
    CONSTRAINT PK_Matricula PRIMARY KEY (CursoId, EstudianteId)
);
GO
CREATE INDEX IX_Matricula_Estudiante ON dbo.Matricula (EstudianteId) INCLUDE (Activa);
GO

/* ---------------------------------------------------------------------
   SesionClase: una clase concreta de un curso (una fecha)
   Estado: Programada -> Abierta (al lanzar el primer checkpoint) -> Cerrada
   --------------------------------------------------------------------- */
CREATE TABLE dbo.SesionClase (
    SesionId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SesionClase PRIMARY KEY,
    CursoId           INT           NOT NULL CONSTRAINT FK_Sesion_Curso REFERENCES dbo.Curso(CursoId),
    Tema              NVARCHAR(200) NULL,
    Modalidad         NVARCHAR(10)  NOT NULL
        CONSTRAINT CK_Sesion_Modalidad CHECK (Modalidad IN ('Presencial','Online')),
    InicioUtc         DATETIME2(0)  NOT NULL,
    FinUtc            DATETIME2(0)  NOT NULL,
    Estado            NVARCHAR(12)  NOT NULL CONSTRAINT DF_Sesion_Estado DEFAULT ('Programada')
        CONSTRAINT CK_Sesion_Estado CHECK (Estado IN ('Programada','Abierta','Cerrada')),
    UrlReunion        NVARCHAR(500) NULL,       -- Online: Zoom / Teams / Meet
    ValidarUbicacion  BIT           NOT NULL CONSTRAINT DF_Sesion_ValUbic DEFAULT (0), -- Presencial: geocerca
    Latitud           DECIMAL(9,6)  NULL,
    Longitud          DECIMAL(9,6)  NULL,
    RadioMetros       INT           NULL,
    CreadoPor         INT           NOT NULL CONSTRAINT FK_Sesion_Usuario REFERENCES dbo.Usuario(UsuarioId),
    CreadoUtc         DATETIME2(0)  NOT NULL CONSTRAINT DF_Sesion_Creado DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Sesion_Horario CHECK (FinUtc > InicioUtc),
    CONSTRAINT CK_Sesion_Geocerca CHECK (
        ValidarUbicacion = 0
        OR (Modalidad = 'Presencial' AND Latitud IS NOT NULL AND Longitud IS NOT NULL AND RadioMetros > 0))
);
GO
CREATE INDEX IX_Sesion_Curso_Inicio ON dbo.SesionClase (CursoId, InicioUtc DESC);
GO

/* ---------------------------------------------------------------------
   Checkpoint: ventana de tiempo en la que se puede marcar.
   - Presencial: normalmente 1 checkpoint (al inicio).
   - Online: 2-3 checkpoints manuales o automáticos (aleatorios).
   Secreto: clave HMAC de 32 bytes. Nunca sale del servidor.
   RotacionSegundos: cada cuántos segundos cambia el QR y el código.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.[Checkpoint] (
    CheckpointId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Checkpoint PRIMARY KEY,
    SesionId          INT            NOT NULL CONSTRAINT FK_Checkpoint_Sesion REFERENCES dbo.SesionClase(SesionId),
    Orden             TINYINT        NOT NULL,
    Secreto           VARBINARY(32)  NOT NULL,
    InicioUtc         DATETIME2(0)   NOT NULL,
    FinUtc            DATETIME2(0)   NOT NULL,
    RotacionSegundos  SMALLINT       NOT NULL CONSTRAINT DF_Checkpoint_Rotacion DEFAULT (20),
    Origen            NVARCHAR(10)   NOT NULL
        CONSTRAINT CK_Checkpoint_Origen CHECK (Origen IN ('Manual','Automatico')),
    Cancelado         BIT            NOT NULL CONSTRAINT DF_Checkpoint_Cancelado DEFAULT (0),
    CreadoUtc         DATETIME2(0)   NOT NULL CONSTRAINT DF_Checkpoint_Creado DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Checkpoint_Sesion_Orden UNIQUE (SesionId, Orden),
    CONSTRAINT CK_Checkpoint_Ventana CHECK (FinUtc > InicioUtc),
    CONSTRAINT CK_Checkpoint_Rotacion CHECK (RotacionSegundos BETWEEN 10 AND 120)
);
GO
CREATE INDEX IX_Checkpoint_Ventana ON dbo.[Checkpoint] (InicioUtc, FinUtc)
    INCLUDE (SesionId, Cancelado, RotacionSegundos);
GO

/* ---------------------------------------------------------------------
   RegistroCheckpoint: una marcación válida de un estudiante
   - Un estudiante marca una sola vez por checkpoint.
   - Un dispositivo marca una sola vez por checkpoint (evita que un
     celular marque por varios compañeros).
   --------------------------------------------------------------------- */
CREATE TABLE dbo.RegistroCheckpoint (
    RegistroId        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistroCheckpoint PRIMARY KEY,
    CheckpointId      INT            NOT NULL CONSTRAINT FK_Registro_Checkpoint REFERENCES dbo.[Checkpoint](CheckpointId),
    EstudianteId      INT            NOT NULL CONSTRAINT FK_Registro_Estudiante REFERENCES dbo.Usuario(UsuarioId),
    FechaHoraUtc      DATETIME2(3)   NOT NULL,
    Metodo            NVARCHAR(10)   NOT NULL
        CONSTRAINT CK_Registro_Metodo CHECK (Metodo IN ('QR','Codigo')),
    DeviceId          NVARCHAR(64)   NOT NULL,
    Latitud           DECIMAL(9,6)   NULL,
    Longitud          DECIMAL(9,6)   NULL,
    PrecisionMetros   DECIMAL(8,2)   NULL,
    DistanciaMetros   DECIMAL(10,2)  NULL,
    IpOrigen          NVARCHAR(45)   NULL,
    CONSTRAINT UQ_Registro_Estudiante UNIQUE (CheckpointId, EstudianteId),
    CONSTRAINT UQ_Registro_Device     UNIQUE (CheckpointId, DeviceId)
);
GO
CREATE INDEX IX_Registro_Estudiante ON dbo.RegistroCheckpoint (EstudianteId, CheckpointId);
GO

/* ---------------------------------------------------------------------
   IntentoRechazado: auditoría de intentos fallidos (detección de fraude)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.IntentoRechazado (
    IntentoId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IntentoRechazado PRIMARY KEY,
    EstudianteId   INT            NOT NULL,
    CheckpointId   INT            NULL,
    Metodo         NVARCHAR(10)   NOT NULL,
    CodigoError    NVARCHAR(40)   NOT NULL,
    Detalle        NVARCHAR(300)  NULL,
    DeviceId       NVARCHAR(64)   NULL,
    IpOrigen       NVARCHAR(45)   NULL,
    FechaUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_Intento_Fecha DEFAULT (SYSUTCDATETIME())
);
GO
CREATE INDEX IX_Intento_Checkpoint ON dbo.IntentoRechazado (CheckpointId, FechaUtc);
GO

/* ---------------------------------------------------------------------
   AjusteAsistencia: corrección manual del docente (justificado,
   problema técnico, etc.). Tiene prioridad sobre el estado calculado.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.AjusteAsistencia (
    SesionId       INT            NOT NULL CONSTRAINT FK_Ajuste_Sesion REFERENCES dbo.SesionClase(SesionId),
    EstudianteId   INT            NOT NULL CONSTRAINT FK_Ajuste_Estudiante REFERENCES dbo.Usuario(UsuarioId),
    Estado         NVARCHAR(12)   NOT NULL
        CONSTRAINT CK_Ajuste_Estado CHECK (Estado IN ('Presente','Parcial','Ausente','Justificado')),
    Motivo         NVARCHAR(300)  NULL,
    UsuarioId      INT            NOT NULL CONSTRAINT FK_Ajuste_Usuario REFERENCES dbo.Usuario(UsuarioId),
    FechaUtc       DATETIME2(0)   NOT NULL CONSTRAINT DF_Ajuste_Fecha DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AjusteAsistencia PRIMARY KEY (SesionId, EstudianteId)
);
GO

/* =====================================================================
   VISTAS
   ===================================================================== */

/* Estado de cada estudiante matriculado en cada sesión.
   Regla (configurable aquí):
     - Sin checkpoints iniciados aún  -> Pendiente
     - Validó todos                   -> Presente
     - Validó al menos uno            -> Parcial
     - Ninguno                        -> Ausente
   El ajuste manual del docente reemplaza el estado calculado. */
CREATE OR ALTER VIEW dbo.vw_AsistenciaSesion
AS
SELECT
    s.SesionId,
    s.CursoId,
    c.Nombre                         AS Curso,
    s.Tema,
    s.Modalidad,
    s.InicioUtc,
    s.Estado                         AS EstadoSesion,
    m.EstudianteId,
    u.Identificacion,
    u.Apellidos + N' ' + u.Nombres   AS Estudiante,
    u.DeviceIdVinculado,
    cp.Total                         AS TotalCheckpoints,
    r.Validados,
    r.PrimeraMarcaUtc,
    calc.EstadoCalculado,
    a.Estado                         AS EstadoAjuste,
    a.Motivo                         AS MotivoAjuste,
    COALESCE(a.Estado, calc.EstadoCalculado) AS EstadoFinal
FROM dbo.SesionClase s
JOIN dbo.Curso      c ON c.CursoId = s.CursoId
JOIN dbo.Matricula  m ON m.CursoId = s.CursoId AND m.Activa = 1
JOIN dbo.Usuario    u ON u.UsuarioId = m.EstudianteId
CROSS APPLY (
    SELECT COUNT(*) AS Total
    FROM dbo.[Checkpoint] k
    WHERE k.SesionId = s.SesionId AND k.Cancelado = 0 AND k.InicioUtc <= SYSUTCDATETIME()
) cp
CROSS APPLY (
    SELECT COUNT(*) AS Validados, MIN(rc.FechaHoraUtc) AS PrimeraMarcaUtc
    FROM dbo.RegistroCheckpoint rc
    JOIN dbo.[Checkpoint] k ON k.CheckpointId = rc.CheckpointId
    WHERE k.SesionId = s.SesionId AND k.Cancelado = 0 AND rc.EstudianteId = m.EstudianteId
) r
CROSS APPLY (
    SELECT CASE
        WHEN cp.Total = 0              THEN N'Pendiente'
        WHEN r.Validados >= cp.Total   THEN N'Presente'
        WHEN r.Validados > 0           THEN N'Parcial'
        ELSE N'Ausente'
    END AS EstadoCalculado
) calc
LEFT JOIN dbo.AjusteAsistencia a
       ON a.SesionId = s.SesionId AND a.EstudianteId = m.EstudianteId;
GO

/* Porcentaje de asistencia por estudiante en un curso (solo sesiones cerradas).
   Presente y Justificado = 1, Parcial = 0.5, Ausente = 0 (ajustable). */
CREATE OR ALTER VIEW dbo.vw_PorcentajeAsistencia
AS
SELECT
    v.CursoId,
    v.EstudianteId,
    v.Identificacion,
    v.Estudiante,
    COUNT(*)                                                     AS Sesiones,
    SUM(CASE WHEN v.EstadoFinal = N'Presente'    THEN 1 ELSE 0 END) AS Presentes,
    SUM(CASE WHEN v.EstadoFinal = N'Parcial'     THEN 1 ELSE 0 END) AS Parciales,
    SUM(CASE WHEN v.EstadoFinal = N'Ausente'     THEN 1 ELSE 0 END) AS Ausentes,
    SUM(CASE WHEN v.EstadoFinal = N'Justificado' THEN 1 ELSE 0 END) AS Justificados,
    CAST(100.0 * SUM(CASE v.EstadoFinal
                        WHEN N'Presente'    THEN 1.0
                        WHEN N'Justificado' THEN 1.0
                        WHEN N'Parcial'     THEN 0.5
                        ELSE 0 END) / NULLIF(COUNT(*), 0) AS DECIMAL(5,2)) AS Porcentaje
FROM dbo.vw_AsistenciaSesion v
WHERE v.EstadoSesion = N'Cerrada'
GROUP BY v.CursoId, v.EstudianteId, v.Identificacion, v.Estudiante;
GO
