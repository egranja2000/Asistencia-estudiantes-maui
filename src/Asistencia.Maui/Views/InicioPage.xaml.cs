using Asistencia.Maui.ViewModels;

namespace Asistencia.Maui.Views;

public partial class InicioPage : ContentPage
{
    private readonly InicioViewModel _vm;
    private IDispatcherTimer? _timer;

    public InicioPage(InicioViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CargarAsync();

        // Refresca cada 15 s para avisar de checkpoints automáticos que se abran durante la clase.
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(15);
        _timer.Tick += async (_, _) => await _vm.CargarAsync();
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        _timer = null;
        base.OnDisappearing();
    }
}
