namespace Asistencia.Api.Modelos;

/// <summary>Todo lo necesario para validar una marcación contra un checkpoint.</summary>
public sealed class CheckpointContexto
{
    public int CheckpointId { get; set; }
    public int SesionId { get; set; }
    public byte Orden { get; set; }
    public byte[] Secreto { get; set; } = [];
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public short RotacionSegundos { get; set; }
    public bool Cancelado { get; set; }
    public string EstadoSesion { get; set; } = "";
    public string Modalidad { get; set; } = "";
    public bool ValidarUbicacion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public int? RadioMetros { get; set; }
    public string? Tema { get; set; }
    public int CursoId { get; set; }
    public string Curso { get; set; } = "";
}
