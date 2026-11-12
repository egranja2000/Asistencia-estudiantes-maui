using System.ComponentModel.DataAnnotations;
using Asistencia.Api.Modelos;
using Asistencia.Shared.Constantes;

namespace Asistencia.Api.ViewModels;

public sealed class CrearSesionViewModel
{
    public int CursoId { get; set; }
    public string? NombreCurso { get; set; }

    [StringLength(200)]
    public string? Tema { get; set; }

    [Required]
    public string Modalidad { get; set; } = Modalidades.Presencial;

    [Required]
    public DateOnly Fecha { get; set; }

    [Required]
    public TimeOnly HoraInicio { get; set; } = new(7, 0);

    [Required]
    public TimeOnly HoraFin { get; set; } = new(9, 0);

    [Url(ErrorMessage = "URL no válida"), StringLength(500)]
    public string? UrlReunion { get; set; }

    public bool ValidarUbicacion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    [Range(10, 1000)]
    public int? RadioMetros { get; set; }

    /// <summary>Checkpoints que el sistema lanza en momentos aleatorios (recomendado en Online).</summary>
    [Range(0, 5)]
    public int CheckpointsAutomaticos { get; set; }

    [Range(20, 1800)]
    public int DuracionCheckpointSegundos { get; set; } = 60;

    [Range(10, 120)]
    public int RotacionSegundos { get; set; } = 20;

    /// <summary>Valores iniciales del formulario para un curso (fecha de hoy y geocerca del aula).</summary>
    public static CrearSesionViewModel Para(CursoRow curso, DateOnly hoy) => new()
    {
        CursoId = curso.CursoId,
        NombreCurso = curso.Nombre,
        Fecha = hoy,
        Latitud = curso.AulaLatitud,
        Longitud = curso.AulaLongitud,
        RadioMetros = curso.RadioMetros,
        ValidarUbicacion = curso.AulaLatitud is not null
    };
}
