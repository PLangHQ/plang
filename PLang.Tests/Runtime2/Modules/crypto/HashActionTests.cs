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
    public async Task Hash_StringInput_ReturnsBase64WithType()
    {
        var action = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is string).IsTrue();
        await Assert.That(result.Type!.Value).IsEqualTo("keccak256");
        var hash = (string)result.Value!;
        await Assert.That(hash).IsNotEmpty();
        await Assert.That(hash.Length).IsEqualTo(44); // 32 bytes = 44 base64 chars
    }

    [Test]
    public async Task Hash_ObjectInput_SerializesToJsonBeforeHashing()
    {
        // Known value: JsonSerializer.Serialize("hello") = "\"hello\""
        // keccak256 of UTF8 bytes of "\"hello\"" is a fixed value.
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize("hello"));
        var refHash = new DefaultCryptoProvider().Hash(jsonBytes, "keccak256");
        var expectedBase64 = Convert.ToBase64String((byte[])refHash.Value!);

        var action = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((string)result.Value!).IsEqualTo(expectedBase64);
    }

    [Test]
    public async Task Hash_ByteArrayInput_HashesRawBytes()
    {
        var action = new Hash { Context = Ctx, Data = new byte[] { 1, 2, 3 }, Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is string).IsTrue();
        await Assert.That(result.Type!.Value).IsEqualTo("keccak256");
    }

    [Test]
    public async Task Hash_ExplicitAlgorithm_OverridesDefault()
    {
        var keccakAction = new Hash { Context = Ctx, Data = "test", Algorithm = "keccak256" };
        var sha256Action = new Hash { Context = Ctx, Data = "test", Algorithm = "sha256" };

        var keccakResult = await keccakAction.Run();
        var sha256Result = await sha256Action.Run();

        await Assert.That(keccakResult.Success).IsTrue();
        await Assert.That(sha256Result.Success).IsTrue();
        await Assert.That(sha256Result.Type!.Value).IsEqualTo("sha256");
        await Assert.That(keccakResult.Type!.Value).IsEqualTo("keccak256");
        await Assert.That((string)sha256Result.Value!).IsNotEqualTo((string)keccakResult.Value!);
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
    public async Task Hash_ProviderReturnsError_RelaysError()
    {
        _engine.Providers.Register<ICryptoProvider>(new FailingCryptoProvider());
        _engine.Providers.SetDefault<ICryptoProvider>("failing");

        var action = new Hash { Context = Ctx, Data = "test", Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderError");
    }

    // --- Verify action ---

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var hashAction = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var hashResult = await hashAction.Run();
        var hash = (string)hashResult.Value!;

        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = hash, Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_WrongHash_ReturnsFalse()
    {
        var hashAction = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
        var hashResult = await hashAction.Run();
        var hash = (string)hashResult.Value!;

        // Use a different hash (flip first char)
        var wrongHash = (hash[0] == 'a' ? 'b' : 'a') + hash[1..];
        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = wrongHash, Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_CorruptedHashString_ReturnsError()
    {
        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = "not-a-valid-base64!!!", Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidHash");
    }

    [Test]
    public async Task Verify_NullHash_ReturnsError()
    {
        var verifyAction = new Verify { Context = Ctx, Data = "hello", Hash = null!, Algorithm = "keccak256" };
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
    public async Task Verify_ProviderReturnsError_RelaysError()
    {
        _engine.Providers.Register<ICryptoProvider>(new FailingCryptoProvider());
        _engine.Providers.SetDefault<ICryptoProvider>("failing");

        var verifyAction = new Verify { Context = Ctx, Data = "test", Hash = Convert.ToBase64String(new byte[32]), Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderError");
    }

    private class FailingCryptoProvider : ICryptoProvider
    {
        public string Name => "failing";
        public bool IsDefault { get; set; }
        public Data Hash(byte[] data, string algorithm) => Data.FromError(new ActionError("Provider failure", "ProviderError", 500));
        public Data Verify(byte[] data, byte[] expectedHash, string algorithm) => Data.FromError(new ActionError("Provider failure", "ProviderError", 500));
    }
}
