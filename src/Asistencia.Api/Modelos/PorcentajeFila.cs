namespace Asistencia.Api.Modelos;

public sealed class PorcentajeFila
{
    public int EstudianteId { get; set; }
    public string Identificacion { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public int Sesiones { get; set; }
    public int Presentes { get; set; }
    public int Parciales { get; set; }
    public int Ausentes { get; set; }
    public int Justificados { get; set; }
    public decimal? Porcentaje { get; set; }
}
