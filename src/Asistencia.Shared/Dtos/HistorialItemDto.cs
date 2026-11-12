namespace Asistencia.Shared.Dtos;

public sealed record HistorialItemDto(
    int SesionId,
    DateTime InicioUtc,
    string Curso,
    string? Tema,
    string Modalidad,
    int Validados,
    int TotalCheckpoints,
    string EstadoFinal);
