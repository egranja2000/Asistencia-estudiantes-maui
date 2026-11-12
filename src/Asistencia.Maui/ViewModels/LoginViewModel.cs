using Asistencia.Maui.Servicios;
using Asistencia.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Asistencia.Maui.ViewModels;

public partial class LoginViewModel(ApiClient api, SesionUsuario sesion, DispositivoService dispositivo) : ObservableObject
{
    [ObservableProperty] private string usuario = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private bool ocupado;
    [ObservableProperty] private string? error;

    [RelayCommand]
    private async Task IngresarAsync()
    {
        if (Ocupado) return;
        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Ingresa tu usuario y contraseña.";
            return;
        }

        Ocupado = true;
        Error = null;
        try
        {
            var deviceId = await dispositivo.ObtenerIdAsync();
            var r = await api.LoginAsync(new LoginRequest(Usuario.Trim(), Password, deviceId));
            await sesion.GuardarAsync(r);
            Password = "";
            await Shell.Current.GoToAsync("//inicio");
        }
        catch (Exception ex)
        {
            Error = MarcacionService.MensajeError(ex);
        }
        finally
        {
            Ocupado = false;
        }
    }
}
