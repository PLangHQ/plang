using App.Context;
using App.Errors;
using App.Variables;
using App.modules.crypto;
using App.modules.crypto.providers;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.crypto;

public class ProviderResolutionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_crypto_" + Guid.NewGuid().ToString("N")[..8]);
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

    private Context.@this Ctx => _engine.System.Context;

    [Test]
    public async Task Hash_UsesProviderFromSettings_NotDefault()
    {
        var mock = new MockCryptoProvider();
        _engine.Providers.Register<ICryptoProvider>(mock);
        _engine.Providers.SetDefault<ICryptoProvider>("mock");

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
        // Fresh engine, no crypto settings — should use DefaultCryptoProvider
        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hash = (byte[])result.Value!;
        // Should not be all zeros (DefaultCryptoProvider produces real keccak256)
        await Assert.That(hash).IsNotEquivalentTo(new byte[32]);
        await Assert.That(hash.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Verify_UsesProviderFromSettings()
    {
        var mock = new AlwaysTrueVerifier();
        _engine.Providers.Register<ICryptoProvider>(mock);
        _engine.Providers.SetDefault<ICryptoProvider>("always-true");

        // Even with garbage hash, mock returns true
        var action = new Verify { Context = Ctx, Data = Data.Ok("hello"), Hash = Convert.ToBase64String(new byte[32]), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    private class MockCryptoProvider : ICryptoProvider
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }
        public Data Hash(Hash action) => Data.Ok(new byte[32]); // all zeros
        public Data Verify(Verify action) => Data.Ok(false);
    }

    private class AlwaysTrueVerifier : ICryptoProvider
    {
        public string Name => "always-true";
        public bool IsDefault { get; set; }
        public Data Hash(Hash action) => Data.Ok(new byte[32]);
        public Data Verify(Verify action) => Data.Ok(true);
    }
}
