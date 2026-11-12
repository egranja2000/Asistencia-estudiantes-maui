namespace Asistencia.Shared.Dtos;

public sealed record LoginResponse(string Token, DateTime ExpiraUtc, int EstudianteId, string Nombre, string Identificacion);
