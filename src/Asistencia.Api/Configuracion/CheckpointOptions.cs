namespace Asistencia.Api.Configuracion;

public sealed class CheckpointOptions
{
    /// <summary>Cada cuántos segundos cambia el QR / código.</summary>
    public int RotacionSegundos { get; set; } = 20;

    /// <summary>Pasos anteriores aceptados (latencia de red + tiempo de escaneo).</summary>
    public int PasosTolerancia { get; set; } = 1;

    /// <summary>Segundos extra aceptados después del fin del checkpoint.</summary>
    public int GraciaSegundos { get; set; } = 10;

    public int DuracionOnlineSegundos { get; set; } = 60;
    public int DuracionPresencialSegundos { get; set; } = 300;

    /// <summary>Si el GPS reporta peor precisión que esto, se rechaza.</summary>
    public double PrecisionMaximaMetros { get; set; } = 100;
}
