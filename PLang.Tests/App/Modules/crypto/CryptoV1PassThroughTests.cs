using global::App.modules.crypto;

namespace PLang.Tests.App.Modules.crypto;

public class CryptoV1PassThroughTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-crypto-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task CryptoEncrypt_V1_ReturnsInputUnchanged()
    {
        var app = NewApp();
        var input = new byte[] { 1, 2, 3, 4 };
        var result = await app.RunAction<encrypt>(
            new encrypt { Input = global::App.Data.@this<byte[]>.Ok(input) }, app.User.Context);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo(input);
    }

    [Test]
    public async Task CryptoDecrypt_V1_ReturnsInputUnchanged()
    {
        var app = NewApp();
        var input = new byte[] { 9, 8, 7 };
        var result = await app.RunAction<decrypt>(
            new decrypt { Input = global::App.Data.@this<byte[]>.Ok(input) }, app.User.Context);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo(input);
    }

    [Test]
    public async Task CryptoEncryptDecrypt_V1_RoundTripIsByteIdentical()
    {
        var app = NewApp();
        var input = new byte[] { 11, 22, 33, 44, 55 };
        var encrypted = await app.RunAction<encrypt>(
            new encrypt { Input = global::App.Data.@this<byte[]>.Ok(input) }, app.User.Context);
        var encryptedBytes = (byte[])encrypted.Value!;
        var decrypted = await app.RunAction<decrypt>(
            new decrypt { Input = global::App.Data.@this<byte[]>.Ok(encryptedBytes) }, app.User.Context);
        await Assert.That(decrypted.Value).IsEquivalentTo(input);
    }

    [Test]
    public async Task CryptoEncrypt_AndCryptoDecrypt_AreAsync()
    {
        var enc = typeof(encrypt).GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var dec = typeof(decrypt).GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(enc).IsNotNull();
        await Assert.That(dec).IsNotNull();
        await Assert.That(typeof(System.Threading.Tasks.Task).IsAssignableFrom(enc!.ReturnType)).IsTrue();
        await Assert.That(typeof(System.Threading.Tasks.Task).IsAssignableFrom(dec!.ReturnType)).IsTrue();
    }
}
