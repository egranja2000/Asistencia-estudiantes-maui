using System.Globalization;

namespace Asistencia.Maui.Convertidores;

/// <summary>true si el valor es un texto no vacío (para mostrar mensajes de error).</summary>
public sealed class NoVacioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
