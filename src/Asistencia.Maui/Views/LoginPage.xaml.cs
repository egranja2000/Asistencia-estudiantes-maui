using Asistencia.Maui.Servicios;
using Asistencia.Maui.ViewModels;

namespace Asistencia.Maui.Views;

public partial class LoginPage : ContentPage
{
    private readonly SesionUsuario _sesion;

    public LoginPage(LoginViewModel vm, SesionUsuario sesion)
    {
        InitializeComponent();
        BindingContext = vm;
        _sesion = sesion;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _sesion.CargarAsync();
        if (_sesion.EstaAutenticado)
            await Shell.Current.GoToAsync("//inicio");
    }
}
