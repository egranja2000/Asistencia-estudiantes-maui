using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Asistencia.Api.Extensiones;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Español (Ecuador) para fechas, pero con punto decimal para que lat/long
    /// se lean igual que en el navegador.
    /// </summary>
    public static WebApplication UseCulturaInstitucion(this WebApplication app)
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-EC").Clone();
        cultura.NumberFormat.NumberDecimalSeparator = ".";
        cultura.NumberFormat.NumberGroupSeparator = ",";

        var localizacion = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(cultura),
            SupportedCultures = [cultura],
            SupportedUICultures = [cultura]
        };
        localizacion.RequestCultureProviders.Clear();
        app.UseRequestLocalization(localizacion);
        return app;
    }
}
