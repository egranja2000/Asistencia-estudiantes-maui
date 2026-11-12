using Asistencia.Shared.Dtos;

namespace Asistencia.Maui.Servicios;

/// <summary>Token JWT y datos del estudiante guardados en el dispositivo.</summary>
public sealed class SesionUsuario
{
    private const string ClaveToken = "jwt";
    private const string ClaveExpira = "jwt_expira";
    private const string ClaveNombre = "nombre";

    private string? _token;
    private DateTime _expiraUtc;
    private bool _cargada;

    public string? Nombre { get; private set; }

    public bool EstaAutenticado => _token is not null && _expiraUtc > DateTime.UtcNow;

    public string? Token => EstaAutenticado ? _token : null;

    public async Task CargarAsync()
    {
        if (_cargada) return;
        _token = await SecureStorage.Default.GetAsync(ClaveToken);
        _expiraUtc = new DateTime(Preferences.Default.Get(ClaveExpira, 0L), DateTimeKind.Utc);
        Nombre = Preferences.Default.Get<string?>(ClaveNombre, null);
        _cargada = true;
    }

    public async Task GuardarAsync(LoginResponse r)
    {
        _token = r.Token;
        _expiraUtc = DateTime.SpecifyKind(r.ExpiraUtc, DateTimeKind.Utc);
        Nombre = r.Nombre;
        await SecureStorage.Default.SetAsync(ClaveToken, r.Token);
        Preferences.Default.Set(ClaveExpira, _expiraUtc.Ticks);
        Preferences.Default.Set(ClaveNombre, r.Nombre);
        _cargada = true;
    }

    public void Cerrar()
    {
        _token = null;
        Nombre = null;
        SecureStorage.Default.Remove(ClaveToken);
        Preferences.Default.Remove(ClaveExpira);
        Preferences.Default.Remove(ClaveNombre);
    }
}
