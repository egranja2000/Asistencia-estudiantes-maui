namespace Asistencia.Maui.Servicios;

public sealed class UbicacionService
{
    public async Task<(Location? Ubicacion, string? Error)> ObtenerAsync()
    {
        var estado = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var s = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return s == PermissionStatus.Granted ? s : await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        });
        if (estado != PermissionStatus.Granted)
            return (null, "Esta clase es presencial y requiere tu ubicación. Activa el permiso de ubicación.");

        try
        {
            var ubicacion = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8)));
            return ubicacion is null
                ? (null, "No se pudo obtener tu ubicación. Activa el GPS e intenta de nuevo.")
                : (ubicacion, null);
        }
        catch (FeatureNotEnabledException)
        {
            return (null, "El GPS está desactivado. Actívalo e intenta de nuevo.");
        }
        catch (Exception)
        {
            return (null, "No se pudo obtener tu ubicación.");
        }
    }
}
