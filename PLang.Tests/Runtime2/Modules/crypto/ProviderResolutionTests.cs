using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.crypto;

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

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Hash_UsesProviderFromSettings_NotDefault()
    {
        var mock = new MockCryptoProvider();
        _engine.Providers.Register<ICryptoProvider>(mock);

        var action = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hashed = result.Value as HashedData;
        await Assert.That(hashed).IsNotNull();
        // Mock returns all-zero bytes → base64 of 32 zero bytes
        await Assert.That(hashed!.Hash).IsEqualTo(Convert.ToBase64String(new byte[32]));
    }

    [Test]
    public async Task Hash_NoProviderConfigured_FallsToBuiltInDefault()
    {
        // Fresh engine, no crypto settings — should use DefaultProvider
        var action = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hashed = result.Value as HashedData;
        await Assert.That(hashed).IsNotNull();
        // Should not be all zeros (DefaultProvider produces real keccak256)
        await Assert.That(hashed!.Hash).IsNotEqualTo(Convert.ToBase64String(new byte[32]));
        // Base64 of 32 bytes = 44 chars
        await Assert.That(hashed.Hash.Length).IsEqualTo(44);
    }

    [Test]
    public async Task Verify_UsesProviderFromSettings()
    {
        var mock = new AlwaysTrueVerifier();
        _engine.Providers.Register<ICryptoProvider>(mock);

        // Even with garbage hash, mock returns true
        var action = new Verify { Context = Ctx, Data = "hello", Hash = Convert.ToBase64String(new byte[32]), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    private class MockCryptoProvider : ICryptoProvider
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }
        public Data Hash(byte[] data, string algorithm) => Data.Ok(new byte[32]); // all zeros
        public Data Verify(byte[] data, byte[] expectedHash, string algorithm) => Data.Ok(false);
    }

    private class AlwaysTrueVerifier : ICryptoProvider
    {
        public string Name => "always-true";
        public bool IsDefault { get; set; }
        public Data Hash(byte[] data, string algorithm) => Data.Ok(new byte[32]);
        public Data Verify(byte[] data, byte[] expectedHash, string algorithm) => Data.Ok(true);
    }
}
