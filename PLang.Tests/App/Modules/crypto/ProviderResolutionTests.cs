using app.actor.context;
using app.error;
using app.variable;
using app.module.crypto;
using app.module.crypto.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.crypto;

public class ProviderResolutionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_crypto_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task Hash_UsesProviderFromSettings_NotDefault()
    {
        var mock = new MockCryptoProvider();
        _app.Code.Register<ICrypto>(mock);
        _app.Code.SetDefault<ICrypto>("mock");

        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" };
        var result = await action.Run();

        await result.IsSuccess();
        var hash = ((global::app.module.crypto.type.hash.@this)result.Value!).Bytes;
        // Mock returns all-zero bytes
        await Assert.That(hash).IsEquivalentTo(new byte[32]);
    }

    [Test]
    public async Task Hash_NoProviderConfigured_FallsToBuiltInDefault()
    {
        // Fresh engine, no crypto settings — should use global::app.module.crypto.code.Default
        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" };
        var result = await action.Run();

        await result.IsSuccess();
        var hash = ((global::app.module.crypto.type.hash.@this)result.Value!).Bytes;
        // Should not be all zeros (global::app.module.crypto.code.Default produces real keccak256)
        await Assert.That(hash).IsNotEquivalentTo(new byte[32]);
        await Assert.That(hash.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Verify_UsesProviderFromSettings()
    {
        var mock = new AlwaysTrueVerifier();
        _app.Code.Register<ICrypto>(mock);
        _app.Code.SetDefault<ICrypto>("always-true");

        // Even with garbage hash, mock returns true
        var action = new Verify { Context = Ctx, Data = Data.Ok("hello"), Hash = Data.Ok(Convert.ToBase64String(new byte[32])), Algorithm = (global::app.type.text.@this)"keccak256" };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    private class MockCryptoProvider : ICrypto
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public global::app.data.@this<global::app.module.crypto.type.hash.@this> Hash(Hash action) => global::app.data.@this<global::app.module.crypto.type.hash.@this>.Ok(new global::app.module.crypto.type.hash.@this(new byte[32], "keccak256"), global::app.type.@this.Create("hash", kind: "keccak256")); // all zeros
        public global::app.data.@this<global::app.type.@bool.@this> Verify(Verify action) => global::app.data.@this<global::app.type.@bool.@this>.Ok(false);
    }

    private class AlwaysTrueVerifier : ICrypto
    {
        public string Name => "always-true";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public global::app.data.@this<global::app.module.crypto.type.hash.@this> Hash(Hash action) => global::app.data.@this<global::app.module.crypto.type.hash.@this>.Ok(new global::app.module.crypto.type.hash.@this(new byte[32], "keccak256"), global::app.type.@this.Create("hash", kind: "keccak256"));
        public global::app.data.@this<global::app.type.@bool.@this> Verify(Verify action) => global::app.data.@this<global::app.type.@bool.@this>.Ok(true);
    }
}
