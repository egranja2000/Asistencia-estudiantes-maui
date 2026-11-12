namespace Asistencia.Shared.Dtos;

public sealed record LoginRequest(string Usuario, string Password, string DeviceId);
