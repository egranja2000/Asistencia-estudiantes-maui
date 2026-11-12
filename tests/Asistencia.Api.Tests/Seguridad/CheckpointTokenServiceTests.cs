using Asistencia.Api.Seguridad;

namespace Asistencia.Api.Tests.Seguridad;

public class CheckpointTokenServiceTests
{
    private readonly CheckpointTokenService _servicio = new();
    private readonly byte[] _secreto = CheckpointTokenService.NuevoSecreto();

    [Fact]
    public void PayloadQr_GeneradoYParseado_TieneFirmaValida()
    {
        var qr = _servicio.GenerarPayloadQr(42, 1000, _secreto);

        Assert.True(CheckpointTokenService.TryParsearQr(qr, out var id, out var paso, out var firma));
        Assert.Equal(42, id);
        Assert.Equal(1000, paso);
        Assert.True(_servicio.FirmaValida(id, paso, _secreto, firma));
    }

    [Fact]
    public void FirmaValida_ConOtroSecreto_DevuelveFalse()
    {
        var qr = _servicio.GenerarPayloadQr(42, 1000, _secreto);
        Assert.True(CheckpointTokenService.TryParsearQr(qr, out var id, out var paso, out var firma));

        Assert.False(_servicio.FirmaValida(id, paso, CheckpointTokenService.NuevoSecreto(), firma));
    }

    [Fact]
    public void FirmaValida_ConOtroPaso_DevuelveFalse()
    {
        var qr = _servicio.GenerarPayloadQr(42, 1000, _secreto);
        Assert.True(CheckpointTokenService.TryParsearQr(qr, out var id, out _, out var firma));

        Assert.False(_servicio.FirmaValida(id, 1001, _secreto, firma));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OTRO.1.2.abc")]
    [InlineData("ASIS1.x.2.abc")]
    [InlineData("ASIS1.1.2")]
    public void TryParsearQr_ConPayloadInvalido_DevuelveFalse(string? payload)
    {
        Assert.False(CheckpointTokenService.TryParsearQr(payload, out _, out _, out _));
    }

    [Fact]
    public void Codigo_TieneSeisDigitosYSeValida()
    {
        var codigo = _servicio.GenerarCodigo(7, 500, _secreto);

        Assert.Matches("^[0-9]{6}$", codigo);
        Assert.True(_servicio.CodigoValido(7, 500, _secreto, codigo));
        Assert.False(_servicio.CodigoValido(7, 501, _secreto, codigo));
    }

    [Fact]
    public void Paso_CambiaCadaRotacion()
    {
        var t0 = DateTimeOffset.FromUnixTimeSeconds(1_000_000_000);

        Assert.Equal(CheckpointTokenService.Paso(t0, 20), CheckpointTokenService.Paso(t0.AddSeconds(19), 20));
        Assert.NotEqual(CheckpointTokenService.Paso(t0, 20), CheckpointTokenService.Paso(t0.AddSeconds(20), 20));
        Assert.Equal(20, CheckpointTokenService.SegundosHastaSiguientePaso(t0, 20));
    }
}
