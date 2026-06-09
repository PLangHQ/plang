using app.variable;
using app.module.crypto;
using app.module.crypto.code;
using hash = global::app.module.crypto.type.hash.@this;

namespace PLang.Tests.App.Modules.crypto;

public class DefaultCryptoProviderTests
{
    private readonly global::app.module.crypto.code.Default _provider = new();

    private static Hash HashAction(object data, string algorithm = "keccak256")
        => new() { Data = Data.Ok(data), Algorithm = (global::app.type.text.@this)algorithm };

    private static Verify VerifyAction(object data, string expectedHash, string algorithm = "keccak256")
        => new() { Data = Data.Ok(data), Hash = Data.Ok(expectedHash), Algorithm = (global::app.type.text.@this)algorithm };

    // --- Hash ---

    [Test]
    public async Task Hash_Keccak256_ProducesCorrectHash()
    {
        var result = _provider.Hash(HashAction(new byte[] { 116, 101, 115, 116 })); // "test" as raw bytes
        var hex = Convert.ToHexString(((hash)(await result.Value())!).Bytes).ToLowerInvariant();

        await result.IsSuccess();
        await Assert.That(hex).IsEqualTo("9c22ff5f21f0b81b113e63f7db6da94fedef11b2119b4088b89664fb9a3cb658");
    }

    [Test]
    public async Task Hash_SHA256_ProducesCorrectHash()
    {
        var result = await _provider.Hash(HashAction(new byte[] { 116, 101, 115, 116 }, "sha256"));
        var hex = Convert.ToHexString(((hash)(await result.Value())!).Bytes).ToLowerInvariant();

        await result.IsSuccess();
        await Assert.That(hex).IsEqualTo("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
    }

    [Test]
    public async Task Hash_UnknownAlgorithm_ReturnsError()
    {
        var result = await _provider.Hash(HashAction("test", "md5"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }

    [Test]
    public async Task Hash_EmptyInput_DoesNotFail()
    {
        var result = await _provider.Hash(HashAction(Array.Empty<byte>()));

        await result.IsSuccess();
        await Assert.That(((hash)(await result.Value())!).Bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Hash_Keccak256_OutputIs32Bytes()
    {
        var result = await _provider.Hash(HashAction("any data"));
        await Assert.That(((hash)(await result.Value())!).Bytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_SHA256_OutputIs32Bytes()
    {
        var result = await _provider.Hash(HashAction("any data", "sha256"));
        await Assert.That(((hash)(await result.Value())!).Bytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_ReturnsTypeMatchingAlgorithm()
    {
        var keccak = await _provider.Hash(HashAction("test"));
        var sha = await _provider.Hash(HashAction("test", "sha256"));

        // Stage 7: name is "hash"; the algorithm is the value's KIND.
        await Assert.That(keccak.Type!.Name).IsEqualTo("hash");
        await Assert.That(keccak.Type!.Kind).IsEqualTo("keccak256");
        await Assert.That(sha.Type!.Name).IsEqualTo("hash");
        await Assert.That(sha.Type!.Kind).IsEqualTo("sha256");
    }

    // --- Verify ---

    [Test]
    public async Task Verify_Keccak256_RoundTrip_ReturnsTrue()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 })); // "hello"
        var base64 = ((hash)(await hashResult.Value())!).ToBase64();
        var result = await _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111 }, base64));

        await result.IsSuccess();
        await Assert.That((bool)(await result.Value())!).IsTrue();
    }

    [Test]
    public async Task Verify_Keccak256_WrongData_ReturnsFalse()
    {
        var hashResult = await _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 }));
        var base64 = ((hash)(await hashResult.Value())!).ToBase64();
        var result = _provider.Verify(VerifyAction(new byte[] { 119, 114, 111, 110, 103 }, base64)); // "wrong"

        await result.IsSuccess();
        await Assert.That((bool)(await result.Value())!).IsFalse();
    }

    [Test]
    public async Task Verify_SHA256_RoundTrip_ReturnsTrue()
    {
        var hashResult = await _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 }, "sha256"));
        var base64 = ((hash)(await hashResult.Value())!).ToBase64();
        var result = await _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111 }, base64, "sha256"));

        await result.IsSuccess();
        await Assert.That((bool)(await result.Value())!).IsTrue();
    }

    [Test]
    public async Task Verify_SHA256_WrongHash_ReturnsFalse()
    {
        var hashResult = await _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 }, "sha256"));
        var hashBytes = ((hash)(await hashResult.Value())!).Bytes.ToArray();
        hashBytes[0] ^= 0xFF;
        var result = await _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111 }, Convert.ToBase64String(hashBytes), "sha256"));

        await result.IsSuccess();
        await Assert.That((bool)(await result.Value())!).IsFalse();
    }

    [Test]
    public async Task Verify_UnknownAlgorithm_ReturnsError()
    {
        var result = await _provider.Verify(VerifyAction("test", Convert.ToBase64String(new byte[32]), "md5"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }
}
