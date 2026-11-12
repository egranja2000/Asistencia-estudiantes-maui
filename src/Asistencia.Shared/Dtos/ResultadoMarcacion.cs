namespace Asistencia.Shared.Dtos;

public sealed record ResultadoMarcacion(
    bool Exito,
    string Codigo,
    string Mensaje,
    string? Curso = null,
    string? Tema = null,
    string? Modalidad = null,
    int? CheckpointOrden = null,
    DateTime? FechaHoraUtc = null)
{
    public static ResultadoMarcacion Falla(string codigo, string mensaje) => new(false, codigo, mensaje);
}
