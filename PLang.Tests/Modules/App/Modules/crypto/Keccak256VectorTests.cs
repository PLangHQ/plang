namespace PLang.Tests.App.Modules.crypto;

/// <summary>BouncyCastle's Keccak-256 is the original Keccak (not FIPS SHA-3): its known answers hold, and feeding the
/// bytes in any chunks gives the one-shot answer (security, packages).</summary>
public class Keccak256VectorTests
{
    private static string Keccak(params byte[][] parts)
    {
        var d = new Org.BouncyCastle.Crypto.Digests.KeccakDigest(256);
        foreach (var p in parts) d.BlockUpdate(p, 0, p.Length);
        var h = new byte[32]; d.DoFinal(h, 0);
        return System.Convert.ToHexString(h).ToLowerInvariant();
    }

    [Test]
    public async Task TheKnownAnswers_AndAnyChunking_Agree()
    {
        await Assert.That(Keccak()).IsEqualTo("c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470");
        await Assert.That(Keccak("abc"u8.ToArray())).IsEqualTo("4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45");
        var big = new byte[1000]; new System.Random(7).NextBytes(big);
        await Assert.That(Keccak(big)).IsEqualTo(Keccak(big[..1], big[1..135], big[135..136], big[136..]));
    }
}
