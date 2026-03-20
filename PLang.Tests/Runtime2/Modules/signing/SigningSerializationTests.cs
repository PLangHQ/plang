using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.signing;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests deterministic serialization, null-signature pattern, base64 encoding.
/// </summary>
public class SigningSerializationTests
{
    [Test]
    public async Task SignedData_NullSignature_SerializedAsNull()
    {
        var sd = CreateTestSignedData();
        sd.Signature = null;

        var json = JsonSerializer.Serialize(sd, SignedData.SigningOptions);
        await Assert.That(json).Contains("\"signature\":null");
    }

    [Test]
    public async Task SignedData_JsonPropertyOrder_StableBytes()
    {
        var sd1 = CreateTestSignedData();
        var sd2 = CreateTestSignedData();

        var bytes1 = JsonSerializer.SerializeToUtf8Bytes(sd1, SignedData.SigningOptions);
        var bytes2 = JsonSerializer.SerializeToUtf8Bytes(sd2, SignedData.SigningOptions);

        await Assert.That(bytes1.AsSpan().SequenceEqual(bytes2)).IsTrue();
    }

    [Test]
    public async Task SignedData_CamelCaseNaming()
    {
        var sd = CreateTestSignedData();
        var json = JsonSerializer.Serialize(sd, SignedData.SigningOptions);

        await Assert.That(json).Contains("\"type\":");
        await Assert.That(json).Contains("\"algorithm\":");
        await Assert.That(json).Contains("\"nonce\":");
    }

    [Test]
    public async Task SignedData_UnsafeRelaxedEscaping()
    {
        var sd = CreateTestSignedData();
        sd.HashedData = new HashedData { Algorithm = "sha256", Format = "json", Hash = "héllo" };

        var json = JsonSerializer.Serialize(sd, SignedData.SigningOptions);
        // Non-ASCII should appear literally, not as \uXXXX
        await Assert.That(json).Contains("héllo");
    }

    [Test]
    public async Task SignedData_Roundtrip_Deserialize()
    {
        var original = CreateTestSignedData();
        original.Signature = Convert.ToBase64String(new byte[64]);

        var json = JsonSerializer.Serialize(original, SignedData.SigningOptions);
        var deserialized = JsonSerializer.Deserialize<SignedData>(json, SignedData.SigningOptions)!;

        await Assert.That(deserialized.Type).IsEqualTo(original.Type);
        await Assert.That(deserialized.Algorithm).IsEqualTo(original.Algorithm);
        await Assert.That(deserialized.Nonce).IsEqualTo(original.Nonce);
        await Assert.That(deserialized.Identity).IsEqualTo(original.Identity);
        await Assert.That(deserialized.Signature).IsEqualTo(original.Signature);
    }

    [Test]
    public async Task HashedData_Hash_IsBase64_NotHex()
    {
        var provider = new Ed25519Provider();
        var keys = provider.GenerateKeyPair();

        // Hash some data using the crypto module's FormatHash
        var data = System.Text.Encoding.UTF8.GetBytes("test data");
        var cryptoProvider = new PLang.Runtime2.modules.crypto.providers.DefaultCryptoProvider();
        var hashResult = cryptoProvider.Hash(data, "sha256");
        var hashBytes = (byte[])hashResult.Value!;
        var base64Hash = PLang.Runtime2.modules.crypto.HashedData.FormatHash(hashBytes);

        // Should be valid base64
        var decoded = Convert.FromBase64String(base64Hash);
        await Assert.That(decoded.Length).IsEqualTo(32);
        // base64 of 32 bytes = 44 chars (padded), hex would be 64 chars
        await Assert.That(base64Hash.Length).IsEqualTo(44);
    }

    [Test]
    public async Task SignedData_Signature_IsBase64()
    {
        var provider = new Ed25519Provider();
        var keys = provider.GenerateKeyPair();

        var data = System.Text.Encoding.UTF8.GetBytes("test");
        var sigResult = provider.Sign(data, keys.PrivateKey);
        var sig = (byte[])sigResult.Value!;
        var base64Sig = Convert.ToBase64String(sig);

        var decoded = Convert.FromBase64String(base64Sig);
        await Assert.That(decoded.Length).IsEqualTo(64); // Ed25519 signature = 64 bytes
    }

    [Test]
    public async Task SignedData_Verified_ExcludedFromJson()
    {
        // Verified is [JsonIgnore] — verify by checking it's not in the serialized output
        var sd = CreateTestSignedData();

        var json = JsonSerializer.Serialize(sd, SignedData.SigningOptions);
        await Assert.That(json).DoesNotContain("\"verified\"");
    }

    [Test]
    public async Task HashedData_InvalidBase64_VerifyRejects()
    {
        // Create a SignedData with invalid base64 in HashedData.Hash
        // Verification should handle it gracefully (the hash comparison will fail)
        var sd = CreateTestSignedData();
        sd.HashedData = new HashedData { Algorithm = "sha256", Format = "json", Hash = "not-valid-base64!!!" };
        sd.Signature = Convert.ToBase64String(new byte[64]);
        sd.Contracts = new List<string> { "C0" };

        // Verify should fail (hash mismatch at minimum) but not throw
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_ser_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var engine = new PLang.Runtime2.Engine.@this(tempDir);
            var signedData = Data.Ok("test");
            signedData.Signature = sd;

            var action = new verify
            {
                Context = engine.Context,
                Data = signedData,
                Contracts = new List<string> { "C0" }
            };
            var result = await sd.VerifyAsync(action);
            await Assert.That(result.Success).IsFalse();
            await engine.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    private static SignedData CreateTestSignedData()
    {
        return new SignedData
        {
            Type = "signature",
            Algorithm = "ed25519",
            Nonce = "abc123",
            Created = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Identity = "testPublicKey",
            Contracts = new List<string> { "C0" },
            HashedData = new HashedData { Algorithm = "sha256", Format = "json", Hash = Convert.ToBase64String(new byte[32]) },
            Signature = null
        };
    }
}
