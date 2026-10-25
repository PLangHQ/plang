using System.Buffers.Binary;
using Shown = app.module.screen.type.screen.code.Shown;

namespace PLang.Tests.App.actions.screen;

/// <summary>Security probe (bugs full pass): a frame is bounded per rectangle, but a message holds up to 65535 of them.
/// A few MB of frame naming rectangles at the max, with no pixels behind them, must not cost the host gigabytes.
/// Measures what the host allocates for it; asserts a bound on the bytes allocated per message byte. RED BY DESIGN (F2,
/// Low): each core rents a buffer of the claimed size before the QOI is looked at, so a 40 KB frame allocates ~1 GB
/// (uninitialised, mostly untouched pages) and the pool keeps them.</summary>
public class FrameAmplificationProbeTests
{
    private static byte[] Frame(int count, int w, int h)
    {
        using var m = new MemoryStream();
        m.WriteByte(1);
        m.Write(BitConverter.GetBytes((ushort)count));
        var i32 = new byte[4];
        void Int(int v) { BinaryPrimitives.WriteInt32LittleEndian(i32, v); m.Write(i32); }
        for (var i = 0; i < count; i++) { Int(0); Int(0); Int(w); Int(h); Int(0); }   // n = 0: no pixels behind the claim
        return m.ToArray();
    }

    [Test]
    public async Task ManyRectanglesAtTheMax_WithNoPixels_CostTheHostLittle()
    {
        await using var app = new global::app.@this("/ampprobe", global::PLang.Tests.Parent.Actor).Testing();
        var max = (await app.actor.list.User.Context.Setting.Get<global::app.module.screen.setting.@this>()).Max;
        var shown = new Shown(64, 48);
        var message = Frame(2000, 5000, 5000);       // 5000×5000×4 = 100 MB each, under the default max (128 MiB)

        var before = GC.GetTotalAllocatedBytes(true);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try { shown.Apply(message, max); } catch (global::app.error.AppException) { }
        clock.Stop();
        var allocated = GC.GetTotalAllocatedBytes(true) - before;

        Console.Error.WriteLine($"PROBE message={message.Length} B, allocated={allocated / 1_000_000} MB, {clock.ElapsedMilliseconds} ms");
        // a 40 KB message must not allocate more than 1000× itself
        await Assert.That(allocated).IsLessThan(message.Length * 1000L);
    }
}
