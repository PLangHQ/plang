using global::App.Actor.Context;
using global::App.Errors;
using global::App.Variables;
using global::App.Providers;
using global::App.modules.signing.providers;
using global::App.modules.identity;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Tests identity create delegation to IKeyProvider.
/// </summary>
public class IdentityKeyProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_keyprovider_" + Guid.NewGuid().ToString("N")[..8]);
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

    private global::App.Actor.Context.@this Ctx => _engine.System.Context;

    [Test]
    public async Task Create_UsesKeyProviderFromRegistry()
    {
        var mockProvider = new MockKeyProvider("mock-pub-key", "mock-priv-key");
        _engine.Providers.Register<IKeyProvider>(mockProvider);
        _engine.Providers.SetDefault<IKeyProvider>("mock");

        var action = new Create { Context = Ctx, Name = "test-identity", SetAsDefault = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var identity = result as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.PublicKey).IsEqualTo("mock-pub-key");
        await Assert.That(identity.PrivateKey).IsEqualTo("mock-priv-key");
    }

    [Test]
    public async Task Create_DefaultEd25519_WhenNoOverride()
    {
        var action = new Create { Context = Ctx, Name = "test-identity", SetAsDefault = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var identity = result as Identity;
        await Assert.That(identity).IsNotNull();

        // Ed25519 keys are 32 bytes each
        var pubBytes = Convert.FromBase64String(identity!.PublicKey);
        var privBytes = Convert.FromBase64String(identity.PrivateKey);
        await Assert.That(pubBytes.Length).IsEqualTo(32);
        await Assert.That(privBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Create_KeyProvider_Throws_ReturnsError()
    {
        var throwingProvider = new ThrowingKeyProvider();
        _engine.Providers.Register<IKeyProvider>(throwingProvider);
        _engine.Providers.SetDefault<IKeyProvider>("throwing");

        var action = new Create { Context = Ctx, Name = "test-identity", SetAsDefault = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task Create_StoresKeysFromProvider()
    {
        var mockProvider = new MockKeyProvider("stored-pub", "stored-priv");
        _engine.Providers.Register<IKeyProvider>(mockProvider);
        _engine.Providers.SetDefault<IKeyProvider>("mock");

        var action = new Create { Context = Ctx, Name = "stored-test", SetAsDefault = true };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();

        // Load it back via Get action
        var getResult = await new Get { Context = Ctx, Name = "stored-test" }.Run();
        await Assert.That(getResult.Success).IsTrue();
        var loaded = getResult as Identity;
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.PublicKey).IsEqualTo("stored-pub");
        await Assert.That(loaded.PrivateKey).IsEqualTo("stored-priv");
    }

    [Test]
    public async Task Create_WithProviderParam_UsesNamedProvider()
    {
        // Ed25519 already registered as default IKeyProvider at engine startup
        var mock = new MockKeyProvider("named-pub", "named-priv") { ProviderName = "mock" };
        _engine.Providers.Register<IKeyProvider>(mock);

        var action = new Create { Context = Ctx, Name = "named-test", SetAsDefault = true, Provider = "mock" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var identity = result as Identity;
        await Assert.That(identity!.PublicKey).IsEqualTo("named-pub");
    }

    private class MockKeyProvider : IKeyProvider
    {
        private readonly string _pubKey;
        private readonly string _privKey;

        public string ProviderName { get; set; } = "mock";
        public string Name => ProviderName;
        public bool IsDefault { get; set; }

        public MockKeyProvider(string pubKey, string privKey)
        {
            _pubKey = pubKey;
            _privKey = privKey;
        }

        public global::App.Data.@this<KeyPair> GenerateKeyPair() => global::App.Data.@this<KeyPair>.Ok(new KeyPair(_pubKey, _privKey));
    }

    private class ThrowingKeyProvider : IKeyProvider
    {
        public string Name => "throwing";
        public bool IsDefault { get; set; }
        public global::App.Data.@this<KeyPair> GenerateKeyPair() => global::App.Data.@this<KeyPair>.FromError(new ActionError("Key generation failed", "KeyGenerationError", 500));
    }
}
