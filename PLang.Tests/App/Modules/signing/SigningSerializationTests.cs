using System.Text.Json;
using global::App.Variables;
using global::App.Providers;
using global::App.modules.signing.providers;
using global::App.modules.signing;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Tests deterministic serialization, null-signature pattern, base64 encoding.
/// </summary>
public class SigningSerializationTests
{
    [Test]
    public async Task SignedData_SigningBytes_ExcludesSignature()
    {
        var sd = CreateTestSignedData();
        sd.Value = "some-signature-value";

        // SigningOptions excludes the Signature property for thread-safe signing bytes
        var json = JsonSerializer.Serialize(sd, Signature.SigningOptions);
        await Assert.That(json).DoesNotContain("some-signature-value");
        await Assert.That(json).DoesNotContain("\"signature\":\"");
    }

    [Test]
    public async Task SignedData_JsonPropertyOrder_StableBytes()
    {
        var sd1 = CreateTestSignedData();
        var sd2 = CreateTestSignedData();

        var bytes1 = JsonSerializer.SerializeToUtf8Bytes(sd1, Signature.SigningOptions);
        var bytes2 = JsonSerializer.SerializeToUtf8Bytes(sd2, Signature.SigningOptions);

        await Assert.That(bytes1.AsSpan().SequenceEqual(bytes2)).IsTrue();
    }

    [Test]
    public async Task SignedData_CamelCaseNaming()
    {
        var sd = CreateTestSignedData();
        var json = JsonSerializer.Serialize(sd, Signature.SigningOptions);

        await Assert.That(json).Contains("\"type\":");
        await Assert.That(json).Contains("\"algorithm\":");
        await Assert.That(json).Contains("\"nonce\":");
    }

    [Test]
    public async Task SignedData_Hash_SerializesAsTypeAndValue()
    {
        var sd = CreateTestSignedData();
        var json = JsonSerializer.Serialize(sd, Signature.SigningOptions);

        // Hash field should serialize as { "type": "sha256", "value": "..." }
        await Assert.That(json).Contains("\"hash\":{");
        await Assert.That(json).Contains("\"type\":\"sha256\"");
        await Assert.That(json).Contains("\"value\":\"");
    }

    [Test]
    public async Task SignedData_UnsafeRelaxedEscaping()
    {
        var sd = CreateTestSignedData();
        sd.Identity = "héllo";

        var json = JsonSerializer.Serialize(sd, Signature.SigningOptions);
        // Non-ASCII should appear literally, not as \uXXXX
        await Assert.That(json).Contains("héllo");
    }

    [Test]
    public async Task SignedData_Roundtrip_Deserialize()
    {
        var original = CreateTestSignedData();
        original.Value = Convert.ToBase64String(new byte[64]);

        // Use standard options for general roundtrip (SigningOptions excludes Signature)
        var generalOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(original, generalOptions);
        var deserialized = JsonSerializer.Deserialize<Signature>(json, generalOptions)!;

        await Assert.That(deserialized.Type).IsEqualTo(original.Type);
        await Assert.That(deserialized.Algorithm).IsEqualTo(original.Algorithm);
        await Assert.That(deserialized.Nonce).IsEqualTo(original.Nonce);
        await Assert.That(deserialized.Identity).IsEqualTo(original.Identity);
        await Assert.That(deserialized.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task Hash_IsBase64_NotHex()
    {
        // Hash some data using the crypto provider directly
        var cryptoProvider = new global::App.modules.crypto.providers.DefaultCryptoProvider();
        var hashResult = cryptoProvider.Hash(new global::App.modules.crypto.Hash
            { Data = Data.Ok(System.Text.Encoding.UTF8.GetBytes("test data")), Algorithm = "sha256" });
        var hashBytes = (byte[])hashResult.Value!;
        var base64Hash = Convert.ToBase64String(hashBytes);

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
        var keys = provider.GenerateKeyPair().Value!;

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

        var json = JsonSerializer.Serialize(sd, Signature.SigningOptions);
        await Assert.That(json).DoesNotContain("\"verified\"");
    }

    [Test]
    public async Task Hash_InvalidBase64_VerifyRejects()
    {
        // Create a Signature with invalid base64 in Hash
        // Verification should handle it gracefully (the hash comparison will fail)
        var sd = CreateTestSignedData();
        sd.Hash = Data.Ok(new byte[] { 0xFF }, global::App.Data.Type.FromName("sha256"));
        sd.Value = Convert.ToBase64String(new byte[64]);
        sd.Contracts = new List<string> { "C0" };

        // Verify should fail (hash mismatch at minimum) but not throw
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_ser_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var engine = new global::App.@this(tempDir);
            var signedData = Data.Ok("test");
            signedData.Value = sd;

            var action = new verify
            {
                Context = engine.User.Context,
                Data = signedData,
                Contracts = new List<string> { "C0" }
            };
            var result = await action.Run();
            await Assert.That(result.Success).IsFalse();
            await engine.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SignedData_ToSigningBytes_ThreadSafe()
    {
        var sd = CreateTestSignedData();
        sd.Value = Convert.ToBase64String(new byte[64]);

        // Call ToSigningBytes concurrently — should produce identical results
        // without corrupting Signature (the old bug: mutated Signature to null during serialization)
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() => sd.ToSigningBytes())).ToArray();
        var results = await Task.WhenAll(tasks);

        // All results should be identical
        var first = results[0];
        foreach (var result in results)
            await Assert.That(result.AsSpan().SequenceEqual(first)).IsTrue();

        // Signature should still be intact after all concurrent calls
        await Assert.That(sd.Value).IsEqualTo(Convert.ToBase64String(new byte[64]));
    }

    private static Signature CreateTestSignedData()
    {
        return new Signature
        {
            Type = "signature",
            Algorithm = "ed25519",
            Nonce = "abc123",
            Created = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Identity = "testPublicKey",
            Contracts = new List<string> { "C0" },
            Hash = Data.Ok(new byte[32], global::App.Data.Type.FromName("sha256")),
            Value = null
        };
    }
}
