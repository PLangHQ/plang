using System.Text.Json;
using global::App.Actor.Context;
using global::App.Errors;
using global::App.Variables;
using global::App.Providers;
using global::App.modules.signing.providers;
using global::App.modules.crypto;
using global::App.modules.identity;
using global::App.modules.signing;
using PLangEngine = global::App.@this;

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

    private global::App.Actor.Context.@this Ctx => _app.System.Context;

    private async Task<Data> SignData(object? data, List<string>? contracts = null,
        int? expiresInMs = null, Dictionary<string, object>? headers = null)
    {
        var action = new sign
        {
            Context = Ctx,
            Data = new Data("", data),
            Contracts = contracts,
            ExpiresInMs = expiresInMs,
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
        await Assert.That(sd.Signature).IsNotNull();
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
        var publicKey = (identityResult as Identity)!.PublicKey;

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
        var result = await SignData("test", expiresInMs: 5000);

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
        var sigBytes = Convert.FromBase64String(sd.Signature!);
        var signingBytes = sd.ToSigningBytes();

        var provider = new Ed25519Provider();
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
        _app.Providers.Register<ISigningProvider>(mock);
        _app.Providers.SetDefault<ISigningProvider>("mock");

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
        _app.Providers.Register<IKeyProvider>(throwingProvider);
        _app.Providers.SetDefault<IKeyProvider>("throwing-key");

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
        _app.Providers.Register<ISigningProvider>(throwing);
        _app.Providers.SetDefault<ISigningProvider>("throwing");

        var result = await SignData("test");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SigningError");
    }

    #endregion

    private class MockSigningProvider : ISigningProvider
    {
        private readonly Ed25519Provider _inner = new();
        public string Name { get; }
        public bool IsDefault { get; set; }
        public bool SignCalled { get; private set; }

        public MockSigningProvider(string name) { Name = name; }

        public global::App.Data.@this<KeyPair> GenerateKeyPair() => _inner.GenerateKeyPair();
        public Data Sign(byte[] data, string privateKey) => _inner.Sign(data, privateKey);
        public Data Verify(byte[] data, byte[] signature, string publicKey) => _inner.Verify(data, signature, publicKey);
        public async Task<Data> SignAsync(sign action) { SignCalled = true; return await _inner.SignAsync(action); }
        public Task<Data> VerifyAsync(verify action) => _inner.VerifyAsync(action);
    }

    private class ThrowingSigningProvider : ISigningProvider
    {
        public string Name => "throwing";
        public bool IsDefault { get; set; }
        public global::App.Data.@this<KeyPair> GenerateKeyPair() => global::App.Data.@this<KeyPair>.FromError(new ActionError("Key generation failed", "KeyGenerationError", 500));
        public Data Sign(byte[] data, string privateKey) => Data.FromError(new ActionError("Sign failed", "SigningError", 500));
        public Data Verify(byte[] data, byte[] signature, string publicKey) => Data.FromError(new ActionError("Verify failed", "SignatureInvalid", 400));
        public Task<Data> SignAsync(sign action) => Task.FromResult(Data.FromError(new ActionError("Sign failed", "SigningError", 500)));
        public Task<Data> VerifyAsync(verify action) => Task.FromResult(Data.FromError(new ActionError("Verify failed", "SignatureInvalid", 400)));
    }

    private class ThrowingKeyProvider : IKeyProvider
    {
        public string Name => "throwing-key";
        public bool IsDefault { get; set; }
        public global::App.Data.@this<KeyPair> GenerateKeyPair() => global::App.Data.@this<KeyPair>.FromError(new ActionError("Key generation failed", "KeyGenerationError", 500));
    }
}
