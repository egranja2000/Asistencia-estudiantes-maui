using Asistencia.Shared.Constantes;
using Asistencia.Shared.Dtos;

namespace Asistencia.Maui.Servicios;

/// <summary>
/// Marca asistencia. Si la clase es presencial con geocerca, la API responde UBICACION_REQUERIDA;
/// solo entonces se pide el GPS y se reenvía (en clases online nunca se solicita la ubicación).
/// </summary>
public sealed class MarcacionService(ApiClient api, UbicacionService ubicacion)
{
    public async Task<ResultadoMarcacion> MarcarAsync(string metodo, string valor)
    {
        var req = new MarcarRequest { Metodo = metodo, Valor = valor.Trim() };
        var r = await api.MarcarAsync(req);
        if (r.Codigo != CodigosResultado.UbicacionRequerida) return r;

        var (loc, error) = await ubicacion.ObtenerAsync();
        if (loc is null) return ResultadoMarcacion.Falla(CodigosResultado.UbicacionRequerida, error!);

        req.Latitud = loc.Latitude;
        req.Longitud = loc.Longitude;
        req.PrecisionMetros = loc.Accuracy;
        req.UbicacionSimulada = loc.IsFromMockProvider;
        return await api.MarcarAsync(req);
    }

    /// <summary>Convierte excepciones de red/API en un mensaje para el usuario.</summary>
    public static string MensajeError(Exception ex) => ex switch
    {
        ApiException a => a.Message,
        HttpRequestException or TaskCanceledException => "No hay conexión con el servidor. Revisa tu internet e intenta de nuevo.",
        _ => "Ocurrió un error inesperado."
    };
}
