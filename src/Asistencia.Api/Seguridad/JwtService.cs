using System.Globalization;
using System.Text;
using Asistencia.Api.Configuracion;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Asistencia.Api.Seguridad;

public sealed class JwtService(IOptions<JwtOptions> opciones, TimeProvider reloj)
{
    private readonly JwtOptions _o = opciones.Value;

    public (string Token, DateTime ExpiraUtc) CrearTokenEstudiante(int estudianteId, string nombre, string deviceId)
    {
        var expira = reloj.GetUtcNow().UtcDateTime.AddDays(_o.DiasExpiracion);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _o.Emisor,
            Audience = _o.Audiencia,
            Expires = expira,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Clave)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = estudianteId.ToString(CultureInfo.InvariantCulture),
                ["name"] = nombre,
                ["rol"] = "Estudiante",
                ["dev"] = deviceId
            }
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
