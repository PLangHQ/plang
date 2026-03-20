namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests deterministic serialization, null-signature pattern, base64 encoding.
/// SignedData must serialize to stable JSON bytes for cryptographic signing.
/// </summary>
public class SigningSerializationTests
{
    [Test]
    public async Task SignedData_NullSignature_SerializedAsNull()
    {
        // "signature": null in JSON (not omitted).
        // The null-signature pattern is critical: Signature is set to null during JSON
        // serialization of the signing payload, then set after signing.
        //
        // Arrange: create SignedData with Signature = null
        // Act: serialize to JSON
        // Assert: JSON string contains "signature": null (field present, value null)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task SignedData_JsonPropertyOrder_StableBytes()
    {
        // Two identical objects → identical JSON bytes.
        // Required for deterministic signature verification.
        //
        // Arrange: create two identical SignedData objects
        // Act: serialize both to JSON bytes
        // Assert: byte arrays are identical
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task SignedData_CamelCaseNaming()
    {
        // JSON uses camelCase: "type", "algorithm", "nonce".
        //
        // Arrange: create SignedData
        // Act: serialize to JSON string
        // Assert: contains "type", "algorithm", "nonce" (not PascalCase)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task SignedData_UnsafeRelaxedEscaping()
    {
        // Non-ASCII characters not double-escaped.
        // Uses UnsafeRelaxedJsonEscaping for canonical JSON.
        //
        // Arrange: create SignedData with non-ASCII data (e.g., "héllo")
        // Act: serialize to JSON
        // Assert: non-ASCII chars appear literally, not as \uXXXX
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task SignedData_Roundtrip_Deserialize()
    {
        // Serialize → deserialize → equal.
        //
        // Arrange: create fully-populated SignedData
        // Act: serialize to JSON, deserialize back
        // Assert: all fields match original
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task HashedData_Hash_IsBase64_NotHex()
    {
        // HashedData.Hash is base64-encoded, not hex.
        //
        // Arrange: create HashedData from signing flow
        // Act: check Hash field
        // Assert: valid base64 (Convert.FromBase64String succeeds), not hex chars only
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task SignedData_Signature_IsBase64()
    {
        // Signature field is base64-encoded.
        //
        // Arrange: create signed SignedData (Signature populated)
        // Act: check Signature field
        // Assert: Convert.FromBase64String(Signature) succeeds, decoded length == 64 (Ed25519)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task SignedData_Verified_ExcludedFromJson()
    {
        // SignedData.Verified is [JsonIgnore] — must NOT appear in serialized JSON.
        // If Verified leaked into the envelope, it would break signature verification.
        //
        // Arrange: create SignedData, set Verified = Data.Ok(true)
        // Act: serialize to JSON string using SigningOptions
        // Assert: JSON does not contain "verified" key
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task HashedData_InvalidBase64_VerifyRejects()
    {
        // Invalid base64 in HashedData.Hash must be caught during verification.
        //
        // Arrange: create SignedData with HashedData.Hash = "not-valid-base64!!!"
        // Act: attempt to verify
        // Assert: error returned (FormatException caught, not thrown)
        await Assert.Fail("stub — implementation depends on signing module");
    }
}
