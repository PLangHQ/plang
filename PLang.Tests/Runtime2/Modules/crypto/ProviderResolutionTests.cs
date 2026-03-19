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
        // Mock returns all-zero bytes → hex is all zeros
        await Assert.That(hashed!.Hash).IsEqualTo("0000000000000000000000000000000000000000000000000000000000000000");
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
        await Assert.That(hashed!.Hash).IsNotEqualTo("0000000000000000000000000000000000000000000000000000000000000000");
        await Assert.That(hashed.Hash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Verify_UsesProviderFromSettings()
    {
        var mock = new AlwaysTrueVerifier();
        _engine.Providers.Register<ICryptoProvider>(mock);

        // Even with garbage hash, mock returns true
        var action = new Verify { Context = Ctx, Data = "hello", Hash = "0000000000000000000000000000000000000000000000000000000000000000", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    private class MockCryptoProvider : ICryptoProvider
    {
        public byte[] Hash(byte[] data, string algorithm) => new byte[32]; // all zeros
        public bool Verify(byte[] data, byte[] expectedHash, string algorithm) => false;
    }

    private class AlwaysTrueVerifier : ICryptoProvider
    {
        public byte[] Hash(byte[] data, string algorithm) => new byte[32];
        public bool Verify(byte[] data, byte[] expectedHash, string algorithm) => true;
    }
}
