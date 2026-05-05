namespace PLang.Tests.App.Modules.crypto;

public class CryptoV1PassThroughTests
{
    [Test]
    public async Task CryptoEncrypt_V1_ReturnsInputUnchanged()
    {
        // v1 body: identity. byte[] in == byte[] out.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CryptoDecrypt_V1_ReturnsInputUnchanged()
    {
        // v1 body: identity. byte[] in == byte[] out.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CryptoEncryptDecrypt_V1_RoundTripIsByteIdentical()
    {
        // encrypt(decrypt(x)) == x, byte-for-byte. Wiring runs even though crypto is no-op.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CryptoEncrypt_AndCryptoDecrypt_AreAsync()
    {
        // Returns Task<byte[]> — the contract is async even though v1 returns immediately.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
