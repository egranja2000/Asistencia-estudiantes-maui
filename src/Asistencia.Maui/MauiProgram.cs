using Asistencia.Maui.Servicios;
using Asistencia.Maui.ViewModels;
using Asistencia.Maui.Views;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace Asistencia.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader();

        QuitarSubrayadoDeEntradas();

        // Servicios
        builder.Services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = new Uri(Constantes.ApiUrl),
            Timeout = TimeSpan.FromSeconds(15)
        });
        builder.Services.AddSingleton<SesionUsuario>();
        builder.Services.AddSingleton<DispositivoService>();
        builder.Services.AddSingleton<UbicacionService>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<MarcacionService>();

        // ViewModels y páginas
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<InicioViewModel>();
        builder.Services.AddTransient<CodigoViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<InicioPage>();
        builder.Services.AddTransient<EscanerPage>();
        builder.Services.AddTransient<CodigoPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }

    /// <summary>
    /// Los Entry se dibujan dentro de un Border con estilo "CampoTexto"; se quita la
    /// línea inferior nativa de Android para que el campo se vea limpio y moderno.
    /// </summary>
    private static void QuitarSubrayadoDeEntradas()
    {
#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SinSubrayado", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif
    }
}
