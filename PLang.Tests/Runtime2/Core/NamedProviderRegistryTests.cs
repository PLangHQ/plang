using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Core;

/// <summary>
/// Tests the upgraded Engine.Providers named registry.
/// Note: Engine constructor registers Ed25519Provider as built-in default for ISigningProvider.
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
        var provider = new MockSigningProvider("custom");
        _engine.Providers.Register<ISigningProvider>(provider);

        var result = _engine.Providers.Get<ISigningProvider>("custom");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsSameReferenceAs(provider);
        await Assert.That(result.Value!.Name).IsEqualTo("custom");
    }

    [Test]
    public async Task Register_FirstProvider_BecomesDefault()
    {
        var provider = new MockSigningProvider("first");
        _engine.Providers.Register<ISigningProvider>(provider);

        // Ed25519 was registered first at engine startup, so it's the default
        // "first" should NOT be default
        await Assert.That(provider.IsDefault).IsFalse();
    }

    [Test]
    public async Task Register_MultipleProviders_EachRetrievableByName()
    {
        var ed = new MockSigningProvider("mock-ed");
        var ec = new MockSigningProvider("mock-ec");
        _engine.Providers.Register<ISigningProvider>(ed);
        _engine.Providers.Register<ISigningProvider>(ec);

        var edResult = _engine.Providers.Get<ISigningProvider>("mock-ed");
        var ecResult = _engine.Providers.Get<ISigningProvider>("mock-ec");
        await Assert.That(edResult.Value).IsSameReferenceAs(ed);
        await Assert.That(ecResult.Value).IsSameReferenceAs(ec);
    }

    [Test]
    public async Task Register_SecondProvider_DoesNotOverrideDefault()
    {
        // Ed25519 is already default from engine startup
        var second = new MockSigningProvider("second");
        _engine.Providers.Register<ISigningProvider>(second);

        var builtIn = _engine.Providers.Get<ISigningProvider>("ed25519");
        await Assert.That(builtIn.Value!.IsDefault).IsTrue();
        await Assert.That(second.IsDefault).IsFalse();
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsProviderExistsError()
    {
        // "ed25519" already registered at startup
        var result = _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("ed25519"));

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    #endregion

    #region Get

    [Test]
    public async Task Get_DefaultProvider_ReturnsIsDefaultTrue()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("second"));

        var result = _engine.Providers.Get<ISigningProvider>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.IsDefault).IsTrue();
        await Assert.That(result.Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task Get_NoneRegistered_ReturnsNull()
    {
        // IKeyProvider has no built-in registration
        var result = _engine.Providers.Get<IKeyProvider>();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Get_ByName_NonExistent_ReturnsError()
    {
        var result = _engine.Providers.Get<ISigningProvider>("ecdsa");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefaultProvider_Succeeds()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("second"));

        var result = _engine.Providers.Remove<ISigningProvider>("second");
        await Assert.That(result.Success).IsTrue();

        var getRemoved = _engine.Providers.Get<ISigningProvider>("second");
        await Assert.That(getRemoved.Success).IsFalse();

        var getBuiltIn = _engine.Providers.Get<ISigningProvider>("ed25519");
        await Assert.That(getBuiltIn.Success).IsTrue();
    }

    [Test]
    public async Task Remove_DefaultProvider_ReturnsCannotRemoveDefaultError()
    {
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
        var second = new MockSigningProvider("second");
        _engine.Providers.Register<ISigningProvider>(second);

        var result = _engine.Providers.SetDefault<ISigningProvider>("second");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(second.IsDefault).IsTrue();

        var builtIn = _engine.Providers.Get<ISigningProvider>("ed25519");
        await Assert.That(builtIn.Value!.IsDefault).IsFalse();
    }

    [Test]
    public async Task SetDefault_NonExistent_ReturnsProviderNotFoundError()
    {
        var result = _engine.Providers.SetDefault<ISigningProvider>("unknown");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllProvidersOfType()
    {
        // Ed25519 already registered at startup = 1
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("a"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("b"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("c"));

        var list = _engine.Providers.List<ISigningProvider>();
        await Assert.That(list.Count).IsEqualTo(4); // ed25519 + a + b + c
    }

    [Test]
    public async Task List_AllInterfaces_ReturnsProvidersAcrossTypes()
    {
        _engine.Providers.Register<PLang.Runtime2.modules.crypto.providers.ICryptoProvider>(
            new PLang.Runtime2.modules.crypto.providers.DefaultProvider());

        var all = _engine.Providers.List();
        await Assert.That(all.Count).IsGreaterThanOrEqualTo(2); // ed25519 + crypto
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
        public Data Sign(byte[] data, string privateKey) => Data.Ok(new byte[64]);
        public Data Verify(byte[] data, byte[] signature, string publicKey) => Data.Ok(true);
    }
}
