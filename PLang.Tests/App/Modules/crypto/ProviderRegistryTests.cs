using App.Engine.Variables;
using App.Engine.Providers;
using App.modules.crypto;
using App.modules.crypto.providers;
using EngineProviders = App.Engine.Providers.@this;
using PLangEngine = App.Engine.@this;

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
        var result = _providers.Get<ICryptoProvider>();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Register_Then_Get_ReturnsSameInstance()
    {
        var provider = new DefaultCryptoProvider();
        _providers.Register<ICryptoProvider>(provider);

        var result = _providers.Get<ICryptoProvider>();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsSameReferenceAs(provider);
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsError()
    {
        var first = new DefaultCryptoProvider();
        _providers.Register<ICryptoProvider>(first);

        var second = new DefaultCryptoProvider();
        var result = _providers.Register<ICryptoProvider>(second);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    [Test]
    public async Task Has_NoRegistration_ReturnsFalse()
    {
        await Assert.That(_providers.Has<ICryptoProvider>()).IsFalse();
    }

    [Test]
    public async Task Has_AfterRegistration_ReturnsTrue()
    {
        _providers.Register<ICryptoProvider>(new DefaultCryptoProvider());
        await Assert.That(_providers.Has<ICryptoProvider>()).IsTrue();
    }

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        var first = new DefaultCryptoProvider();
        _providers.Register<ICryptoProvider>(first);
        var second = new NamedCryptoProvider("second");
        _providers.Register<ICryptoProvider>(second);

        var removed = _providers.Remove<ICryptoProvider>("second");
        await Assert.That(removed.Success).IsTrue();
        await Assert.That(_providers.Get<ICryptoProvider>("second").Success).IsFalse();
    }

    [Test]
    public async Task Remove_NoRegistration_ReturnsError()
    {
        var removed = _providers.Remove<ICryptoProvider>("unknown");
        await Assert.That(removed.Success).IsFalse();
        await Assert.That(removed.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task GetOrDefault_NoRegistration_ReturnsDefault()
    {
        var fallback = new DefaultCryptoProvider();
        var result = _providers.GetOrDefault<ICryptoProvider>(fallback);
        await Assert.That(result).IsSameReferenceAs(fallback);
    }

    [Test]
    public async Task GetOrDefault_WithRegistration_ReturnsRegistered()
    {
        var registered = new DefaultCryptoProvider();
        var fallback = new NamedCryptoProvider("fallback");
        _providers.Register<ICryptoProvider>(registered);

        var result = _providers.GetOrDefault<ICryptoProvider>(fallback);
        await Assert.That(result).IsSameReferenceAs(registered);
    }

    private class NamedCryptoProvider : ICryptoProvider
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public NamedCryptoProvider(string name) { Name = name; }

        public Data Hash(Hash action) => Data.Ok(new byte[32]);
        public Data Verify(Verify action) => Data.Ok(true);
    }
}
