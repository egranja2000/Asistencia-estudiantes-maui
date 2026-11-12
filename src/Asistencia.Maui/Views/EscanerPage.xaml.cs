using Asistencia.Maui.Servicios;
using Asistencia.Shared.Constantes;
using Asistencia.Shared.Dtos;
using ZXing.Net.Maui;

namespace Asistencia.Maui.Views;

public partial class EscanerPage : ContentPage
{
    private const string TextoGuia = "Apunta al código QR que muestra el docente";
    private readonly MarcacionService _marcacion;
    private int _procesando; // 0 = libre, 1 = validando (evita lecturas duplicadas)

    public EscanerPage(MarcacionService marcacion)
    {
        InitializeComponent();
        _marcacion = marcacion;
        Lector.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var permiso = await Permissions.RequestAsync<Permissions.Camera>();
        if (permiso != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Cámara", "Necesitamos acceso a la cámara para escanear el QR. " +
                                         "También puedes usar el código de 6 dígitos.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }
        Interlocked.Exchange(ref _procesando, 0);
        Lector.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        Lector.IsDetecting = false;
        Lector.IsTorchOn = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var valor = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(valor)) return;
        if (Interlocked.Exchange(ref _procesando, 1) == 1) return;

        MainThread.BeginInvokeOnMainThread(async () => await ProcesarAsync(valor));
    }

    private async Task ProcesarAsync(string valor)
    {
        Lector.IsDetecting = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        Estado.Text = "Validando…";
        try
        {
            ResultadoMarcacion r;
            if (!valor.StartsWith(FormatoQr.Prefijo + FormatoQr.Separador, StringComparison.Ordinal))
                r = ResultadoMarcacion.Falla(CodigosResultado.QrInvalido, "Este QR no es de asistencia.");
            else
            {
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* sin vibración */ }
                r = await _marcacion.MarcarAsync(MetodosMarcacion.QR, valor);
            }

            Indicador.IsVisible = Indicador.IsRunning = false;
            await ResultadoPage.MostrarAsync(r);

            if (r.Exito)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
        }
        catch (SesionExpiradaException)
        {
            await Navegacion.IrALoginAsync();
            return;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo marcar", MarcacionService.MensajeError(ex), "OK");
        }

        // Error: volver a escanear
        Indicador.IsVisible = Indicador.IsRunning = false;
        Estado.Text = TextoGuia;
        Interlocked.Exchange(ref _procesando, 0);
        Lector.IsDetecting = true;
    }

    private void OnLinterna(object? sender, EventArgs e) => Lector.IsTorchOn = !Lector.IsTorchOn;
}
