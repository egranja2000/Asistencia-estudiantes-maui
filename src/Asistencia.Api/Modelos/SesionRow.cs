using Asistencia.Shared.Constantes;

namespace Asistencia.Api.Modelos;

public sealed class SesionRow
{
    public int SesionId { get; set; }
    public int CursoId { get; set; }
    public string Curso { get; set; } = "";
    public string CodigoCurso { get; set; } = "";
    public string? Tema { get; set; }
    public string Modalidad { get; set; } = "";
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public string Estado { get; set; } = "";
    public string? UrlReunion { get; set; }
    public bool ValidarUbicacion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public int? RadioMetros { get; set; }
    public int Matriculados { get; set; }
    public int Checkpoints { get; set; }
    public bool EsOnline => Modalidad == Modalidades.Online;
}
