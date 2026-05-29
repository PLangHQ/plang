using app.actor.context;
using app.error;
using app.variables;
using app.modules.crypto;
using app.modules.crypto.code;
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

        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hash = (byte[])result.Value!;
        // Mock returns all-zero bytes
        await Assert.That(hash).IsEquivalentTo(new byte[32]);
    }

    [Test]
    public async Task Hash_NoProviderConfigured_FallsToBuiltInDefault()
    {
        // Fresh engine, no crypto settings — should use global::app.modules.crypto.code.Default
        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hash = (byte[])result.Value!;
        // Should not be all zeros (global::app.modules.crypto.code.Default produces real keccak256)
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
        var action = new Verify { Context = Ctx, Data = Data.Ok("hello"), Hash = Convert.ToBase64String(new byte[32]), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    private class MockCryptoProvider : ICrypto
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public global::app.data.@this<byte[]> Hash(Hash action) => global::app.data.@this<byte[]>.Ok(new byte[32]); // all zeros
        public global::app.data.@this<bool> Verify(Verify action) => global::app.data.@this<bool>.Ok(false);
    }

    private class AlwaysTrueVerifier : ICrypto
    {
        public string Name => "always-true";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public global::app.data.@this<byte[]> Hash(Hash action) => global::app.data.@this<byte[]>.Ok(new byte[32]);
        public global::app.data.@this<bool> Verify(Verify action) => global::app.data.@this<bool>.Ok(true);
    }
}
