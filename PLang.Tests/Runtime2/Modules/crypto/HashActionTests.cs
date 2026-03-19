using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class HashActionTests
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

    // --- Hash action ---

    [Test]
    public async Task Hash_StringInput_ReturnsHashedData()
    {
        var action = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hashed = result.Value as HashedData;
        await Assert.That(hashed).IsNotNull();
        await Assert.That(hashed!.Algorithm).IsEqualTo("keccak256");
        await Assert.That(hashed.Format).IsEqualTo("json");
        await Assert.That(hashed.Hash).IsNotEmpty();
        await Assert.That(hashed.Hash.Length).IsEqualTo(64); // 32 bytes = 64 hex chars
    }

    [Test]
    public async Task Hash_ObjectInput_SerializesToJsonBeforeHashing()
    {
        var action1 = new Hash { Context = Ctx, Data = new { Name = "test", Value = 42 }, Algorithm = "keccak256" };
        var action2 = new Hash { Context = Ctx, Data = new { Name = "test", Value = 42 }, Algorithm = "keccak256" };

        var result1 = await action1.Run();
        var result2 = await action2.Run();

        await Assert.That(result1.Success).IsTrue();
        await Assert.That(result2.Success).IsTrue();
        var hash1 = (result1.Value as HashedData)!.Hash;
        var hash2 = (result2.Value as HashedData)!.Hash;
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task Hash_ByteArrayInput_FormatIsRaw()
    {
        var action = new Hash { Context = Ctx, Data = new byte[] { 1, 2, 3 }, Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hashed = result.Value as HashedData;
        await Assert.That(hashed).IsNotNull();
        await Assert.That(hashed!.Format).IsEqualTo("raw");
    }

    [Test]
    public async Task Hash_ExplicitAlgorithm_OverridesDefault()
    {
        // SHA256 of JSON-serialized "test" → JsonSerializer.Serialize("test") = "\"test\""
        var action = new Hash { Context = Ctx, Data = "test", Algorithm = "sha256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var hashed = result.Value as HashedData;
        await Assert.That(hashed).IsNotNull();
        await Assert.That(hashed!.Algorithm).IsEqualTo("sha256");
        await Assert.That(hashed.Hash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Hash_NullInput_ReturnsError()
    {
        var action = new Hash { Context = Ctx, Data = null, Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
        await Assert.That(result.Error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Hash_UnsupportedAlgorithm_ReturnsError()
    {
        var action = new Hash { Context = Ctx, Data = "test", Algorithm = "md5" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }

    [Test]
    public async Task Hash_ProviderThrows_ReturnsDataFail()
    {
        Ctx.MemoryStack.Set("CryptoProvider", new ThrowingCryptoProvider());

        var action = new Hash { Context = Ctx, Data = "test", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Exception).IsTypeOf<InvalidOperationException>();
    }

    // --- Verify action ---

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var hashAction = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var hashResult = await hashAction.Run();
        var hashed = (hashResult.Value as HashedData)!;

        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = hashed.Hash, Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_WrongHash_ReturnsFalse()
    {
        var hashAction = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var hashResult = await hashAction.Run();
        var hashed = (hashResult.Value as HashedData)!;

        // Use a different hash (flip first char)
        var wrongHash = (hashed.Hash[0] == 'a' ? 'b' : 'a') + hashed.Hash[1..];
        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = wrongHash, Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_CorruptedHashString_ReturnsError()
    {
        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = "not-a-valid-hex-string!!!", Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidHash");
    }

    [Test]
    public async Task Verify_NullInput_ReturnsError()
    {
        var verifyAction = new Verify { Context = Ctx, Data = null, Hash = "abc123", Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
        await Assert.That(result.Error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Verify_ProviderThrows_ReturnsDataFail()
    {
        Ctx.MemoryStack.Set("CryptoProvider", new ThrowingCryptoProvider());

        // Need a valid hex string so we get past the hex decode
        var verifyAction = new Verify { Context = Ctx, Data = "test", Hash = "0000000000000000000000000000000000000000000000000000000000000000", Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Exception).IsTypeOf<InvalidOperationException>();
    }

    private class ThrowingCryptoProvider : ICryptoProvider
    {
        public byte[] Hash(byte[] data, string algorithm) => throw new InvalidOperationException("Provider failure");
        public bool Verify(byte[] data, byte[] expectedHash, string algorithm) => throw new InvalidOperationException("Provider failure");
    }
}
