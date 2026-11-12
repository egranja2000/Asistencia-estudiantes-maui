namespace Asistencia.Api.Modelos;

public sealed class CursoRow
{
    public int CursoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Periodo { get; set; } = "";
    public decimal? AulaLatitud { get; set; }
    public decimal? AulaLongitud { get; set; }
    public int RadioMetros { get; set; }
    public int Matriculados { get; set; }
    public int Sesiones { get; set; }
    public DateTime? ProximaSesionUtc { get; set; }
}
