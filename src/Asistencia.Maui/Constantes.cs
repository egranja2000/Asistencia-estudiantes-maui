namespace Asistencia.Maui;

public static class Constantes
{
    /// <summary>
    /// URL de la API.
    /// - Emulador Android: 10.0.2.2 apunta al localhost de tu PC.
    /// - Simulador iOS:    localhost.
    /// - Celular físico:   usa la IP de tu PC en la red local (ej. http://192.168.1.50:5080/)
    ///                     o, mejor, la URL HTTPS publicada.
    /// </summary>
    public static string ApiUrl =>
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5080/"
            : "http://192.168.1.6:5080/";
#else
        // TODO producción: cambiar por la URL HTTPS publicada (https://asistencia.tuinstitucion.edu.ec/)
        "http://192.168.1.6:5080/";
#endif
}
