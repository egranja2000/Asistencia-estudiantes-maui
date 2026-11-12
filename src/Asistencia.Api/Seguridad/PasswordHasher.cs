using System.Security.Cryptography;

namespace Asistencia.Api.Seguridad;

/// <summary>Hash de contraseñas PBKDF2-SHA256. Formato: PBKDF2-SHA256$iter$saltB64$hashB64</summary>
public sealed class PasswordHasher
{
    private const int Iteraciones = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256${Iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string password, string almacenado)
    {
        var partes = almacenado.Split('$');
        if (partes.Length != 4 || partes[0] != "PBKDF2-SHA256" || !int.TryParse(partes[1], out var iter))
            return false;

        var salt = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
