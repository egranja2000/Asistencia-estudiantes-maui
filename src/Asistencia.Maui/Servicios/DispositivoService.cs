namespace Asistencia.Maui.Servicios;

/// <summary>
/// Identificador estable de la instalación. La API vincula 1 estudiante = 1 dispositivo.
/// iOS: el Keychain lo conserva aunque se reinstale la app.
/// Android: se pierde al desinstalar (el docente puede desvincular desde la web).
/// </summary>
public sealed class DispositivoService
{
    private const string Clave = "device_id";
    private string? _id;

    public async Task<string> ObtenerIdAsync()
    {
        if (_id is not null) return _id;
        _id = await SecureStorage.Default.GetAsync(Clave);
        if (string.IsNullOrEmpty(_id))
        {
            _id = Guid.NewGuid().ToString("N");
            await SecureStorage.Default.SetAsync(Clave, _id);
        }
        return _id;
    }
}
