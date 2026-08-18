using SaasVistoria.Application;

namespace SaasVistoria.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_AcceptsTheCorrectPassword()
    {
        var hash = PasswordHasher.Hash("Vistora@2026");
        Assert.True(PasswordHasher.Verify("Vistora@2026", hash));
    }

    [Fact]
    public void Verify_RejectsAWrongPassword()
    {
        var hash = PasswordHasher.Hash("Vistora@2026");
        Assert.False(PasswordHasher.Verify("senha-errada", hash));
    }

    [Fact]
    public void Hash_UsesADifferentSaltEachTime()
    {
        var a = PasswordHasher.Hash("mesma-senha");
        var b = PasswordHasher.Hash("mesma-senha");
        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verify("mesma-senha", a));
        Assert.True(PasswordHasher.Verify("mesma-senha", b));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-pontos-suficientes")]
    [InlineData("a.b")]
    [InlineData("abc.def.ghi.jkl")]
    [InlineData("nao-numero.c2FsdA==.a2V5")]
    public void Verify_RejectsAMalformedStoredHash(string malformed)
    {
        Assert.False(PasswordHasher.Verify("qualquer-senha", malformed));
    }
}
