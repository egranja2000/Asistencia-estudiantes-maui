using Asistencia.Api.Modelos;

namespace Asistencia.Api.ViewModels;

public sealed class DetalleCursoViewModel
{
    public required CursoRow Curso { get; init; }
    public required IReadOnlyList<SesionRow> Sesiones { get; init; }
    public required IReadOnlyList<PorcentajeFila> Porcentajes { get; init; }

    /// <summary>Valores iniciales del modal "Nueva sesión".</summary>
    public required CrearSesionViewModel NuevaSesion { get; init; }
}
