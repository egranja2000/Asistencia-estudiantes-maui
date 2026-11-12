using Asistencia.Api.Modelos;

namespace Asistencia.Api.ViewModels;

public sealed class DetalleSesionViewModel
{
    public required SesionRow Sesion { get; init; }
    public required IReadOnlyList<CheckpointRow> Checkpoints { get; init; }
    public required IReadOnlyList<AsistenciaFila> Asistencia { get; init; }
    public required IReadOnlyList<IntentoResumen> Intentos { get; init; }
    public int DuracionSugeridaSegundos { get; init; }
}
