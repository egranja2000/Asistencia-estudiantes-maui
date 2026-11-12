namespace Asistencia.Api.Configuracion;

public sealed class JwtOptions
{
    public string Emisor { get; set; } = "";
    public string Audiencia { get; set; } = "";
    public string Clave { get; set; } = "";
    public int DiasExpiracion { get; set; } = 30;
}
