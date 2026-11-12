namespace Asistencia.Maui.Servicios;

public static class Navegacion
{
    public static async Task IrALoginAsync()
    {
        IPlatformApplication.Current?.Services.GetService<SesionUsuario>()?.Cerrar();
        await Shell.Current.GoToAsync("//login");
    }
}
