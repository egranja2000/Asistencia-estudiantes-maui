using Asistencia.Shared.Constantes;

namespace Asistencia.Shared.Dtos;

public sealed class MarcarRequest
{
    /// <summary>"QR" o "Codigo".</summary>
    public string Metodo { get; set; } = MetodosMarcacion.QR;

    /// <summary>Contenido del QR escaneado, o el código de 6 dígitos.</summary>
    public string Valor { get; set; } = "";

    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public double? PrecisionMetros { get; set; }

    /// <summary>Android: Location.IsFromMockProvider (apps de fake GPS).</summary>
    public bool UbicacionSimulada { get; set; }
}
