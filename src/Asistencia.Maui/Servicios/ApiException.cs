namespace Asistencia.Maui.Servicios;

public sealed class ApiException(string mensaje) : Exception(mensaje);
