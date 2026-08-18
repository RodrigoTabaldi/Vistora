using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Tests;

public class TokenServiceTests
{
    private static readonly AppUser User = new(Guid.NewGuid(), Guid.NewGuid(), "Mariana Costa", "admin@atelierimoveis.com.br", "Administrador", "hash-nao-usado-aqui");

    private static IConfiguration ConfigWithKey(string? key) =>
        new ConfigurationBuilder().AddInMemoryCollection(key is null ? [] : new Dictionary<string, string?> { ["Jwt:Key"] = key }).Build();

    [Fact]
    public void CreateThenValidate_RoundTripsToTheSameUserId()
    {
        var service = new TokenService(ConfigWithKey("chave-de-teste-com-32-caracteres-ou-mais"), new FakeEnv(Environments.Production));
        var token = service.Create(User);
        Assert.Equal(User.Id, service.ValidateAndGetUserId(token.AccessToken));
    }

    [Fact]
    public void ValidateAndGetUserId_RejectsATokenSignedWithADifferentKey()
    {
        var issuer = new TokenService(ConfigWithKey("chave-A-com-pelo-menos-32-caracteres!!"), new FakeEnv(Environments.Production));
        var verifier = new TokenService(ConfigWithKey("chave-B-completamente-diferente-32-chr"), new FakeEnv(Environments.Production));
        var token = issuer.Create(User);
        Assert.Null(verifier.ValidateAndGetUserId(token.AccessToken));
    }

    [Fact]
    public void ValidateAndGetUserId_RejectsATamperedPayload()
    {
        var service = new TokenService(ConfigWithKey("chave-de-teste-com-32-caracteres-ou-mais"), new FakeEnv(Environments.Production));
        var token = service.Create(User);
        var parts = token.AccessToken.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}x.{parts[2]}"; // payload alterado, assinatura antiga mantida
        Assert.Null(service.ValidateAndGetUserId(tampered));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-um-jwt")]
    [InlineData("a.b")]
    public void ValidateAndGetUserId_RejectsMalformedInput(string? malformed)
    {
        var service = new TokenService(ConfigWithKey("chave-de-teste-com-32-caracteres-ou-mais"), new FakeEnv(Environments.Production));
        Assert.Null(service.ValidateAndGetUserId(malformed));
    }

    [Fact]
    public void Constructor_ThrowsOutsideDevelopment_WhenJwtKeyIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() => new TokenService(ConfigWithKey(null), new FakeEnv(Environments.Production)));
    }

    [Fact]
    public void Constructor_ThrowsOutsideDevelopment_WhenJwtKeyIsTooShort()
    {
        Assert.Throws<InvalidOperationException>(() => new TokenService(ConfigWithKey("curta-demais"), new FakeEnv(Environments.Staging)));
    }

    [Fact]
    public void Constructor_FallsBackToARandomEphemeralKey_InDevelopment_WhenJwtKeyIsMissing()
    {
        // Não deve lançar em Development, e o serviço resultante ainda cria/valida tokens normalmente
        // (a chave é apenas aleatória por execução, nunca um valor fixo gravado no código-fonte).
        var service = new TokenService(ConfigWithKey(null), new FakeEnv(Environments.Development));
        var token = service.Create(User);
        Assert.Equal(User.Id, service.ValidateAndGetUserId(token.AccessToken));
    }

    [Fact]
    public void TwoDevelopmentInstancesWithoutJwtKey_UseDifferentEphemeralKeys()
    {
        var a = new TokenService(ConfigWithKey(null), new FakeEnv(Environments.Development));
        var b = new TokenService(ConfigWithKey(null), new FakeEnv(Environments.Development));
        var token = a.Create(User);
        Assert.Null(b.ValidateAndGetUserId(token.AccessToken));
    }

    private sealed class FakeEnv(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SaasVistoria.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
