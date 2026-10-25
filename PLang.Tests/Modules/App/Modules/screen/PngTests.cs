using System.Buffers.Binary;
using Bitmap = app.module.screen.type.screen.code.Bitmap;
using Qoi = app.module.screen.type.screen.code.Qoi;
using View = app.module.screen.type.screen.view.@this;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A screen's picture as PNG: the view writes the pixels it shows as a PNG image, and a bitmap reads a PNG frame
/// back into the same straight BGRA pixels, alpha and all. Bytes that are no image are refused, never shown.
/// </summary>
public class PngTests
{
    private const int W = 37, H = 23;

    // a full-picture frame message: [kind 1][u16 count] then the rectangle's head and its QOI
    private static byte[] Frame(byte[] px)
    {
        using var m = new MemoryStream();
        m.WriteByte(1);
        var i32 = new byte[4];
        void Int(int v) { BinaryPrimitives.WriteInt32LittleEndian(i32, v); m.Write(i32); }
        m.Write(BitConverter.GetBytes((ushort)1));
        var q = new byte[Qoi.MaxSize(W, H)];
        var n = new Qoi(px, W, H).Into(q);
        Int(0); Int(0); Int(W); Int(H); Int(n);
        m.Write(q, 0, n);
        return m.ToArray();
    }

    [Test]
    public async Task TheViewsPicture_ReadByTheWindow_IsThePixelsItShows()
    {
        await using var app = new global::app.@this("/pngtests", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var shown = new byte[W * H * 4];
        new Random(7).NextBytes(shown);
        var view = (View)(await View.Start("png", W, H, null, null, context).Value())!;
        var max = await Max(context);
        await Assert.That(view.Show(Frame(shown), max)).IsTrue();

        // the view applies frames on its own thread: its picture is read until it holds the frame
        Bitmap? read = null;
        for (var i = 0; i < 100 && read?.Pixels.SequenceEqual(shown) != true; i++)
        {
            await Task.Delay(20);
            read = new Bitmap(view.Picture.Bytes, max);
        }

        await Assert.That(read).IsNotNull();
        await Assert.That((read!.Width, read.Height)).IsEqualTo((W, H));
        await Assert.That(read.Pixels.SequenceEqual(shown)).IsTrue();
        view.Close();
    }

    [Test]
    public async Task BytesThatAreNoImage_AreRefusedAsAFrame()
    {
        await using var app = new global::app.@this("/pngtests", global::PLang.Tests.Parent.Actor).Testing();
        await RefusedAsBadFrame([1, 2, 3, 4, 5, 6, 7, 8], await Max(app.actor.list.User.Context));
    }

    // a PNG whose header and CRCs are right, 50000×50000 declared, its data five junk bytes: with a max that lets
    // it through, the decoder fails inside
    [Test]
    public async Task APngWhosePixelsAreJunk_IsRefusedAsAFrame()
    {
        await using var app = new global::app.@this("/pngtests", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        await SetMax("16 GiB", context);
        await RefusedAsBadFrame(Png(50000, 50000, [1, 2, 3, 4, 5]), await Max(context));
    }

    // a frame whose pixels would take more than screen.setting.max (128 MiB): refused before the decoder makes one,
    // the message naming the setting
    [Test]
    public async Task APngThatClaimsMoreThanTheMax_IsRefusedAsAFrame()
    {
        await using var app = new global::app.@this("/pngtests", global::PLang.Tests.Parent.Actor).Testing();
        var refused = await RefusedAsBadFrame(Png(8000, 8000, [1, 2, 3, 4, 5]), await Max(app.actor.list.User.Context));
        await Assert.That(refused.Error.Message).Contains("--screen.setting.max=");
    }

    // a frame past a low max is refused; the max raised, the same frame reads
    [Test]
    public async Task AFramePastTheMax_ReadsOnceTheMaxIsRaised()
    {
        await using var app = new global::app.@this("/pngtests", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var png = BlankPng(600, 600);   // 600 × 600 × 4 = 1.44 MB of pixels
        await SetMax("1 MiB", context);
        await RefusedAsBadFrame(png, await Max(context));

        await SetMax("2 MiB", context);
        var read = new Bitmap(png, await Max(context));
        await Assert.That((read.Width, read.Height)).IsEqualTo((600, 600));
    }

    private static async Task<global::app.type.item.size.@this> Max(global::app.actor.context.@this context)
        => (await context.Setting.Get<global::app.module.screen.setting.@this>()).Max;

    private static async Task SetMax(string max, global::app.actor.context.@this context)
        => await (await new global::app.type.item.variable.parser.@this("%!screen.setting.max%", context.App.type.list).Variable.Single()
            .Set(context.Ok(context.Text(max)), context)).IsSuccess();

    private static async Task<global::app.error.AppException> RefusedAsBadFrame(byte[] bytes, global::app.type.item.size.@this max)
    {
        var refused = await Assert.That(() => new Bitmap(bytes, max)).Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
        return refused;
    }

    // a w×h 8-bit RGBA PNG holding idat as its data
    private static byte[] Png(int w, int h, byte[] idat)
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, w);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), h);
        header[8] = 8; header[9] = 6;   // 8-bit RGBA
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", idat);
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    // a w×h PNG of transparent black: each row its filter byte and zeros, deflated
    private static byte[] BlankPng(int w, int h)
    {
        using var raw = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(raw, System.IO.Compression.CompressionLevel.SmallestSize, true))
        {
            var row = new byte[w * 4 + 1];
            for (var y = 0; y < h; y++) z.Write(row);
        }
        return Png(w, h, raw.ToArray());
    }

    private static void Chunk(MemoryStream png, string kind, byte[] data)
    {
        var four = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(four, (uint)data.Length);
        png.Write(four);
        var body = System.Text.Encoding.ASCII.GetBytes(kind).Concat(data).ToArray();
        png.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(four, Crc(body));
        png.Write(four);
    }

    private static uint Crc(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }
}
