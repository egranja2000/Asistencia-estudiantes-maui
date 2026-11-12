using Asistencia.Maui.Servicios;
using Asistencia.Maui.Views;
using Asistencia.Shared.Constantes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Asistencia.Maui.ViewModels;

public partial class CodigoViewModel(MarcacionService marcacion) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarcarCommand))]
    private string codigo = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarcarCommand))]
    private bool ocupado;

    private bool PuedeMarcar() => !Ocupado && Codigo.Count(char.IsDigit) == 6;

    [RelayCommand(CanExecute = nameof(PuedeMarcar))]
    private async Task MarcarAsync()
    {
        Ocupado = true;
        try
        {
            var limpio = new string(Codigo.Where(char.IsDigit).ToArray());
            var r = await marcacion.MarcarAsync(MetodosMarcacion.Codigo, limpio);
            await ResultadoPage.MostrarAsync(r);
            if (r.Exito)
                await Shell.Current.GoToAsync("..");
            else
                Codigo = "";
        }
        catch (SesionExpiradaException)
        {
            await Navegacion.IrALoginAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("No se pudo marcar", MarcacionService.MensajeError(ex), "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private Task CerrarAsync() => Shell.Current.GoToAsync("..");
}
