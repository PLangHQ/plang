using app.actor.context;
using app.error;
using app.variable;
using app.module.crypto;
using app.module.crypto.code;
using PLangEngine = global::app.@this;
using hash = global::app.module.crypto.type.hash.@this;

namespace PLang.Tests.App.Modules.crypto;

public class HashActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_crypto_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        global::PLang.Tests.TestApp.UseSharedIdentity(_app);
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

    // --- Hash action ---

    [Test]
    public async Task Hash_StringInput_ReturnsBytesWithType()
    {
        var action = new Hash { Context = Ctx, Data = Ctx.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value()) is hash).IsTrue();
        // Stage 7: the algorithm is the value's KIND, name is "hash".
        await Assert.That(result.Type!.Name).IsEqualTo("hash");
        await Assert.That(result.Type!.Kind).IsEqualTo("keccak256");
        await Assert.That(((hash)(await result.Value())!).Bytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_ObjectInput_ProducesDeterministicHash()
    {
        var refHash = await new global::app.module.crypto.code.Default().Hash(new Hash { Context = Ctx, Data = Ctx.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" });

        var action = new Hash { Context = Ctx, Data = Ctx.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(((hash)(await result.Value())!).Bytes).IsEquivalentTo(((hash)(await refHash.Value())!).Bytes);
    }

    [Test]
    public async Task Hash_ByteArrayInput_HashesRawBytes()
    {
        var action = new Hash { Context = Ctx, Data = Ctx.Ok(new byte[] { 1, 2, 3 }), Algorithm = (global::app.type.text.@this)"keccak256" };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value()) is hash).IsTrue();
        await Assert.That(result.Type!.Name).IsEqualTo("hash");
        await Assert.That(result.Type!.Kind).IsEqualTo("keccak256");
    }

    [Test]
    public async Task Hash_ExplicitAlgorithm_OverridesDefault()
    {
        var keccakAction = new Hash { Context = Ctx, Data = Ctx.Ok("test"), Algorithm = (global::app.type.text.@this)"keccak256" };
        var sha256Action = new Hash { Context = Ctx, Data = Ctx.Ok("test"), Algorithm = (global::app.type.text.@this)"sha256" };
        await keccakAction.Attach(null, Ctx);
        await sha256Action.Attach(null, Ctx);

        var keccakResult = await keccakAction.Run();
        var sha256Result = await sha256Action.Run();

        await keccakResult.IsSuccess();
        await sha256Result.IsSuccess();
        await Assert.That(sha256Result.Type!.Name).IsEqualTo("hash");
        await Assert.That(sha256Result.Type!.Kind).IsEqualTo("sha256");
        await Assert.That(keccakResult.Type!.Name).IsEqualTo("hash");
        await Assert.That(keccakResult.Type!.Kind).IsEqualTo("keccak256");
        await Assert.That(((hash)(await sha256Result.Value())!).Bytes).IsNotEquivalentTo(((hash)(await keccakResult.Value())!).Bytes);
    }

    [Test]
    public async Task Hash_NullInput_ReturnsError()
    {
        var action = new PrAction
        {
            Module = "crypto", ActionName = "hash",
            Parameters = new List<Data> { new Data("data", null, context: Ctx), new Data("algorithm", "keccak256", context: Ctx) }
        };
        var result = await action.RunAsync(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ValueRequired");
        await Assert.That(result.Error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Hash_UnsupportedAlgorithm_ReturnsError()
    {
        var action = new Hash { Context = Ctx, Data = Ctx.Ok("test"), Algorithm = (global::app.type.text.@this)"md5" };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }

    [Test]
    public async Task Hash_ProviderReturnsError_RelaysError()
    {
        _app.Code.Register<ICrypto>(new FailingCryptoProvider());
        _app.Code.SetDefault<ICrypto>("failing");

        var action = new Hash { Context = Ctx, Data = Ctx.Ok("test"), Algorithm = (global::app.type.text.@this)"keccak256" };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderError");
    }

    // --- Verify action ---

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var hashAction = new Hash { Context = Ctx, Data = Ctx.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" };
        await hashAction.Attach(null, Ctx);
        var hashResult = await hashAction.Run();
        var base64 = ((hash)(await hashResult.Value())!).ToBase64();

        var verifyAction = new Verify { Context = Ctx, Data = Ctx.Ok("hello"), Hash = Ctx.Ok(base64), Algorithm = (global::app.type.text.@this)"keccak256" };
        await verifyAction.Attach(null, Ctx);
        var result = await verifyAction.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsTrue();
    }

    [Test]
    public async Task Verify_WrongHash_ReturnsFalse()
    {
        var hashAction = new Hash { Context = Ctx, Data = Ctx.Ok("hello"), Algorithm = (global::app.type.text.@this)"keccak256" };
        await hashAction.Attach(null, Ctx);
        var hashResult = await hashAction.Run();
        var hashBytes = ((hash)(await hashResult.Value())!).Bytes.ToArray();
        hashBytes[0] ^= 0xFF; // flip first byte
        var wrongHash = Convert.ToBase64String(hashBytes);
        var verifyAction = new Verify { Context = Ctx, Data = Ctx.Ok("hello"), Hash = Ctx.Ok(wrongHash), Algorithm = (global::app.type.text.@this)"keccak256" };
        await verifyAction.Attach(null, Ctx);
        var result = await verifyAction.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsFalse();
    }

    [Test]
    public async Task Verify_CorruptedHashString_ReturnsError()
    {
        var verifyAction = new Verify { Context = Ctx, Data = Ctx.Ok("hello"), Hash = Ctx.Ok("not-a-valid-base64!!!"), Algorithm = (global::app.type.text.@this)"keccak256" };
        await verifyAction.Attach(null, Ctx);
        var result = await verifyAction.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidHash");
    }

    [Test]
    public async Task Verify_NullHash_ReturnsError()
    {
        var action = new PrAction
        {
            Module = "crypto", ActionName = "verify",
            Parameters = new List<Data> { new Data("data", "hello", context: Ctx), new Data("hash", null, context: Ctx), new Data("algorithm", "keccak256", context: Ctx) }
        };
        var (h, err) = await new Verify().Resolve(action, Ctx);
        var result = err != null ? Ctx.Error(err) : await h!.Execute();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        // [IsNotNull] validation catches the null Hash
        await Assert.That(result.Error!.Key).IsEqualTo("ValueRequired");
    }

    [Test]
    public async Task Verify_NullInput_ReturnsError()
    {
        var action = new PrAction
        {
            Module = "crypto", ActionName = "verify",
            Parameters = new List<Data> { new Data("data", null, context: Ctx), new Data("hash", "abc123", context: Ctx), new Data("algorithm", "keccak256", context: Ctx) }
        };
        var result = await action.RunAsync(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ValueRequired");
        await Assert.That(result.Error.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task Verify_ProviderReturnsError_RelaysError()
    {
        _app.Code.Register<ICrypto>(new FailingCryptoProvider());
        _app.Code.SetDefault<ICrypto>("failing");

        var verifyAction = new Verify { Context = Ctx, Data = Ctx.Ok("test"), Hash = Ctx.Ok(Convert.ToBase64String(new byte[32])), Algorithm = (global::app.type.text.@this)"keccak256" };
        await verifyAction.Attach(null, Ctx);
        var result = await verifyAction.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderError");
    }

    private class FailingCryptoProvider : ICrypto
    {
        public string Name => "failing";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public System.Threading.Tasks.Task<global::app.data.@this<global::app.module.crypto.type.hash.@this>> Hash(Hash action) => System.Threading.Tasks.Task.FromResult(global::app.data.@this<global::app.module.crypto.type.hash.@this>.FromError(new ActionError("Provider failure", "ProviderError", 500)));         public System.Threading.Tasks.Task<global::app.data.@this<global::app.type.@bool.@this>> Verify(Verify action) => System.Threading.Tasks.Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Provider failure", "ProviderError", 500)));     }
}
