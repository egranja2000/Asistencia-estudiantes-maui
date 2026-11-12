using Asistencia.Maui.Views;

namespace Asistencia.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(EscanerPage), typeof(EscanerPage));
        Routing.RegisterRoute(nameof(CodigoPage), typeof(CodigoPage));
    }
}
