namespace PLang.Tests.App.actions.screen;

/// <summary>A frame's pixels are bounded by <c>screen.setting.max</c> before any decode: a few KB of PNG that claims a
/// huge picture is refused as BadFrame, never decoded (security, packages).</summary>
public class FrameBoundTests
{
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

    [Test]
    public async Task ABlankPng_ThatClaims8000x8000_IsRefusedAsBadFrame_AtTheDefaultMax()
    {
        var png = BlankPng(8000, 8000);
        await using var app = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        var max = (await app.actor.list.User.Context.Setting.Get<global::app.module.screen.setting.@this>()).Max;

        var refused = Assert.Throws<global::app.error.AppException>(() => _ = new global::app.module.screen.type.screen.code.Bitmap(png, max));

        await Assert.That(png.Length).IsLessThan(1_000_000);
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
        await Assert.That(refused.Error.Message).Contains("screen.setting.max");
    }
}
