using System.Collections.ObjectModel;
using Asistencia.Maui.Servicios;
using Asistencia.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Asistencia.Maui.ViewModels;

public partial class InicioViewModel(ApiClient api, SesionUsuario sesion) : ObservableObject
{
    public ObservableCollection<CheckpointItem> Activos { get; } = [];
    public ObservableCollection<HistorialItem> Historial { get; } = [];

    private bool _cargando;

    [ObservableProperty] private string saludo = "";
    [ObservableProperty] private bool refrescando;
    [ObservableProperty] private bool hayActivos;
    [ObservableProperty] private string? mensaje;

    /// <summary>Pull-to-refresh.</summary>
    [RelayCommand]
    private async Task RefrescarAsync()
    {
        await CargarAsync();
        Refrescando = false;
    }

    public async Task CargarAsync()
    {
        if (_cargando) return;
        _cargando = true;
        Saludo = $"Hola, {sesion.Nombre?.Split(' ')[0] ?? "estudiante"}";
        try
        {
            var activosTask = api.CheckpointsActivosAsync();
            var historialTask = api.HistorialAsync(30);
            await Task.WhenAll(activosTask, historialTask);

            Activos.Clear();
            foreach (var c in activosTask.Result)
                Activos.Add(new CheckpointItem(
                    c.Curso,
                    $"{c.Modalidad} · checkpoint #{c.Orden} · cierra en {Math.Max(0, c.SegundosRestantes) / 60}:{Math.Max(0, c.SegundosRestantes) % 60:00}",
                    c.YaRegistrado));
            HayActivos = Activos.Count > 0;

            Historial.Clear();
            foreach (var h in historialTask.Result)
                Historial.Add(new HistorialItem(
                    DateTime.SpecifyKind(h.InicioUtc, DateTimeKind.Utc).ToLocalTime().ToString("ddd dd/MM HH:mm"),
                    h.Curso,
                    $"{h.Modalidad} · {h.Validados}/{h.TotalCheckpoints} checkpoints",
                    h.EstadoFinal));
            Mensaje = null;
        }
        catch (SesionExpiradaException)
        {
            await Navegacion.IrALoginAsync();
        }
        catch (Exception ex)
        {
            Mensaje = MarcacionService.MensajeError(ex);
        }
        finally
        {
            _cargando = false;
        }
    }

    [RelayCommand]
    private Task EscanearAsync() => Shell.Current.GoToAsync(nameof(EscanerPage));

    [RelayCommand]
    private Task IngresarCodigoAsync() => Shell.Current.GoToAsync(nameof(CodigoPage));

    [RelayCommand]
    private async Task SalirAsync()
    {
        if (await Shell.Current.DisplayAlertAsync("Cerrar sesión", "¿Deseas salir?", "Salir", "Cancelar"))
            await Navegacion.IrALoginAsync();
    }
}
