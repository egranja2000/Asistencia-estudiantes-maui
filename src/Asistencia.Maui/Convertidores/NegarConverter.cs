using System.Globalization;

namespace Asistencia.Maui.Convertidores;

/// <summary>Invierte un bool.</summary>
public sealed class NegarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
