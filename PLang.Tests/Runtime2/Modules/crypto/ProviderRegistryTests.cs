using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class ProviderRegistryTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_providers_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    [Test]
    public async Task Get_NoRegistration_ReturnsError()
    {
        var result = _engine.Providers.Get<ICryptoProvider>();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Register_Then_Get_ReturnsSameInstance()
    {
        var provider = new DefaultProvider();
        _engine.Providers.Register<ICryptoProvider>(provider);

        var result = _engine.Providers.Get<ICryptoProvider>();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsSameReferenceAs(provider);
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsError()
    {
        var first = new DefaultProvider();
        _engine.Providers.Register<ICryptoProvider>(first);

        var second = new DefaultProvider();
        var result = _engine.Providers.Register<ICryptoProvider>(second);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    [Test]
    public async Task Has_NoRegistration_ReturnsFalse()
    {
        await Assert.That(_engine.Providers.Has<ICryptoProvider>()).IsFalse();
    }

    [Test]
    public async Task Has_AfterRegistration_ReturnsTrue()
    {
        _engine.Providers.Register<ICryptoProvider>(new DefaultProvider());
        await Assert.That(_engine.Providers.Has<ICryptoProvider>()).IsTrue();
    }

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        var first = new DefaultProvider();
        _engine.Providers.Register<ICryptoProvider>(first);
        var second = new NamedCryptoProvider("second");
        _engine.Providers.Register<ICryptoProvider>(second);

        var removed = _engine.Providers.Remove<ICryptoProvider>("second");
        await Assert.That(removed.Success).IsTrue();
        await Assert.That(_engine.Providers.Get<ICryptoProvider>("second").Success).IsFalse();
    }

    [Test]
    public async Task Remove_NoRegistration_ReturnsError()
    {
        var removed = _engine.Providers.Remove<ICryptoProvider>("unknown");
        await Assert.That(removed.Success).IsFalse();
        await Assert.That(removed.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task GetOrDefault_NoRegistration_ReturnsDefault()
    {
        var fallback = new DefaultProvider();
        var result = _engine.Providers.GetOrDefault<ICryptoProvider>(fallback);
        await Assert.That(result).IsSameReferenceAs(fallback);
    }

    [Test]
    public async Task GetOrDefault_WithRegistration_ReturnsRegistered()
    {
        var registered = new DefaultProvider();
        var fallback = new NamedCryptoProvider("fallback");
        _engine.Providers.Register<ICryptoProvider>(registered);

        var result = _engine.Providers.GetOrDefault<ICryptoProvider>(fallback);
        await Assert.That(result).IsSameReferenceAs(registered);
    }

    private class NamedCryptoProvider : ICryptoProvider
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public NamedCryptoProvider(string name) { Name = name; }

        public Data Hash(byte[] data, string algorithm) => Data.Ok(new byte[32]);
        public Data Verify(byte[] data, byte[] expectedHash, string algorithm) => Data.Ok(true);
    }
}
