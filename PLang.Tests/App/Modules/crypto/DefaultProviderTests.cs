using global::app.Variables;
using global::app.modules.crypto;
using global::app.modules.crypto.code;

namespace PLang.Tests.App.Modules.crypto;

public class DefaultCryptoProviderTests
{
    private readonly global::app.modules.crypto.code.default _provider = new();

    private static Hash HashAction(object data, string algorithm = "keccak256")
        => new() { Data = Data.Ok(data), Algorithm = algorithm };

    private static Verify VerifyAction(object data, string hash, string algorithm = "keccak256")
        => new() { Data = Data.Ok(data), Hash = hash, Algorithm = algorithm };

    // --- Hash ---

    [Test]
    public async Task Hash_Keccak256_ProducesCorrectHash()
    {
        var result = _provider.Hash(HashAction(new byte[] { 116, 101, 115, 116 })); // "test" as raw bytes
        var hex = Convert.ToHexString((byte[])result.Value!).ToLowerInvariant();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(hex).IsEqualTo("9c22ff5f21f0b81b113e63f7db6da94fedef11b2119b4088b89664fb9a3cb658");
    }

    [Test]
    public async Task Hash_SHA256_ProducesCorrectHash()
    {
        var result = _provider.Hash(HashAction(new byte[] { 116, 101, 115, 116 }, "sha256"));
        var hex = Convert.ToHexString((byte[])result.Value!).ToLowerInvariant();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(hex).IsEqualTo("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
    }

    [Test]
    public async Task Hash_UnknownAlgorithm_ReturnsError()
    {
        var result = _provider.Hash(HashAction("test", "md5"));

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }

    [Test]
    public async Task Hash_EmptyInput_DoesNotFail()
    {
        var result = _provider.Hash(HashAction(Array.Empty<byte>()));

        await Assert.That(result.Success).IsTrue();
        await Assert.That(((byte[])result.Value!).Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Hash_Keccak256_OutputIs32Bytes()
    {
        var result = _provider.Hash(HashAction("any data"));
        await Assert.That(((byte[])result.Value!).Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_SHA256_OutputIs32Bytes()
    {
        var result = _provider.Hash(HashAction("any data", "sha256"));
        await Assert.That(((byte[])result.Value!).Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_ReturnsTypeMatchingAlgorithm()
    {
        var keccak = _provider.Hash(HashAction("test"));
        var sha = _provider.Hash(HashAction("test", "sha256"));

        await Assert.That(keccak.Type!.Value).IsEqualTo("keccak256");
        await Assert.That(sha.Type!.Value).IsEqualTo("sha256");
    }

    // --- Verify ---

    [Test]
    public async Task Verify_Keccak256_RoundTrip_ReturnsTrue()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 })); // "hello"
        var base64 = Convert.ToBase64String((byte[])hashResult.Value!);
        var result = _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111 }, base64));

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_Keccak256_WrongData_ReturnsFalse()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 }));
        var base64 = Convert.ToBase64String((byte[])hashResult.Value!);
        var result = _provider.Verify(VerifyAction(new byte[] { 119, 114, 111, 110, 103 }, base64)); // "wrong"

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_SHA256_RoundTrip_ReturnsTrue()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 }, "sha256"));
        var base64 = Convert.ToBase64String((byte[])hashResult.Value!);
        var result = _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111 }, base64, "sha256"));

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_SHA256_WrongHash_ReturnsFalse()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111 }, "sha256"));
        var hashBytes = (byte[])hashResult.Value!;
        hashBytes[0] ^= 0xFF;
        var result = _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111 }, Convert.ToBase64String(hashBytes), "sha256"));

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_UnknownAlgorithm_ReturnsError()
    {
        var result = _provider.Verify(VerifyAction("test", Convert.ToBase64String(new byte[32]), "md5"));

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }
}
