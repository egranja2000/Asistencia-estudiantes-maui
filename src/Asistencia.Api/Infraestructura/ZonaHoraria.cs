namespace Asistencia.Api.Infraestructura;

/// <summary>Conversión UTC ↔ hora local de la institución.</summary>
public sealed class ZonaHoraria(IConfiguration configuration)
{
    private readonly TimeZoneInfo _tz =
        TimeZoneInfo.FindSystemTimeZoneById(configuration["ZonaHoraria"] ?? "America/Guayaquil");

    public DateTime ALocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz);

    public DateTime AUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz);

    public DateOnly HoyLocal(TimeProvider reloj) => DateOnly.FromDateTime(ALocal(reloj.GetUtcNow().UtcDateTime));
}
