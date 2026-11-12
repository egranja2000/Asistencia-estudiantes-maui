namespace Asistencia.Api.Modelos;

public sealed class UsuarioRow
{
    public int UsuarioId { get; set; }
    public string Identificacion { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = "";
    public string? DeviceIdVinculado { get; set; }
    public bool Activo { get; set; }
    public string NombreCompleto => $"{Nombres} {Apellidos}";
}
