using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class DefaultProviderTests
{
    private readonly DefaultProvider _provider = new();

    // --- Hash: Keccak256 ---

    [Test]
    public async Task Hash_Keccak256_ProducesCorrectHash()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("test");
        var result = _provider.Hash(input, "keccak256");
        var hex = Convert.ToHexString((byte[])result.Value!).ToLowerInvariant();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(hex).IsEqualTo("9c22ff5f21f0b81b113e63f7db6da94fedef11b2119b4088b89664fb9a3cb658");
    }

    [Test]
    public async Task Hash_SHA256_ProducesCorrectHash()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("test");
        var result = _provider.Hash(input, "sha256");
        var hex = Convert.ToHexString((byte[])result.Value!).ToLowerInvariant();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(hex).IsEqualTo("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
    }

    [Test]
    [Skip("Bcrypt deferred — not needed for signing module")]
    public async Task Hash_Bcrypt_ProducesValidHash()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("password123");
        var result = _provider.Hash(input, "bcrypt");
        var bcryptString = System.Text.Encoding.UTF8.GetString((byte[])result.Value!);

        await Assert.That(bcryptString).StartsWith("$2");
    }

    [Test]
    [Skip("Bcrypt deferred — not needed for signing module")]
    public async Task Hash_Bcrypt_SameInput_DifferentHashes()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("password123");
        var result1 = _provider.Hash(input, "bcrypt");
        var result2 = _provider.Hash(input, "bcrypt");

        await Assert.That((byte[])result1.Value!).IsNotEquivalentTo((byte[])result2.Value!);
    }

    [Test]
    public async Task Hash_UnknownAlgorithm_ReturnsError()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("test");
        var result = _provider.Hash(input, "md5");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }

    [Test]
    public async Task Hash_EmptyInput_DoesNotFail()
    {
        var input = Array.Empty<byte>();
        var result = _provider.Hash(input, "keccak256");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(((byte[])result.Value!).Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Hash_Keccak256_OutputIs32Bytes()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("any data");
        var result = _provider.Hash(input, "keccak256");

        await Assert.That(((byte[])result.Value!).Length).IsEqualTo(32);
    }

    [Test]
    public async Task Hash_SHA256_OutputIs32Bytes()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("any data");
        var result = _provider.Hash(input, "sha256");

        await Assert.That(((byte[])result.Value!).Length).IsEqualTo(32);
    }

    // --- Verify ---

    [Test]
    public async Task Verify_Keccak256_RoundTrip_ReturnsTrue()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("hello world");
        var hashResult = _provider.Hash(input, "keccak256");
        var result = _provider.Verify(input, (byte[])hashResult.Value!, "keccak256");

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_Keccak256_WrongData_ReturnsFalse()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("hello world");
        var hashResult = _provider.Hash(input, "keccak256");
        var wrongInput = System.Text.Encoding.UTF8.GetBytes("different data");
        var result = _provider.Verify(wrongInput, (byte[])hashResult.Value!, "keccak256");

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_SHA256_RoundTrip_ReturnsTrue()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("hello world");
        var hashResult = _provider.Hash(input, "sha256");
        var result = _provider.Verify(input, (byte[])hashResult.Value!, "sha256");

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Verify_SHA256_WrongHash_ReturnsFalse()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("hello world");
        var hashResult = _provider.Hash(input, "sha256");
        var hash = (byte[])hashResult.Value!;
        hash[0] = (byte)(hash[0] ^ 0xFF);
        var result = _provider.Verify(input, hash, "sha256");

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    [Skip("Bcrypt deferred — not needed for signing module")]
    public async Task Verify_Bcrypt_CorrectPassword_ReturnsTrue()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("mypassword");
        var hashResult = _provider.Hash(input, "bcrypt");
        var result = _provider.Verify(input, (byte[])hashResult.Value!, "bcrypt");

        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    [Skip("Bcrypt deferred — not needed for signing module")]
    public async Task Verify_Bcrypt_WrongPassword_ReturnsFalse()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("mypassword");
        var hashResult = _provider.Hash(input, "bcrypt");
        var wrongInput = System.Text.Encoding.UTF8.GetBytes("wrongpassword");
        var result = _provider.Verify(wrongInput, (byte[])hashResult.Value!, "bcrypt");

        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Verify_UnknownAlgorithm_ReturnsError()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("test");
        var fakeHash = new byte[32];
        var result = _provider.Verify(input, fakeHash, "md5");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }
}
