namespace PLang.Tests.Runtime.App.IdentityLeak;

/// Security probe (fast-apps decision 94): GHSA-gwg2-r3hj-4w44, ImageSharp's ICC CLUT parse allocates from unvalidated
/// dimensions. Asked through the image type's own door (<c>image.Width</c>): does a PNG or JPEG carrying a crafted ICC
/// profile make the size read allocate without bound or hang? Safe = answers fast and allocates little.
public class ImageIccProfileProbeTests
{
    // an ICC profile with one A2B0 tag, lut8 ('mft1'): 15 input channels, 255 grid points, 3 outputs — a CLUT of
    // 255^15 entries if a reader sizes it from those bytes
    private static byte[] Icc()
    {
        var tag = new List<byte>();
        tag.AddRange("mft1"u8.ToArray()); tag.AddRange(new byte[4]);
        tag.AddRange(new byte[] { 15, 3, 255, 0 });
        tag.AddRange(new byte[36]);                    // the matrix
        tag.AddRange(new byte[256 * 15]);              // input tables
        tag.AddRange(new byte[64]);                    // a little CLUT, far short of what the header claims
        var p = new byte[128 + 4 + 12 + tag.Count];
        void Put(int at, byte[] v) => System.Array.Copy(v, 0, p, at, v.Length);
        Put(0, System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(p.Length) is var n ? System.BitConverter.GetBytes(n) : []);
        Put(8, [2, 0x40, 0, 0]); Put(12, "mntr"u8.ToArray()); Put(16, "RGB "u8.ToArray()); Put(20, "XYZ "u8.ToArray());
        Put(36, "acsp"u8.ToArray());
        Put(128, [0, 0, 0, 1]);
        Put(132, "A2B0"u8.ToArray()); Put(136, [0, 0, 0, 144]); Put(140, System.BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(tag.Count)));
        Put(144, tag.ToArray());
        return p;
    }

    private static uint Crc(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data) { crc ^= b; for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1; }
        return ~crc;
    }

    private static void Chunk(System.IO.Stream s, string type, byte[] body)
    {
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        var all = t.Concat(body).ToArray();
        s.Write(System.BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(body.Length)));
        s.Write(all);
        s.Write(System.BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(Crc(all))));
    }

    private static byte[] Zlib(byte[] raw)
    {
        using var o = new System.IO.MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(o, System.IO.Compression.CompressionLevel.Optimal, true)) z.Write(raw);
        return o.ToArray();
    }

    private static byte[] Png()
    {
        using var s = new System.IO.MemoryStream();
        s.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(s, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 0, 0, 0, 0]);
        Chunk(s, "iCCP", System.Text.Encoding.ASCII.GetBytes("p\0\0").Concat(Zlib(Icc())).ToArray());
        Chunk(s, "IDAT", Zlib([0, 0]));
        Chunk(s, "IEND", []);
        return s.ToArray();
    }

    private static byte[] Jpeg()
    {
        var icc = System.Text.Encoding.ASCII.GetBytes("ICC_PROFILE\0").Concat(new byte[] { 1, 1 }).Concat(Icc()).ToArray();
        using var s = new System.IO.MemoryStream();
        s.Write([0xFF, 0xD8]);
        s.Write([0xFF, 0xE2, (byte)((icc.Length + 2) >> 8), (byte)(icc.Length + 2)]); s.Write(icc);
        // SOF0: 8 bit, 1x1, one component
        s.Write([0xFF, 0xC0, 0, 11, 8, 0, 1, 0, 1, 1, 1, 0x11, 0]);
        s.Write([0xFF, 0xD9]);
        return s.ToArray();
    }

    private static async Task Asked(byte[] bytes, string what)
    {
        await using var app = new global::app.@this("/iccprobe", global::PLang.Tests.Parent.Actor).Testing();
        var image = new global::app.type.item.image.@this(bytes, app.type.list["image"]);
        long allocated = 0;
        var read = System.Threading.Tasks.Task.Run(() =>
        {
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            try { _ = image.Width; } catch (System.Exception) { /* a refusal is fine */ }
            allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        });
        await Assert.That(read.Wait(3000)).IsTrue().Because($"{what}: the size read held past 3 s");
        await Assert.That(allocated).IsLessThan(64L * 1024 * 1024).Because($"{what}: the size read allocated {allocated} bytes");
    }

    [Test] public Task APng_WithACraftedIccClut_SizeReadsQuickly() => Asked(Png(), "png");

    [Test] public Task AJpeg_WithACraftedIccClut_SizeReadsQuickly() => Asked(Jpeg(), "jpeg");

    // the control: the crafted PNG is a PNG the size read accepts (the probe reaches the profile's chunk at all)
    [Test]
    public async Task Control_TheCraftedPng_IsReadAs1x1()
    {
        await using var app = new global::app.@this("/iccprobe", global::PLang.Tests.Parent.Actor).Testing();
        var image = new global::app.type.item.image.@this(Png(), app.type.list["image"]);
        await Assert.That(image.Width).IsEqualTo(1);
    }
}
