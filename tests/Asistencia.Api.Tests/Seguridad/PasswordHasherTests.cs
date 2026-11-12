using Asistencia.Api.Seguridad;

namespace Asistencia.Api.Tests.Seguridad;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verificar_ConLaMismaPassword_DevuelveTrue()
    {
        var hash = _hasher.Hash("Demo123*");

        Assert.True(_hasher.Verificar("Demo123*", hash));
    }

    [Fact]
    public void Verificar_ConPasswordDistinta_DevuelveFalse()
    {
        var hash = _hasher.Hash("Demo123*");

        Assert.False(_hasher.Verificar("otra", hash));
    }

    [Fact]
    public void Hash_UsaSaltAleatorio()
    {
        Assert.NotEqual(_hasher.Hash("Demo123*"), _hasher.Hash("Demo123*"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("SHA1$1$a$b")]
    public void Verificar_ConFormatoInvalido_DevuelveFalse(string almacenado)
    {
        Assert.False(_hasher.Verificar("Demo123*", almacenado));
    }
}
