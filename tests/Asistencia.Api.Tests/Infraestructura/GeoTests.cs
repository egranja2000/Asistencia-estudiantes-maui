using Asistencia.Api.Infraestructura;

namespace Asistencia.Api.Tests.Infraestructura;

public class GeoTests
{
    [Fact]
    public void DistanciaMetros_MismoPunto_EsCero()
    {
        Assert.Equal(0, Geo.DistanciaMetros(-0.1807, -78.4678, -0.1807, -78.4678), precision: 6);
    }

    [Fact]
    public void DistanciaMetros_UnaMilesimaDeGradoDeLatitud_EsAproximadamente111Metros()
    {
        var d = Geo.DistanciaMetros(-0.1807, -78.4678, -0.1817, -78.4678);

        Assert.InRange(d, 110, 112);
    }
}
