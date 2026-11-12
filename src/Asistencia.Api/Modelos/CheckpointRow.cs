namespace Asistencia.Api.Modelos;

public sealed class CheckpointRow
{
    public int CheckpointId { get; set; }
    public byte Orden { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public short RotacionSegundos { get; set; }
    public string Origen { get; set; } = "";
    public bool Cancelado { get; set; }
    public int Registrados { get; set; }
}
