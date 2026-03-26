using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class DefaultCryptoProviderTests
{
    private readonly DefaultCryptoProvider _provider = new();

    private static Hash HashAction(object data, string algorithm = "keccak256")
        => new() { Data = data, Algorithm = algorithm };

    private static Verify VerifyAction(object data, string hash, string algorithm = "keccak256")
        => new() { Data = data, Hash = hash, Algorithm = algorithm };

    // --- Hash: Keccak256 ---

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
        var result = _provider.Hash(HashAction(new byte[] { 116, 101, 115, 116 }, "sha256")); // "test" as raw bytes
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

    // --- Verify ---

    [Test]
    public async Task Verify_Keccak256_RoundTrip_ReturnsTrue()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 })); // "hello world"
        var base64Hash = Convert.ToBase64String((byte[])hashResult.Value!);
        var result = _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }, base64Hash));

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_Keccak256_WrongData_ReturnsFalse()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }));
        var base64Hash = Convert.ToBase64String((byte[])hashResult.Value!);
        var result = _provider.Verify(VerifyAction(new byte[] { 100, 105, 102, 102, 101, 114, 101, 110, 116 }, base64Hash)); // "different"

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_SHA256_RoundTrip_ReturnsTrue()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }, "sha256"));
        var base64Hash = Convert.ToBase64String((byte[])hashResult.Value!);
        var result = _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }, base64Hash, "sha256"));

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_SHA256_WrongHash_ReturnsFalse()
    {
        var hashResult = _provider.Hash(HashAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }, "sha256"));
        var hash = (byte[])hashResult.Value!;
        hash[0] = (byte)(hash[0] ^ 0xFF);
        var base64Hash = Convert.ToBase64String(hash);
        var result = _provider.Verify(VerifyAction(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }, base64Hash, "sha256"));

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
