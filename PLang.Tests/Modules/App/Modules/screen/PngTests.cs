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
        await Assert.That(view.Show(Frame(shown))).IsTrue();

        // the view applies frames on its own thread: its picture is read until it holds the frame
        Bitmap? read = null;
        for (var i = 0; i < 100 && read?.Pixels.SequenceEqual(shown) != true; i++)
        {
            await Task.Delay(20);
            read = new Bitmap(view.Picture.Bytes);
        }

        await Assert.That(read).IsNotNull();
        await Assert.That((read!.Width, read.Height)).IsEqualTo((W, H));
        await Assert.That(read.Pixels.SequenceEqual(shown)).IsTrue();
        view.Close();
    }

    [Test]
    public async Task BytesThatAreNoImage_AreRefusedAsAFrame()
        => await RefusedAsBadFrame([1, 2, 3, 4, 5, 6, 7, 8]);

    private static async Task RefusedAsBadFrame(byte[] bytes)
    {
        var refused = await Assert.That(() => new Bitmap(bytes)).Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
    }

    // a PNG whose header and CRCs are right, 50000×50000 declared, its data five junk bytes: the decoder fails inside
    [Test]
    public async Task APngWhosePixelsAreJunk_IsRefusedAsAFrame()
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, 50000);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 50000);
        header[8] = 8; header[9] = 6;   // 8-bit RGBA
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", [1, 2, 3, 4, 5]);
        Chunk(png, "IEND", []);
        await RefusedAsBadFrame(png.ToArray());
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
