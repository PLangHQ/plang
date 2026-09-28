using app.actor.context;
using app.error;
using app.type.item.variable;
using app.module.action.code;
using app.module.action.signing.code;
using app.module.action.identity;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Tests identity create delegation to IKey.
/// </summary>
public class IdentityKeyProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_keyprovider_" + Guid.NewGuid().ToString("N")[..8]);
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

    private global::app.actor.context.@this Ctx => _app.System.Context;

    [Test]
    public async Task Create_UsesKeyProviderFromRegistry()
    {
        var mockProvider = new MockKeyProvider();
        _app.Code.Register<IKey>(mockProvider);
        _app.Code.SetDefault<IKey>("mock");

        var action = new Create(Ctx) { Name = (global::app.type.item.text.@this)"test-identity", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.PublicKey).IsEqualTo(mockProvider.Keys.PublicKey);
        await Assert.That(identity.PrivateKey).IsEqualTo(mockProvider.Keys.PrivateKey);
    }

    [Test]
    public async Task Create_DefaultEd25519_WhenNoOverride()
    {
        var action = new Create(Ctx) { Name = (global::app.type.item.text.@this)"test-identity", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
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
        _app.Code.Register<IKey>(throwingProvider);
        _app.Code.SetDefault<IKey>("throwing");

        var action = new Create(Ctx) { Name = (global::app.type.item.text.@this)"test-identity", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task Create_StoresKeysFromProvider()
    {
        var mockProvider = new MockKeyProvider();
        _app.Code.Register<IKey>(mockProvider);
        _app.Code.SetDefault<IKey>("mock");

        var action = new Create(Ctx) { Name = (global::app.type.item.text.@this)"stored-test", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await action.Attach(null, Ctx);
        var result = await action.Start();
        await result.IsSuccess();

        // Load it back via Get action
        var __a0 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"stored-test" };
        await __a0.Attach(null, Ctx);
        var getResult = await __a0.Start();
        await getResult.IsSuccess();
        var loaded = (await getResult.Value()) as Identity;
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.PublicKey).IsEqualTo(mockProvider.Keys.PublicKey);
        await Assert.That(loaded.PrivateKey).IsEqualTo(mockProvider.Keys.PrivateKey);
    }

    [Test]
    public async Task Create_WithProviderParam_UsesNamedProvider()
    {
        // Ed25519 already registered as default IKey at engine startup
        var mock = new MockKeyProvider();
        _app.Code.Register<IKey>(mock);

        var action = new Create(Ctx) { Name = (global::app.type.item.text.@this)"named-test", SetAsDefault = (global::app.type.item.@bool.@this)true, Provider = (global::app.type.item.text.@this)"mock" };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PublicKey).IsEqualTo(mock.Keys.PublicKey);
    }

    // Hands out one fixed pair — real Ed25519 keys, since the identity's own row is signed with them
    private class MockKeyProvider : IKey
    {
        public KeyPair Keys { get; } = new Ed25519().GenerateKeyPair().keys!;

        public string Name => "mock";

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }

        public (KeyPair? keys, global::app.error.Error? error) GenerateKeyPair() => (Keys, null);
    }

    private class ThrowingKeyProvider : IKey
    {
        public string Name => "throwing";

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public (KeyPair? keys, global::app.error.Error? error) GenerateKeyPair() => (null, new ActionError("Key generation failed", "KeyGenerationError", 500));
    }
}
