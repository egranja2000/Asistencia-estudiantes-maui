using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Asistencia.Shared.Dtos;

namespace Asistencia.Maui.Servicios;

public sealed class ApiClient(HttpClient http, SesionUsuario sesion)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        using var resp = await http.PostAsJsonAsync("api/auth/login", req, Json);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            throw new ApiException("Demasiados intentos. Espera un minuto.");
        throw new ApiException(await LeerErrorAsync(resp));
    }

    public Task<List<CheckpointActivoDto>> CheckpointsActivosAsync() =>
        EnviarAsync<List<CheckpointActivoDto>>(HttpMethod.Get, "api/estudiante/checkpoints-activos");

    public Task<List<HistorialItemDto>> HistorialAsync(int dias = 30) =>
        EnviarAsync<List<HistorialItemDto>>(HttpMethod.Get, $"api/estudiante/historial?dias={dias}");

    public Task<ResultadoMarcacion> MarcarAsync(MarcarRequest req) =>
        EnviarAsync<ResultadoMarcacion>(HttpMethod.Post, "api/estudiante/marcar", req);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo = null)
    {
        using var msg = new HttpRequestMessage(metodo, url);
        if (sesion.Token is { } token)
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null)
            msg.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json);

        using var resp = await http.SendAsync(msg);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new SesionExpiradaException();
        if (resp.StatusCode == HttpStatusCode.TooManyRequests) throw new ApiException("Demasiados intentos. Espera un minuto.");
        if (!resp.IsSuccessStatusCode) throw new ApiException(await LeerErrorAsync(resp));
        return (await resp.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string> LeerErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var e = await resp.Content.ReadFromJsonAsync<ErrorResponse>(Json);
            if (!string.IsNullOrWhiteSpace(e?.Mensaje)) return e.Mensaje;
        }
        catch { /* respuesta sin JSON */ }
        return $"Error del servidor ({(int)resp.StatusCode}).";
    }
}
