using app.actor.context;
using app.error;
using app.variable;
using app.module.code;
using app.module.signing;
using app.module.signing.code;
using app.module.identity.code;
using app.module.crypto.code;
using PLangEngine = global::app.@this;
using EngineProviders = global::app.module.code.@this;

namespace PLang.Tests.App.Core;

/// <summary>
/// Tests the upgraded Engine.Providers named registry.
/// Note: Engine constructor registers Ed25519 as built-in default for ISigning.
/// </summary>
public class NamedProviderRegistryTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_registry_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
        _app.Code.Register<ISigning>(provider);

        var result = _app.Code.Get<ISigning>("custom");
        await Assert.That(result.Error).IsNull();
        await Assert.That(result.Provider).IsSameReferenceAs(provider);
        await Assert.That(((global::app.module.code.ICode)result.Provider!).Name).IsEqualTo("custom");
    }

    [Test]
    public async Task Register_FirstProvider_BecomesDefault()
    {
        var provider = new MockSigningProvider("first");
        _app.Code.Register<ISigning>(provider);

        // Ed25519 was registered first at engine startup, so it's the default
        // "first" should NOT be default
        await Assert.That(provider.IsDefault).IsFalse();
    }

    [Test]
    public async Task Register_MultipleProviders_EachRetrievableByName()
    {
        var ed = new MockSigningProvider("mock-ed");
        var ec = new MockSigningProvider("mock-ec");
        _app.Code.Register<ISigning>(ed);
        _app.Code.Register<ISigning>(ec);

        var edResult = _app.Code.Get<ISigning>("mock-ed");
        var ecResult = _app.Code.Get<ISigning>("mock-ec");
        await Assert.That(edResult.Provider).IsSameReferenceAs(ed);
        await Assert.That(ecResult.Provider).IsSameReferenceAs(ec);
    }

    [Test]
    public async Task Register_SecondProvider_DoesNotOverrideDefault()
    {
        // Ed25519 is already default from engine startup
        var second = new MockSigningProvider("second");
        _app.Code.Register<ISigning>(second);

        var builtIn = _app.Code.Get<ISigning>("ed25519");
        await Assert.That(((global::app.module.code.ICode)builtIn.Provider!).IsDefault).IsTrue();
        await Assert.That(second.IsDefault).IsFalse();
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsProviderExistsError()
    {
        // "ed25519" already registered at startup
        var result = _app.Code.Register<ISigning>(new MockSigningProvider("ed25519"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    #endregion

    #region Get

    [Test]
    public async Task Get_DefaultCryptoProvider_ReturnsIsDefaultTrue()
    {
        _app.Code.Register<ISigning>(new MockSigningProvider("second"));

        var result = _app.Code.Get<ISigning>();
        await Assert.That(result.Error).IsNull();
        var entry = (global::app.module.code.ICode)result.Provider!;
        await Assert.That(entry.IsDefault).IsTrue();
        await Assert.That(entry.Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task Get_NoneRegistered_ReturnsError()
    {
        // Use a bare registry with no built-in registrations
        var bare = new EngineProviders();
        var result = bare.Get<IKey>();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Get_ByName_NonExistent_ReturnsError()
    {
        var result = _app.Code.Get<ISigning>("ecdsa");
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefaultCryptoProvider_Succeeds()
    {
        _app.Code.Register<ISigning>(new MockSigningProvider("second"));

        var result = _app.Code.Remove<ISigning>("second");
        await result.IsSuccess();

        var getRemoved = _app.Code.Get<ISigning>("second");
        await Assert.That(getRemoved.Error).IsNotNull();

        var getBuiltIn = _app.Code.Get<ISigning>("ed25519");
        await Assert.That(getBuiltIn.Error).IsNull();
    }

    [Test]
    public async Task Remove_DefaultCryptoProvider_ReturnsCannotRemoveDefaultError()
    {
        var result = _app.Code.Remove<ISigning>("ed25519");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CannotRemoveDefault");
    }

    [Test]
    public async Task Remove_NonExistent_ReturnsProviderNotFoundError()
    {
        var result = _app.Code.Remove<ISigning>("unknown");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region SetDefault

    [Test]
    public async Task SetDefault_ExistingProvider_BecomesDefault()
    {
        var second = new MockSigningProvider("second");
        _app.Code.Register<ISigning>(second);

        var result = _app.Code.SetDefault<ISigning>("second");
        await result.IsSuccess();
        await Assert.That(second.IsDefault).IsTrue();

        var builtIn = _app.Code.Get<ISigning>("ed25519");
        await Assert.That(((global::app.module.code.ICode)builtIn.Provider!).IsDefault).IsFalse();
    }

    [Test]
    public async Task SetDefault_NonExistent_ReturnsProviderNotFoundError()
    {
        var result = _app.Code.SetDefault<ISigning>("unknown");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllProvidersOfType()
    {
        // Ed25519 already registered at startup = 1
        _app.Code.Register<ISigning>(new MockSigningProvider("a"));
        _app.Code.Register<ISigning>(new MockSigningProvider("b"));
        _app.Code.Register<ISigning>(new MockSigningProvider("c"));

        var list = _app.Code.List<ISigning>();
        await Assert.That(list.Count).IsEqualTo(4); // ed25519 + a + b + c
    }

    [Test]
    public async Task List_AllInterfaces_ReturnsProvidersAcrossTypes()
    {
        // Engine registers ISigning, IKey, IIdentity, ICrypto at startup
        var all = _app.Code.List();
        await Assert.That(all.Count).IsGreaterThanOrEqualTo(4);
    }

    #endregion

    #region ResolveType

    [Test]
    public async Task ResolveType_Signing_ReturnsISigningProvider()
    {
        var result = _app.Code.ResolveType("signing");
        await Assert.That(result).IsEqualTo(typeof(ISigning));
    }

    [Test]
    public async Task ResolveType_Identity_ReturnsIIdentityProvider()
    {
        var result = _app.Code.ResolveType("identity");
        await Assert.That(result).IsEqualTo(typeof(IIdentity));
    }

    [Test]
    public async Task ResolveType_Crypto_ReturnsICryptoProvider()
    {
        var result = _app.Code.ResolveType("crypto");
        await Assert.That(result).IsEqualTo(typeof(ICrypto));
    }

    [Test]
    public async Task ResolveType_Key_ReturnsIKeyProvider()
    {
        var result = _app.Code.ResolveType("key");
        await Assert.That(result).IsEqualTo(typeof(IKey));
    }

    [Test]
    public async Task ResolveType_Unknown_ReturnsNull()
    {
        var result = _app.Code.ResolveType("quantum");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveType_Null_DefaultsToSigning()
    {
        var result = _app.Code.ResolveType(null);
        await Assert.That(result).IsEqualTo(typeof(ISigning));
    }

    [Test]
    public async Task ResolveType_Empty_DefaultsToSigning()
    {
        var result = _app.Code.ResolveType("");
        await Assert.That(result).IsEqualTo(typeof(ISigning));
    }

    [Test]
    public async Task ResolveType_CaseInsensitive()
    {
        var result = _app.Code.ResolveType("SIGNING");
        await Assert.That(result).IsEqualTo(typeof(ISigning));
    }

    #endregion

    #region Null Name Guards

    [Test]
    public async Task Remove_NullName_ReturnsValidationError()
    {
        var result = _app.Code.Remove<ISigning>(null!);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task Remove_EmptyName_ReturnsValidationError()
    {
        var result = _app.Code.Remove<ISigning>("");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task SetDefault_NullName_ReturnsValidationError()
    {
        var result = _app.Code.SetDefault<ISigning>(null!);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task SetDefault_EmptyName_ReturnsValidationError()
    {
        var result = _app.Code.SetDefault<ISigning>("");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    #endregion

    #region GetOrDefault and Has

    [Test]
    public async Task GetOrDefault_WhenRegistered_ReturnsRegistered()
    {
        var result = _app.Code.GetOrDefault<ISigning>(new MockSigningProvider("fallback"));
        await Assert.That(result.Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task GetOrDefault_WhenNoneRegistered_ReturnsFallback()
    {
        var bare = new EngineProviders();
        var fallback = new MockSigningProvider("fallback");
        var result = bare.GetOrDefault<ISigning>(fallback);
        await Assert.That(result).IsSameReferenceAs(fallback);
    }

    [Test]
    public async Task Has_WhenRegistered_ReturnsTrue()
    {
        await Assert.That(_app.Code.Has<ISigning>()).IsTrue();
    }

    [Test]
    public async Task Has_WhenNoneRegistered_ReturnsFalse()
    {
        var bare = new EngineProviders();
        await Assert.That(bare.Has<ISigning>()).IsFalse();
    }

    #endregion


    private class MockSigningProvider : ISigning
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }

        public MockSigningProvider(string name) { Name = name; }

        public (KeyPair? keys, global::app.error.IError? error) GenerateKeyPair() => (new KeyPair("mockPub", "mockPriv"), null);
        public global::app.data.@this<global::app.type.binary.@this> Sign(byte[] data, string privateKey) => global::app.data.@this<global::app.type.binary.@this>.Ok(new byte[64]);
        public global::app.data.@this<global::app.type.@bool.@this> Verify(byte[] data, byte[] signature, string publicKey) => global::app.data.@this<global::app.type.@bool.@this>.Ok(true);
        public Task<global::app.data.@this> SignAsync(sign action) => Task.FromResult(global::app.data.@this.Ok());
        public Task<global::app.data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action) => Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.Ok(true));
    }
}
