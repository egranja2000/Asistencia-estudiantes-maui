using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Asistencia.Shared.Constantes;
using Microsoft.AspNetCore.WebUtilities;

namespace Asistencia.Api.Seguridad;

/// <summary>
/// Genera y valida los tokens rotativos de un checkpoint (estilo TOTP).
///   paso   = unixSeconds / RotacionSegundos
///   QR     = ASIS1.{checkpointId}.{paso}.{base64url(HMAC(secreto, "qr|id|paso"))[0..16 bytes]}
///   Código = 6 dígitos por truncamiento dinámico de HMAC(secreto, "code|id|paso")  (RFC 4226)
/// El secreto es distinto por checkpoint y nunca sale del servidor.
/// </summary>
public sealed class CheckpointTokenService
{
    public static byte[] NuevoSecreto() => RandomNumberGenerator.GetBytes(32);

    public static long Paso(DateTimeOffset ahora, int rotacionSegundos) =>
        ahora.ToUnixTimeSeconds() / rotacionSegundos;

    public static int SegundosHastaSiguientePaso(DateTimeOffset ahora, int rotacionSegundos) =>
        rotacionSegundos - (int)(ahora.ToUnixTimeSeconds() % rotacionSegundos);

    public string GenerarPayloadQr(int checkpointId, long paso, byte[] secreto) =>
        string.Join(FormatoQr.Separador, FormatoQr.Prefijo, checkpointId, paso, Firma(checkpointId, paso, secreto));

    public string GenerarCodigo(int checkpointId, long paso, byte[] secreto)
    {
        var h = HMACSHA256.HashData(secreto, Encoding.UTF8.GetBytes($"code|{checkpointId}|{paso}"));
        var offset = h[^1] & 0x0F;
        var binario = ((h[offset] & 0x7F) << 24) | (h[offset + 1] << 16) | (h[offset + 2] << 8) | h[offset + 3];
        return (binario % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public static bool TryParsearQr(string? payload, out int checkpointId, out long paso, out string firma)
    {
        checkpointId = 0; paso = 0; firma = "";
        var partes = payload?.Trim().Split(FormatoQr.Separador);
        if (partes is not { Length: 4 } || partes[0] != FormatoQr.Prefijo) return false;
        if (!int.TryParse(partes[1], out checkpointId) || !long.TryParse(partes[2], out paso)) return false;
        firma = partes[3];
        return firma.Length is > 0 and < 64;
    }

    public bool FirmaValida(int checkpointId, long paso, byte[] secreto, string firma) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Firma(checkpointId, paso, secreto)),
            Encoding.ASCII.GetBytes(firma));

    public bool CodigoValido(int checkpointId, long paso, byte[] secreto, string codigo) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(GenerarCodigo(checkpointId, paso, secreto)),
            Encoding.ASCII.GetBytes(codigo));

    private static string Firma(int checkpointId, long paso, byte[] secreto)
    {
        var h = HMACSHA256.HashData(secreto, Encoding.UTF8.GetBytes($"qr|{checkpointId}|{paso}"));
        return WebEncoders.Base64UrlEncode(h, 0, 16);
    }
}
