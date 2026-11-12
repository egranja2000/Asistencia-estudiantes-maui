using Asistencia.Maui.ViewModels;

namespace Asistencia.Maui.Views;

public partial class CodigoPage : ContentPage
{
    public CodigoPage(CodigoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        EntradaCodigo.Focus();
    }
}
