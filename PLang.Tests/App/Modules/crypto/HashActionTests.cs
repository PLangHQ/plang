using App.Context;
using App.Errors;
using App.Variables;
using App.modules.crypto;
using App.modules.crypto.providers;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.crypto;

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

    private Context.@this Ctx => _engine.System.Context;

    // --- Hash action ---

    [Test]
    public async Task Hash_StringInput_ReturnsBytesWithType()
    {
        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is byte[]).IsTrue();
        await Assert.That(result.Type!.Value).IsEqualTo("keccak256");
        await Assert.That(((byte[])result.Value!).Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_ObjectInput_ProducesDeterministicHash()
    {
        var refHash = new DefaultCryptoProvider().Hash(new Hash { Data = Data.Ok("hello"), Algorithm = "keccak256" });

        var action = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((byte[])result.Value!).IsEquivalentTo((byte[])refHash.Value!);
    }

    [Test]
    public async Task Hash_ByteArrayInput_HashesRawBytes()
    {
        var action = new Hash { Context = Ctx, Data = Data.Ok(new byte[] { 1, 2, 3 }), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is byte[]).IsTrue();
        await Assert.That(result.Type!.Value).IsEqualTo("keccak256");
    }

    [Test]
    public async Task Hash_ExplicitAlgorithm_OverridesDefault()
    {
        var keccakAction = new Hash { Context = Ctx, Data = Data.Ok("test"), Algorithm = "keccak256" };
        var sha256Action = new Hash { Context = Ctx, Data = Data.Ok("test"), Algorithm = "sha256" };

        var keccakResult = await keccakAction.Run();
        var sha256Result = await sha256Action.Run();

        await Assert.That(keccakResult.Success).IsTrue();
        await Assert.That(sha256Result.Success).IsTrue();
        await Assert.That(sha256Result.Type!.Value).IsEqualTo("sha256");
        await Assert.That(keccakResult.Type!.Value).IsEqualTo("keccak256");
        await Assert.That((byte[])sha256Result.Value!).IsNotEquivalentTo((byte[])keccakResult.Value!);
    }

    [Test]
    public async Task Hash_NullInput_ReturnsError()
    {
        // [IsNotNull] validation runs in ExecuteAsync
        var action = new Hash { Data = new Data(""), Algorithm = "keccak256" };
        var result = await action.ExecuteAsync(new App.Goals.Goal.Steps.Step.Actions.Action.@this(), _engine, Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ValueRequired");
        await Assert.That(result.Error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Hash_UnsupportedAlgorithm_ReturnsError()
    {
        var action = new Hash { Context = Ctx, Data = Data.Ok("test"), Algorithm = "md5" };
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

        var action = new Hash { Context = Ctx, Data = Data.Ok("test"), Algorithm = "keccak256" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderError");
    }

    // --- Verify action ---

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var hashAction = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var hashResult = await hashAction.Run();
        var hash = Convert.ToBase64String((byte[])hashResult.Value!);

        var verifyAction = new Verify { Context = Ctx, Data = Data.Ok("hello"), Hash = hash, Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_WrongHash_ReturnsFalse()
    {
        var hashAction = new Hash { Context = Ctx, Data = Data.Ok("hello"), Algorithm = "keccak256" };
        var hashResult = await hashAction.Run();
        var hashBytes = (byte[])hashResult.Value!;
        hashBytes[0] ^= 0xFF; // flip first byte
        var wrongHash = Convert.ToBase64String(hashBytes);
        var verifyAction = new Verify { Context = Ctx, Data = Data.Ok("hello"), Hash = wrongHash, Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_CorruptedHashString_ReturnsError()
    {
        var verifyAction = new Verify { Context = Ctx, Data = Data.Ok("hello"), Hash = "not-a-valid-base64!!!", Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidHash");
    }

    [Test]
    public async Task Verify_NullHash_ReturnsError()
    {
        var verifyAction = new Verify { Data = Data.Ok("hello"), Hash = null!, Algorithm = "keccak256" };
        var result = await verifyAction.ExecuteAsync(new App.Goals.Goal.Steps.Step.Actions.Action.@this(), _engine, Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        // String property hits MissingParameter before [IsNotNull] — both validate, first wins
        await Assert.That(result.Error!.Key).IsEqualTo("MissingParameter");
    }

    [Test]
    public async Task Verify_NullInput_ReturnsError()
    {
        var verifyAction = new Verify { Data = new Data(""), Hash = "abc123", Algorithm = "keccak256" };
        var result = await verifyAction.ExecuteAsync(new App.Goals.Goal.Steps.Step.Actions.Action.@this(), _engine, Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ValueRequired");
        await Assert.That(result.Error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Verify_ProviderReturnsError_RelaysError()
    {
        _engine.Providers.Register<ICryptoProvider>(new FailingCryptoProvider());
        _engine.Providers.SetDefault<ICryptoProvider>("failing");

        var verifyAction = new Verify { Context = Ctx, Data = Data.Ok("test"), Hash = Convert.ToBase64String(new byte[32]), Algorithm = "keccak256" };
        var result = await verifyAction.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderError");
    }

    private class FailingCryptoProvider : ICryptoProvider
    {
        public string Name => "failing";
        public bool IsDefault { get; set; }
        public Data Hash(Hash action) => Data.FromError(new ActionError("Provider failure", "ProviderError", 500));
        public Data Verify(Verify action) => Data.FromError(new ActionError("Provider failure", "ProviderError", 500));
    }
}
