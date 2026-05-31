using app.variable;
using app.module.code;
using app.module.crypto;
using app.module.crypto.code;
using EngineProviders = global::app.module.code.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.crypto;

public class ProviderRegistryTests
{
    private EngineProviders _providers = null!;

    [Before(Test)]
    public void Setup()
    {
        _providers = new EngineProviders();
    }

    [Test]
    public async Task Get_NoRegistration_ReturnsError()
    {
        var result = _providers.Get<ICrypto>();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Register_Then_Get_ReturnsSameInstance()
    {
        var provider = new global::app.module.crypto.code.Default();
        _providers.Register<ICrypto>(provider);

        var result = _providers.Get<ICrypto>();
        await result.IsSuccess();
        await Assert.That(result.Value).IsSameReferenceAs(provider);
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsError()
    {
        var first = new global::app.module.crypto.code.Default();
        _providers.Register<ICrypto>(first);

        var second = new global::app.module.crypto.code.Default();
        var result = _providers.Register<ICrypto>(second);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    [Test]
    public async Task Has_NoRegistration_ReturnsFalse()
    {
        await Assert.That(_providers.Has<ICrypto>()).IsFalse();
    }

    [Test]
    public async Task Has_AfterRegistration_ReturnsTrue()
    {
        _providers.Register<ICrypto>(new global::app.module.crypto.code.Default());
        await Assert.That(_providers.Has<ICrypto>()).IsTrue();
    }

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        var first = new global::app.module.crypto.code.Default();
        _providers.Register<ICrypto>(first);
        var second = new NamedCryptoProvider("second");
        _providers.Register<ICrypto>(second);

        var removed = _providers.Remove<ICrypto>("second");
        await removed.IsSuccess();
        await _providers.Get<ICrypto>("second").IsFailure();
    }

    [Test]
    public async Task Remove_NoRegistration_ReturnsError()
    {
        var removed = _providers.Remove<ICrypto>("unknown");
        await removed.IsFailure();
        await Assert.That(removed.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task GetOrDefault_NoRegistration_ReturnsDefault()
    {
        var fallback = new global::app.module.crypto.code.Default();
        var result = _providers.GetOrDefault<ICrypto>(fallback);
        await Assert.That(result).IsSameReferenceAs(fallback);
    }

    [Test]
    public async Task GetOrDefault_WithRegistration_ReturnsRegistered()
    {
        var registered = new global::app.module.crypto.code.Default();
        var fallback = new NamedCryptoProvider("fallback");
        _providers.Register<ICrypto>(registered);

        var result = _providers.GetOrDefault<ICrypto>(fallback);
        await Assert.That(result).IsSameReferenceAs(registered);
    }

    private class NamedCryptoProvider : ICrypto
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }

        public NamedCryptoProvider(string name) { Name = name; }

        public global::app.data.@this<global::app.module.crypto.type.hash.@this> Hash(Hash action) => global::app.data.@this<global::app.module.crypto.type.hash.@this>.Ok(new global::app.module.crypto.type.hash.@this(new byte[32], "keccak256"), global::app.type.@this.Create("hash", kind: "keccak256"));
        public global::app.data.@this<bool> Verify(Verify action) => global::app.data.@this<bool>.Ok(true);
    }
}
