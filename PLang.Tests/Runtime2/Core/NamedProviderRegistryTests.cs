using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Core;

/// <summary>
/// Tests the upgraded Engine.Providers named registry.
/// </summary>
public class NamedProviderRegistryTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_registry_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Register & Retrieve

    [Test]
    public async Task Register_SingleProvider_CanRetrieveByName()
    {
        var provider = new MockSigningProvider("ed25519");
        _engine.Providers.Register<ISigningProvider>(provider);

        var result = _engine.Providers.Get<ISigningProvider>("ed25519");
        await Assert.That(result).IsSameReferenceAs(provider);
        await Assert.That(result!.Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task Register_FirstProvider_BecomesDefault()
    {
        var provider = new MockSigningProvider("first");
        _engine.Providers.Register<ISigningProvider>(provider);

        await Assert.That(provider.IsDefault).IsTrue();
    }

    [Test]
    public async Task Register_MultipleProviders_EachRetrievableByName()
    {
        var ed = new MockSigningProvider("ed25519");
        var ec = new MockSigningProvider("ecdsa");
        _engine.Providers.Register<ISigningProvider>(ed);
        _engine.Providers.Register<ISigningProvider>(ec);

        await Assert.That(_engine.Providers.Get<ISigningProvider>("ed25519")).IsSameReferenceAs(ed);
        await Assert.That(_engine.Providers.Get<ISigningProvider>("ecdsa")).IsSameReferenceAs(ec);
    }

    [Test]
    public async Task Register_SecondProvider_DoesNotOverrideDefault()
    {
        var first = new MockSigningProvider("first");
        var second = new MockSigningProvider("second");
        _engine.Providers.Register<ISigningProvider>(first);
        _engine.Providers.Register<ISigningProvider>(second);

        await Assert.That(first.IsDefault).IsTrue();
        await Assert.That(second.IsDefault).IsFalse();
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsProviderExistsError()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));
        var result = _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    #endregion

    #region Get

    [Test]
    public async Task Get_DefaultProvider_ReturnsIsDefaultTrue()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("first"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("second"));

        var result = _engine.Providers.Get<ISigningProvider>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.IsDefault).IsTrue();
        await Assert.That(result.Name).IsEqualTo("first");
    }

    [Test]
    public async Task Get_NoneRegistered_ReturnsNull()
    {
        var result = _engine.Providers.Get<ISigningProvider>();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Get_ByName_NonExistent_ReturnsNull()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));
        var result = _engine.Providers.Get<ISigningProvider>("ecdsa");
        await Assert.That(result).IsNull();
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefaultProvider_Succeeds()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("first"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("second"));

        var result = _engine.Providers.Remove<ISigningProvider>("second");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(_engine.Providers.Get<ISigningProvider>("second")).IsNull();
        await Assert.That(_engine.Providers.Get<ISigningProvider>("first")).IsNotNull();
    }

    [Test]
    public async Task Remove_DefaultProvider_ReturnsCannotRemoveDefaultError()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));

        var result = _engine.Providers.Remove<ISigningProvider>("ed25519");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("CannotRemoveDefault");
    }

    [Test]
    public async Task Remove_NonExistent_ReturnsProviderNotFoundError()
    {
        var result = _engine.Providers.Remove<ISigningProvider>("unknown");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region SetDefault

    [Test]
    public async Task SetDefault_ExistingProvider_BecomesDefault()
    {
        var first = new MockSigningProvider("first");
        var second = new MockSigningProvider("second");
        _engine.Providers.Register<ISigningProvider>(first);
        _engine.Providers.Register<ISigningProvider>(second);

        var result = _engine.Providers.SetDefault<ISigningProvider>("second");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(second.IsDefault).IsTrue();
        await Assert.That(first.IsDefault).IsFalse();
    }

    [Test]
    public async Task SetDefault_NonExistent_ReturnsProviderNotFoundError()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));

        var result = _engine.Providers.SetDefault<ISigningProvider>("unknown");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllProvidersOfType()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("a"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("b"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("c"));

        var list = _engine.Providers.List<ISigningProvider>();
        await Assert.That(list.Count).IsEqualTo(3);
    }

    [Test]
    public async Task List_AllInterfaces_ReturnsProvidersAcrossTypes()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));
        _engine.Providers.Register<PLang.Runtime2.modules.crypto.providers.ICryptoProvider>(
            new PLang.Runtime2.modules.crypto.providers.DefaultProvider());

        var all = _engine.Providers.List();
        await Assert.That(all.Count).IsGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Sub-engine scope

    [Test, Skip("Sub-engine provider scope chain deferred")]
    public async Task SubEngine_InheritsParentProviders()
    {
        await Task.CompletedTask;
    }

    [Test, Skip("Sub-engine provider scope chain deferred")]
    public async Task SubEngine_LocalOverlay_DoesNotAffectParent()
    {
        await Task.CompletedTask;
    }

    [Test, Skip("Sub-engine provider scope chain deferred")]
    public async Task SubEngine_LocalOverlay_ClearedOnPoolReturn()
    {
        await Task.CompletedTask;
    }

    [Test, Skip("Sub-engine provider scope chain deferred")]
    public async Task SubEngine_FallsBackToParent_WhenLocalOverlayLacksProvider()
    {
        await Task.CompletedTask;
    }

    #endregion

    private class MockSigningProvider : ISigningProvider
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public MockSigningProvider(string name) { Name = name; }

        public KeyPair GenerateKeyPair() => new("mockPub", "mockPriv");
        public byte[] Sign(byte[] data, string privateKey) => new byte[64];
        public bool Verify(byte[] data, byte[] signature, string publicKey) => true;
    }
}
