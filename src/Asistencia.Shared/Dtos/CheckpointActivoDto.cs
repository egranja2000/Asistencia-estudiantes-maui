namespace Asistencia.Shared.Dtos;

/// <summary>Checkpoint abierto ahora en un curso del estudiante (no expone el secreto ni el código).</summary>
public sealed record CheckpointActivoDto(
    int CheckpointId,
    string Curso,
    string? Tema,
    string Modalidad,
    int Orden,
    int SegundosRestantes,
    bool YaRegistrado);
