using PLang.Runtime2.Engine.Memory;
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
    public async Task Get_NoRegistration_ReturnsNull()
    {
        var result = _engine.Providers.Get<ICryptoProvider>();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Register_Then_Get_ReturnsSameInstance()
    {
        var provider = new DefaultProvider();
        _engine.Providers.Register<ICryptoProvider>(provider);

        var result = _engine.Providers.Get<ICryptoProvider>();
        await Assert.That(result).IsSameReferenceAs(provider);
    }

    [Test]
    public async Task Register_Overwrites_PreviousRegistration()
    {
        var first = new DefaultProvider();
        var second = new DefaultProvider();
        _engine.Providers.Register<ICryptoProvider>(first);
        _engine.Providers.Register<ICryptoProvider>(second);

        var result = _engine.Providers.Get<ICryptoProvider>();
        await Assert.That(result).IsSameReferenceAs(second);
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
    public async Task Remove_AfterRegistration_ReturnsTrue_And_GetReturnsNull()
    {
        _engine.Providers.Register<ICryptoProvider>(new DefaultProvider());

        var removed = _engine.Providers.Remove<ICryptoProvider>();
        await Assert.That(removed).IsTrue();
        await Assert.That(_engine.Providers.Get<ICryptoProvider>()).IsNull();
    }

    [Test]
    public async Task Remove_NoRegistration_ReturnsFalse()
    {
        var removed = _engine.Providers.Remove<ICryptoProvider>();
        await Assert.That(removed).IsFalse();
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
        var fallback = new DefaultProvider();
        _engine.Providers.Register<ICryptoProvider>(registered);

        var result = _engine.Providers.GetOrDefault<ICryptoProvider>(fallback);
        await Assert.That(result).IsSameReferenceAs(registered);
    }
}
