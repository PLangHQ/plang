using System.Text.Json;
using app.actor.context;
using app.error;
using app.variable;
using app.module.code;
using app.module.signing.code;
using app.module.crypto;
using app.module.identity;
using app.module.signing;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Tests the sign action handler.
/// </summary>
public class SignActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_sign_" + Guid.NewGuid().ToString("N")[..8]);
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

    private async Task<Data> SignData(object? data, List<string>? contracts = null,
        TimeSpan? expires = null, Dictionary<string, object>? headers = null)
    {
        var action = new sign
        {
            Context = Ctx,
            Data = new Data("", data),
            Contracts = contracts,
            Expires = expires,
            Headers = headers
        };
        return await _app.RunAction<sign>(action, Ctx);
    }

    #region Happy Path & Field Population

    [Test]
    public async Task Sign_HappyPath_AllFieldsPopulated()
    {
        var result = await SignData(new { message = "hello" });

        await Assert.That(result.Success).IsTrue();
        var sd = result.Signature;
        await Assert.That(sd).IsNotNull();
        await Assert.That(sd!.Type).IsNotEmpty();
        await Assert.That(sd.Algorithm).IsNotEmpty();
        await Assert.That(sd.Nonce).IsNotEmpty();
        await Assert.That(sd.Identity).IsNotEmpty();
        await Assert.That(sd.Hash).IsNotNull();
        await Assert.That(sd.Value).IsNotNull();
    }

    [Test]
    public async Task Sign_Type_IsSignature_Algorithm_IsEd25519()
    {
        var result = await SignData(new { message = "hello" });

        await Assert.That(result.Success).IsTrue();
        var sd = result.Signature!;
        await Assert.That(sd.Type).IsEqualTo("signature");
        await Assert.That(sd.Algorithm).IsEqualTo("ed25519");
    }

    [Test]
    public async Task Sign_Identity_MatchesPublicKey()
    {
        // Create identity first to capture public key
        var identityResult = await new Get { Context = Ctx, Name = null }.Run();
        var publicKey = (identityResult.Value as Identity)!.PublicKey;

        var result = await SignData("test data");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature!.Identity).IsEqualTo(publicKey);
    }

    #endregion

    #region Created

    [Test]
    public async Task Sign_Created_IsApproximatelyNow()
    {
        var before = DateTimeOffset.UtcNow;
        var result = await SignData("test");
        var after = DateTimeOffset.UtcNow;

        await Assert.That(result.Success).IsTrue();
        var created = result.Signature!.Created;
        await Assert.That(created >= before.AddSeconds(-1)).IsTrue();
        await Assert.That(created <= after.AddSeconds(1)).IsTrue();
    }

    #endregion

    #region Contracts

    [Test]
    public async Task Sign_NoContracts_ContractsIsNull()
    {
        var result = await SignData("test");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature!.Contracts).IsNull();
    }

    [Test]
    public async Task Sign_CustomContracts()
    {
        var result = await SignData("test", contracts: new List<string> { "C0", "C1" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature!.Contracts!.Count).IsEqualTo(2);
        await Assert.That(result.Signature!.Contracts).Contains("C0");
        await Assert.That(result.Signature!.Contracts).Contains("C1");
    }

    #endregion

    #region TTL / Expiry

    [Test]
    public async Task Sign_TTL_SetsExpires()
    {
        var result = await SignData("test", expires: TimeSpan.FromSeconds(5));

        await Assert.That(result.Success).IsTrue();
        var sd = result.Signature!;
        await Assert.That(sd.Expires).IsNotNull();
        var diff = (sd.Expires!.Value - sd.Created).TotalMilliseconds;
        await Assert.That(diff).IsGreaterThanOrEqualTo(4900);
        await Assert.That(diff).IsLessThanOrEqualTo(5100);
    }

    [Test]
    public async Task Sign_NoTTL_ExpiresIsNull()
    {
        var result = await SignData("test");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature!.Expires).IsNull();
    }

    #endregion

    #region Headers & Hash

    [Test]
    public async Task Sign_Headers_IncludedWhenProvided()
    {
        var headers = new Dictionary<string, object> { { "method", "POST" } };
        var result = await SignData("test", headers: headers);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature!.Headers).IsNotNull();
        await Assert.That(result.Signature!.Headers!["method"].ToString()).IsEqualTo("POST");
    }

    [Test]
    public async Task Sign_Hash_IsBytes()
    {
        var result = await SignData("test");

        await Assert.That(result.Success).IsTrue();
        var hash = result.Signature!.Hash;
        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Value is byte[]).IsTrue();
        await Assert.That(((byte[])hash.Value!).Length).IsGreaterThan(0);
    }

    #endregion

    #region Cryptographic Validity

    [Test]
    public async Task Sign_Signature_CryptographicallyValid()
    {
        var result = await SignData("test");
        await Assert.That(result.Success).IsTrue();

        var sd = result.Signature!;
        var sigBytes = Convert.FromBase64String(sd.Value!);
        var signingBytes = sd.ToSigningBytes();

        var provider = new Ed25519();
        var verifyResult = provider.Verify(signingBytes, sigBytes, sd.Identity);
        await Assert.That(verifyResult.Success).IsTrue();
        await Assert.That((bool)verifyResult.Value!).IsTrue();
    }

    #endregion

    #region Provider Override

    [Test]
    public async Task Sign_CustomDefaultProvider_UsesIt()
    {
        // Ensure identity exists with the default ed25519 provider first
        await new Get { Context = Ctx, Name = null }.Run();

        var mock = new MockSigningProvider("mock");
        _app.Code.Register<ISigning>(mock);
        _app.Code.SetDefault<ISigning>("mock");

        var result = await SignData("test");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature).IsNotNull();
        await Assert.That(mock.SignCalled).IsTrue();
    }

    #endregion

    #region Empty Contracts

    [Test]
    public async Task Sign_EmptyContracts_Succeeds()
    {
        var result = await SignData("test", contracts: new List<string>());
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Signature!.Contracts).IsNotNull();
        await Assert.That(result.Signature!.Contracts!.Count).IsEqualTo(0);
    }

    #endregion

    #region Error Paths

    [Test]
    public async Task Sign_ThrowingKeyProvider_ReturnsError()
    {
        // Register a key provider that throws to simulate identity creation failure
        var throwingProvider = new ThrowingKeyProvider();
        _app.Code.Register<IKey>(throwingProvider);
        _app.Code.SetDefault<IKey>("throwing-key");

        var result = await SignData("test");
        await Assert.That(result.Success).IsFalse();
        // Key generation fails, identity creation fails, sign fails
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task Sign_ProviderThrows_ReturnsDataFromError()
    {
        // Ensure identity exists first
        await new Get { Context = Ctx, Name = null }.Run();

        var throwing = new ThrowingSigningProvider();
        _app.Code.Register<ISigning>(throwing);
        _app.Code.SetDefault<ISigning>("throwing");

        var result = await SignData("test");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SigningError");
    }

    #endregion

    private class MockSigningProvider : ISigning
    {
        private readonly Ed25519 _inner = new();
        public string Name { get; }
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public bool SignCalled { get; private set; }

        public MockSigningProvider(string name) { Name = name; }

        public global::app.data.@this<KeyPair> GenerateKeyPair() => _inner.GenerateKeyPair();
        public global::app.data.@this<byte[]> Sign(byte[] data, string privateKey) => _inner.Sign(data, privateKey);
        public global::app.data.@this<bool> Verify(byte[] data, byte[] signature, string publicKey) => _inner.Verify(data, signature, publicKey);
        public async Task<global::app.data.@this<object>> SignAsync(sign action) { SignCalled = true; return await _inner.SignAsync(action); }
        public Task<global::app.data.@this<bool>> VerifyAsync(verify action) => _inner.VerifyAsync(action);
    }

    private class ThrowingSigningProvider : ISigning
    {
        public string Name => "throwing";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public global::app.data.@this<KeyPair> GenerateKeyPair() => global::app.data.@this<KeyPair>.FromError(new ActionError("Key generation failed", "KeyGenerationError", 500));
        public global::app.data.@this<byte[]> Sign(byte[] data, string privateKey) => global::app.data.@this<byte[]>.FromError(new ActionError("Sign failed", "SigningError", 500));
        public global::app.data.@this<bool> Verify(byte[] data, byte[] signature, string publicKey) => global::app.data.@this<bool>.FromError(new ActionError("Verify failed", "SignatureInvalid", 400));
        public Task<global::app.data.@this<object>> SignAsync(sign action) => Task.FromResult(global::app.data.@this<object>.FromError(new ActionError("Sign failed", "SigningError", 500)));
        public Task<global::app.data.@this<bool>> VerifyAsync(verify action) => Task.FromResult(global::app.data.@this<bool>.FromError(new ActionError("Verify failed", "SignatureInvalid", 400)));
    }

    private class ThrowingKeyProvider : IKey
    {
        public string Name => "throwing-key";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }
        public global::app.data.@this<KeyPair> GenerateKeyPair() => global::app.data.@this<KeyPair>.FromError(new ActionError("Key generation failed", "KeyGenerationError", 500));
    }
}
