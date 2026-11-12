using Asistencia.Shared.Constantes;
using Asistencia.Shared.Dtos;

namespace Asistencia.Maui.Views;

/// <summary>Pantalla grande de éxito/error tras marcar. Se muestra modal y se espera su cierre.</summary>
public partial class ResultadoPage : ContentPage
{
    private readonly TaskCompletionSource _cerrada = new();
    private bool _cerrando;

    public ResultadoPage(ResultadoMarcacion r)
    {
        InitializeComponent();

        // emerald-600 / rose-600 con su halo suave (paleta Tailwind, igual que el portal).
        var color = Color.FromArgb(r.Exito ? "#059669" : "#E11D48");
        Circulo.BackgroundColor = color;
        Halo.BackgroundColor = Color.FromArgb(r.Exito ? "#D1FAE5" : "#FFE4E6");
        BotonContinuar.BackgroundColor = r.Exito ? color : Color.FromArgb("#4F46E5");
        Icono.Text = r.Exito ? "✓" : "✕";
        Titulo.Text = r.Exito ? "¡Listo!" : "No se registró";
        Titulo.TextColor = color;
        Mensaje.Text = r.Mensaje;

        if (r.Exito)
        {
            var hora = r.FechaHoraUtc is DateTime f
                ? DateTime.SpecifyKind(f, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss")
                : "";
            Detalle.Text = $"{r.Curso}\n{r.Modalidad} · checkpoint #{r.CheckpointOrden} · {hora}";
            BotonContinuar.Text = "Continuar";
        }
        else
        {
            Detalle.Text = r.Codigo switch
            {
                CodigosResultado.TokenExpirado or CodigosResultado.CodigoInvalido => "Usa el código que está en pantalla en este momento.",
                CodigosResultado.FueraDeRango => "Debes estar dentro del aula para marcar.",
                CodigosResultado.UbicacionRequerida => "Activa la ubicación y el permiso de la app.",
                _ => ""
            };
            BotonContinuar.Text = "Intentar de nuevo";
        }
        CajaDetalle.IsVisible = !string.IsNullOrEmpty(Detalle.Text);

        try
        {
            if (r.Exito) Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(120));
        }
        catch { /* dispositivo sin vibración */ }
    }

    public static async Task MostrarAsync(ResultadoMarcacion r)
    {
        var pagina = new ResultadoPage(r);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        await pagina._cerrada.Task;
    }

    private async void OnContinuar(object? sender, EventArgs e) => await CerrarAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync();
        return true;
    }

    private async Task CerrarAsync()
    {
        if (_cerrando) return;
        _cerrando = true;
        await Navigation.PopModalAsync();
        _cerrada.TrySetResult();
    }
}
