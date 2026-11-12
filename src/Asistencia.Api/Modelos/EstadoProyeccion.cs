namespace Asistencia.Api.Modelos;

/// <summary>Estado de la pantalla de proyección (sin la imagen QR, que la arma el controlador).</summary>
public sealed record EstadoProyeccion(
    string EstadoSesion,
    bool Activo,
    int? CheckpointId = null,
    int? Orden = null,
    long? Paso = null,
    string? PayloadQr = null,
    string? Codigo = null,
    int SegundosToken = 0,
    int RotacionSegundos = 0,
    int SegundosCheckpoint = 0,
    int Registrados = 0,
    int Matriculados = 0);
