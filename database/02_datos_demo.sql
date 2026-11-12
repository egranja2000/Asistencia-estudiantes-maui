/* =====================================================================
   Datos de demostración
   Contraseña de todos los usuarios demo:  Demo123*
   (hash PBKDF2-SHA256, 100.000 iteraciones, mismo formato que PasswordHasher.cs)
   ===================================================================== */
USE AsistenciaDB;
GO
SET NOCOUNT ON;

INSERT INTO dbo.Usuario (Identificacion, Nombres, Apellidos, Email, PasswordHash, Rol) VALUES
 (N'0900000001', N'María',  N'Docente Demo', N'docente@demo.edu',
  N'PBKDF2-SHA256$100000$ZXuPPMHuTJaxsmeO5XEUvw==$QY37UVL+l1y3cm321OHHzWpalUyrOIX5a+qbSREna60=', N'Docente'),
 (N'0900000002', N'Carlos', N'Andrade',      N'carlos@demo.edu',
  N'PBKDF2-SHA256$100000$F9HkMThrYcw2qD31Vh7q8Q==$61bTARnacp867BJRZIz70JB6LCHecUPSZFfmbc3hk+Q=', N'Estudiante'),
 (N'0900000003', N'Lucía',  N'Benítez',      N'lucia@demo.edu',
  N'PBKDF2-SHA256$100000$0j2t5x9L89s6O/OvbXngBQ==$Xu4jwUriH7TTC17poHMDZgoNjEMuMQ8+kjNAGeDC+y0=', N'Estudiante'),
 (N'0900000004', N'Diego',  N'Cedeño',       N'diego@demo.edu',
  N'PBKDF2-SHA256$100000$xQ6poC5FAyWNvJNgLyvQ6w==$KNFrpXW6D97xFE843tPk0+1VTm52wxK//BaV4pL3O9w=', N'Estudiante');

DECLARE @Docente INT = (SELECT UsuarioId FROM dbo.Usuario WHERE Email = N'docente@demo.edu');

-- Coordenadas de ejemplo (Guayaquil). Cámbialas por las del aula real.
INSERT INTO dbo.Curso (Codigo, Nombre, Periodo, DocenteId, AulaLatitud, AulaLongitud, RadioMetros)
VALUES (N'PROG-101', N'Programación Móvil con .NET MAUI', N'2026-2', @Docente, -2.145800, -79.965200, 50);

DECLARE @Curso INT = SCOPE_IDENTITY();

INSERT INTO dbo.Matricula (CursoId, EstudianteId)
SELECT @Curso, UsuarioId FROM dbo.Usuario WHERE Rol = N'Estudiante';

PRINT 'Datos demo cargados. Usuario docente: docente@demo.edu / Demo123*';
GO
