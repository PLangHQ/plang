using NSec.Cryptography;

namespace PLang.Tests.Runtime.App.IdentityLeak;

/// Security probes (packages, security's pass): NSec 26 (Ed25519 semantics), BouncyCastle's Keccak-256, SkiaSharp's
/// decode of a window frame, Fluid 2.40's include lookup. Each asserts the SAFE behaviour: red is a hole.
public class PackagesPassProbeTests
{
    // ---- NSec 26.4.0: Ed25519 verification stays strict ----

    private static byte[] H(string hex) => System.Convert.FromHexString(hex);

    // RFC 8032 test 1 (empty message)
    private const string Pk = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";
    private const string Sig = "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private static bool Verifies(byte[] pk, byte[] message, byte[] sig)
    {
        var key = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, pk, KeyBlobFormat.RawPublicKey);
        return SignatureAlgorithm.Ed25519.Verify(key, message, sig);
    }

    [Test] public async Task Ed25519_TheRfcVector_Verifies() => await Assert.That(Verifies(H(Pk), [], H(Sig))).IsTrue();

    [Test] public async Task Ed25519_ATamperedMessage_DoesNotVerify() => await Assert.That(Verifies(H(Pk), [0], H(Sig))).IsFalse();

    [Test]
    public async Task Ed25519_ANonCanonicalS_DoesNotVerify()
    {
        // S + L (the group order) is the same scalar mod L: a malleable twin of the signature that must be refused
        var sig = H(Sig);
        var l = H("edd3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010");
        var carry = 0;
        for (var i = 0; i < 32; i++) { var v = sig[32 + i] + l[i] + carry; sig[32 + i] = (byte)v; carry = v >> 8; }
        await Assert.That(Verifies(H(Pk), [], sig)).IsFalse();
    }

    [Test]
    public async Task Ed25519_ASmallOrderKey_WithAZeroSignature_DoesNotVerify()
    {
        var identity = new byte[32]; identity[0] = 1;       // the curve's identity point
        var sig = new byte[64]; sig[0] = 1;                  // R = identity, S = 0
        bool ok;
        try { ok = Verifies(identity, [], sig); } catch (System.Exception) { ok = false; }
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task Ed25519_AShortSignature_DoesNotVerifyAndDoesNotThrow()
    {
        var short63 = H(Sig)[..63];
        await Assert.That(Verifies(H(Pk), [], short63)).IsFalse();
    }

    // ---- BouncyCastle Keccak-256: the original padding, not FIPS SHA-3 ----

    private static string Keccak(params byte[][] parts)
    {
        var d = new Org.BouncyCastle.Crypto.Digests.KeccakDigest(256);
        foreach (var p in parts) d.BlockUpdate(p, 0, p.Length);
        var h = new byte[32]; d.DoFinal(h, 0);
        return System.Convert.ToHexString(h).ToLowerInvariant();
    }

    [Test]
    public async Task Keccak_TheKnownAnswers_AndAnyChunking_Agree()
    {
        await Assert.That(Keccak()).IsEqualTo("c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470");
        await Assert.That(Keccak("abc"u8.ToArray())).IsEqualTo("4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45");
        var big = new byte[1000]; new System.Random(7).NextBytes(big);
        await Assert.That(Keccak(big)).IsEqualTo(Keccak(big[..1], big[1..135], big[135..136], big[136..]));
    }

    // ---- SkiaSharp: a small PNG that decodes to many pixels ----

    private static uint Crc(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data) { crc ^= b; for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1; }
        return ~crc;
    }

    private static void Chunk(System.IO.Stream s, string type, byte[] body)
    {
        var all = System.Text.Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
        s.Write(System.BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(body.Length)));
        s.Write(all);
        s.Write(System.BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(Crc(all))));
    }

    // a w×h 8-bit grey PNG of zeros: a few KB however large
    private static byte[] BlankPng(int w, int h)
    {
        using var raw = new System.IO.MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(raw, System.IO.Compression.CompressionLevel.SmallestSize, true))
        {
            var row = new byte[w + 1];
            for (var y = 0; y < h; y++) z.Write(row);
        }
        using var s = new System.IO.MemoryStream();
        s.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr, w);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), h);
        ihdr[8] = 8;
        Chunk(s, "IHDR", ihdr);
        Chunk(s, "IDAT", raw.ToArray());
        Chunk(s, "IEND", []);
        return s.ToArray();
    }

    // The window decodes whatever frame a browser page sends (window/this.cs:121, 133) with no bound on the size the
    // header claims: a frame of a few hundred KB asks for width×height×4 bytes of pixels, twice (Skia's, then the
    // byte[] copy). Safe = a bound before the decode (a frame past the screen's limits is BadFrame).
    [Test]
    public async Task ABlankPng_ThatClaims8000x8000_IsRefusedBeforeItsPixelsAreAllocated()
    {
        var png = BlankPng(8000, 8000);
        var peakBefore = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64;
        global::app.error.AppException? refused = null;
        global::app.module.screen.type.screen.code.Bitmap? decoded = null;
        try { decoded = new global::app.module.screen.type.screen.code.Bitmap(png); }
        catch (global::app.error.AppException ex) { refused = ex; }
        var peakAfter = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64;

        await Assert.That(png.Length).IsLessThan(1_000_000);
        await Assert.That(refused is not null || (peakAfter - peakBefore) < 64L * 1024 * 1024)
            .IsTrue().Because($"a {png.Length / 1024} KB frame decoded to {decoded?.Width}x{decoded?.Height}, peak working set +{(peakAfter - peakBefore) / (1024 * 1024)} MB");
    }

    // ---- Fluid 2.40: includes still go through the gate ----

    private sealed class Canned : global::app.channel.@this
    {
        public int Asked;
        private readonly global::app.actor.context.@this _ctx;
        public Canned(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        { Asked++; return Task.FromResult(_ctx.Ok("n")); }
    }

    private static async Task<(global::app.data.@this result, Canned asking, string root, string foreign)> Render(string template)
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-pk-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        var foreign = System.IO.Directory.CreateDirectory(root + "-foreign").FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(foreign, "secret.liquid"), "SECRET_TOKEN");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "p"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "p", "ok.liquid"), "IN_ROOT");
        var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var asking = new Canned(user.Context);
        user.Channel.Register(asking);
        var goal = new Goal(app.type.list) { Name = "Host", Path = global::app.type.item.path.@this.Resolve("/host.goal", user.Context) };
        await using var inGoal = user.Context.call.Push(goal, await user.Context.call.Setting());
        var action = new global::app.module.ui.Render(user.Context)
        {
            Template = new global::app.data.@this<global::app.type.item.text.@this>("Template", template, context: user.Context),
            IsFile = new global::app.data.@this<global::app.type.item.@bool.@this>("IsFile", false, context: user.Context),
        };
        var result = await new global::app.module.ui.code.Fluid().Render(action);
        return (result, asking, root, foreign);
    }

    private static async Task<string> Text(global::app.data.@this r) => r.Success ? (await r.Value())?.ToString() ?? "" : r.Error?.Message ?? "";

    [Test]
    public async Task Include_ADotDotName_ReachesNoFurtherThanTheGate()
    {
        // each render makes its own root and its sibling <root>-foreign holding secret.liquid; the name is learned from a
        // first render, then used against a second app whose root has the same shape
        var first = await Render("{% include 'p/ok' %}");
        var foreignName = System.IO.Path.GetFileName(first.foreign);
        var r = await Render("{% include '../" + foreignName + "/secret' %}");
        System.IO.File.WriteAllText(System.IO.Path.Combine(first.foreign, "secret.liquid"), "SECRET_TOKEN");
        var sibling = await Render("{% include '../../" + System.IO.Path.GetFileName(System.IO.Path.GetTempPath().TrimEnd('/')) + "/" + foreignName + "/secret' %}");
        await Assert.That(await Text(r.result)).DoesNotContain("SECRET_TOKEN");
        await Assert.That(await Text(sibling.result)).DoesNotContain("SECRET_TOKEN");
        await Assert.That(await Text((await Render("{% include '../../../../../../../tmp/" + foreignName + "/secret' %}")).result)).DoesNotContain("SECRET_TOKEN");
    }

    [Test]
    public async Task Include_ANameByAVariable_IsRefused_EvenWhenItNamesAnInRootFile()
    {
        var r = await Render("{% assign n = 'p/ok' %}{% include n %}");
        await Assert.That(r.result.Success).IsFalse();
        await Assert.That(r.result.Error!.Key).IsEqualTo("IncludeNotLiteral");
    }

    [Test]
    public async Task Include_ALiteralInRootName_Renders_WithoutAnAsk()
    {
        var r = await Render("{% include 'p/ok' %}");
        await Assert.That(await Text(r.result)).Contains("IN_ROOT");
        await Assert.That(r.asking.Asked).IsEqualTo(0);
    }

    // a variable that spells a DIFFERENT path to a literally included file (./ or case) is still refused
    [Test]
    public async Task Include_AVariableSpellingAnotherPathToTheSameFile_IsRefused()
    {
        var r = await Render("{% include 'p/ok' %}{% assign n = './p/ok' %}{% include n %}");
        await Assert.That(r.result.Success).IsFalse();
    }
}
