namespace Asistencia.Api.Modelos;

public sealed class AsistenciaFila
{
    public int EstudianteId { get; set; }
    public string Identificacion { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public string? DeviceIdVinculado { get; set; }
    public int TotalCheckpoints { get; set; }
    public int Validados { get; set; }
    public DateTime? PrimeraMarcaUtc { get; set; }
    public string EstadoCalculado { get; set; } = "";
    public string? EstadoAjuste { get; set; }
    public string? MotivoAjuste { get; set; }
    public string EstadoFinal { get; set; } = "";
}
