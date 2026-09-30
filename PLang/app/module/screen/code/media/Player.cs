using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace app.module.screen.code.media;

/// <summary>
/// A redirected video, on the host: it keeps the coded frames PlangOS sends (in the order they are
/// decoded), follows the page's clock (the page's audio runs it: its time, and how it goes on between
/// messages), decodes with Windows a few frames ahead, and shows the one due now — scaled into the
/// video element's box as the page would (contain: its shape kept, black bars) — as a layer the window
/// shows where the page has the key colour. A seek (the page's, or being far behind) starts again at
/// the key frame before.
/// </summary>
internal sealed class Player : IDisposable
{
    private const int Ahead = 3;          // frames decoded before they are due
    private const double Late = 1.0;      // s behind the clock: start again at a key frame

    private readonly int track;
    private readonly Action<int, (int x, int y, int w, int h), (int x, int y, int w, int h), uint[]?> show;
    private readonly Action<int> hide;
    private readonly Action<string> failed;
    private readonly Func<double> delay;
    private readonly List<Coded> frames = new();
    private readonly List<(double time, IntPtr sample)> decoded = new();
    private readonly Lock gate = new();
    private readonly SemaphoreSlim wake = new(0);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Thread thread;
    private volatile bool stopping;

    // what PlangOS said last
    private string codec = "";
    private int width, height;
    private double time, rate = 1, at;
    private bool paused = true, shown, seek = true;
    private (int x, int y, int w, int h)? box;

    // the decoder's side (its thread only)
    private Transform? decoder, scaler;
    private (int w, int h, int fw, int fh) scaled;
    private int next;
    private double last = double.NegativeInfinity, pruned;
    private bool broken, hidden, flip;
    private readonly uint[][] pictures = [[], []];

    /// <param name="show">A picture for the layer: track, the element's box, the picture's place in it, its BGRA.</param>
    /// <param name="hide">The layer goes.</param>
    /// <param name="failed">This host can't show it (no decoder): why.</param>
    /// <param name="delay">How long (s) PlangOS's messages take to get here: the page's time in one was
    /// that long ago.</param>
    internal Player(int track, Action<int, (int, int, int, int), (int, int, int, int), uint[]?> show, Action<int> hide, Action<string> failed, Func<double> delay)
    {
        this.track = track;
        this.show = show;
        this.hide = hide;
        this.failed = failed;
        this.delay = delay;
        thread = new Thread(Run) { IsBackground = true, Name = "screen media " + track };
        thread.Start();
    }

    /// <summary>A message about it: frames (with <paramref name="bytes"/>: kept as slices of the
    /// message, not copied), its state, a remove.</summary>
    internal void Heard(JsonObject head, ReadOnlyMemory<byte> bytes)
    {
        switch (head["op"]?.GetValue<string>())
        {
            case "frames":
                lock (gate)
                {
                    codec = head["codec"]?.GetValue<string>() ?? codec;
                    width = head["width"]?.GetValue<int>() ?? width;
                    height = head["height"]?.GetValue<int>() ?? height;
                    var at = 0;
                    foreach (var f in head["frames"]!.AsArray())
                    {
                        var n = f![3]!.GetValue<int>();
                        frames.Add(new Coded(f[0]!.GetValue<double>(), f[1]!.GetValue<double>(), f[2]!.GetValue<bool>(), bytes.Slice(at, n)));
                        at += n;
                    }
                }
                break;
            case "state":
                lock (gate)
                {
                    var now = Now - delay();   // when the page said it
                    var t = head["time"]?.GetValue<double>() ?? time;
                    var seeking = head["seeking"]?.GetValue<bool>() ?? false;
                    // a jump from where the clock was going: a seek
                    if (seeking || Math.Abs(t - Clock(now)) > 0.3) seek = true;
                    time = t; at = now;
                    paused = head["paused"]?.GetValue<bool>() ?? paused;
                    rate = head["rate"]?.GetValue<double>() ?? rate;
                    shown = head["shown"]?.GetValue<bool>() ?? shown;
                    box = head["rect"] is JsonArray r ? (r[0]!.GetValue<int>(), r[1]!.GetValue<int>(), r[2]!.GetValue<int>(), r[3]!.GetValue<int>()) : null;
                }
                break;
            case "remove":
                lock (gate)
                {
                    double start = head["start"]!.GetValue<double>(), end = head["end"]!.GetValue<double>();
                    var removed = frames.RemoveAll(f => f.Time >= start && f.Time < end);
                    if (removed > 0) seek = true;   // what it was decoding from may be gone
                }
                break;
        }
        wake.Release();
    }

    private double Now => clock.Elapsed.TotalSeconds;

    /// <summary>The page's time at <paramref name="now"/>: where it was, and on at its rate while it plays.</summary>
    private double Clock(double now) => paused ? time : time + (now - at) * rate;

    [DllImport("winmm.dll")] private static extern int timeBeginPeriod(int ms);
    [DllImport("winmm.dll")] private static extern int timeEndPeriod(int ms);

    private void Run()
    {
        // Windows wakes a waiting thread every 15.6 ms unless asked for finer: a frame every 16.7 ms
        // needs 1 ms (per process since Windows 10 2004), while a video plays
        var fine = OperatingSystem.IsWindows() && timeBeginPeriod(1) == 0;
        var wait = 8.0;
        while (!stopping)
        {
            wake.Wait(TimeSpan.FromMilliseconds(Math.Clamp(wait, 1, 8)));
            if (stopping || broken) continue;
            try { wait = Step(); }
            catch (Exception ex) when (ex is InvalidOperationException or COMException or ArgumentException)
            {
                broken = true;
                hide(track);   // the page's own box shows again, not a black one
                failed(ex.Message);
            }
        }
        if (fine) timeEndPeriod(1);
        Drop(decoded.Count);
        scaler?.Dispose();
        decoder?.Dispose();
    }

    /// <summary>One turn: decode what is due soon, show what is due now. How long (ms) until the next
    /// frame is due.</summary>
    private double Step()
    {
        double now, t;
        (int x, int y, int w, int h)? place;
        bool seeking;
        int count;
        lock (gate)
        {
            now = Now;
            t = Clock(now);
            place = shown ? box : null;
            seeking = seek;
            seek = false;
            count = frames.Count;
        }
        if (place is not { } element || element.w <= 0 || element.h <= 0 || count == 0)
        {
            if (place == null && !hidden) { hide(track); hidden = true; }
            return 8;
        }
        hidden = false;
        Prune(t);
        if (decoder == null && !Open()) return 8;
        if (seeking || t - last > Late && decoded.Count == 0 && last > double.NegativeInfinity)
        {
            decoder!.Flush();
            Drop(decoded.Count);
            next = KeyBefore(t);
            last = double.NegativeInfinity;
        }
        // decode ahead: until a frame after now is ready, or enough are
        while (decoded.Count < Ahead && (decoded.Count == 0 || decoded[^1].time <= t))
        {
            Coded frame;
            lock (gate)
            {
                if (next >= frames.Count) break;
                frame = frames[next++];
            }
            var sample = Transform.Sample(frame.Bytes, frame.Time, frame.Duration);
            try
            {
                while (!decoder!.Feed(sample)) Out();
            }
            finally { Transform.Let(sample); }
            Out();
        }
        // what is due: the newest decoded frame not after now; those before it are too late
        var due = -1;
        for (var i = 0; i < decoded.Count; i++) if (decoded[i].time <= t + 0.004) due = i;
        if (due >= 0)
        {
            var (time, picture) = decoded[due];
            decoded.RemoveAt(due);
            Drop(due);
            try { if (time != last) Show(picture, element); }
            finally { decoder!.Recycle(picture); }
            last = time;
        }
        // until the next is due (by the page's clock and rate)
        lock (gate)
            return decoded.Count > 0 && !paused && rate > 0 ? (decoded[0].time - Clock(Now)) * 1000 / rate : 8;
    }

    /// <summary>What the decoder gives now, kept in time order.</summary>
    private void Out()
    {
        for (var s = decoder!.Take(); s != IntPtr.Zero; s = decoder.Take())
        {
            var t = Transform.Time(s);
            var at = decoded.FindIndex(d => d.time > t);
            decoded.Insert(at < 0 ? decoded.Count : at, (t, s));
        }
    }

    /// <summary>The first <paramref name="n"/> decoded frames let go of.</summary>
    private void Drop(int n)
    {
        for (var i = 0; i < n; i++) decoder!.Recycle(decoded[i].sample);
        decoded.RemoveRange(0, n);
    }

    /// <summary>Where decoding starts for time <paramref name="t"/>: the last key frame (in decoding
    /// order) at or before it.</summary>
    private int KeyBefore(double t)
    {
        lock (gate)
        {
            var key = 0;
            for (var i = 0; i < frames.Count; i++)
                if (frames[i].Key && frames[i].Time <= t + 0.001) key = i;
            // nothing kept from before this time: the first key frame
            if (key == 0) key = Math.Max(0, frames.FindIndex(f => f.Key));
            return key;
        }
    }

    /// <summary>What is long past isn't kept (an hour of video would be gigabytes): once a second,
    /// frames before the first key frame of the last 30 s, and not ahead of where decoding is, go.</summary>
    private void Prune(double t)
    {
        if (Now - pruned < 1) return;
        pruned = Now;
        lock (gate)
        {
            var old = frames.FindIndex(f => f.Key && f.Time > t - 30);
            var cut = Math.Min(old, next);
            if (cut <= 0) return;
            frames.RemoveRange(0, cut);
            next -= cut;
        }
    }

    private bool Open()
    {
        string c; int w, h;
        lock (gate) { c = codec; w = width; h = height; }
        decoder = Transform.Decoder(c == "vp9" ? Transform.Vp9 : Transform.H264, w, h);
        if (decoder != null) return true;
        broken = true;
        failed($"Windows has no {c.ToUpperInvariant()} decoder");
        return false;
    }

    /// <summary>A decoded picture into the element's box: its shape kept (contain), scaled by Windows'
    /// video processor, or — when there is none — here, nearest pixel.</summary>
    private void Show(IntPtr picture, (int x, int y, int w, int h) element)
    {
        int w, h;
        lock (gate) { w = width; h = height; }
        if (w <= 0 || h <= 0) return;
        var s = Math.Min(element.w / (double)w, element.h / (double)h);
        int fw = Math.Max(2, (int)Math.Round(w * s)) & ~1, fh = Math.Max(2, (int)Math.Round(h * s)) & ~1;
        var inner = (element.x + (element.w - fw) / 2, element.y + (element.h - fh) / 2, fw, fh);
        var type = decoder!.OutputType();
        try
        {
            var (aw, ah) = Transform.Size(type);
            // made again when what it takes (the decoder's frame) or gives (the box) changes
            if (scaler == null || scaled != (aw, ah, fw, fh))
            {
                scaler?.Dispose();
                scaler = Transform.Scaler(type, w, h, fw, fh);
                scaled = (aw, ah, fw, fh);
            }
            // two pictures, taking turns: the window keeps the last one shown (it merges it again when the
            // page changes under it) while the next is made
            flip = !flip;
            if (pictures[flip ? 1 : 0].Length != fw * fh) pictures[flip ? 1 : 0] = new uint[fw * fh];
            var bgra = pictures[flip ? 1 : 0];
            if (scaler != null && scaler.Feed(picture) && scaler.Take() is var output && output != IntPtr.Zero)
            {
                // Marshal.Copy has no uint[]: the runtime lets a uint[] be seen as the int[] it is bit for bit
                try { Transform.Read(output, (data, length) => Marshal.Copy(data, (int[])(object)bgra, 0, Math.Min(bgra.Length, length / 4))); }
                finally { scaler.Recycle(output); }
            }
            else Transform.Read(picture, (data, length) => Nearest(data, length, Transform.Stride(type, aw), ah, w, h, bgra, fw, fh));
            show(track, element, inner, bgra);
        }
        finally { Transform.Let(type); }
    }

    /// <summary>NV12 to BGRA, nearest pixel (when Windows has no video processor).</summary>
    private static void Nearest(IntPtr data, int length, int stride, int frameHeight, int w, int h, uint[] bgra, int fw, int fh)
    {
        if (length < stride * frameHeight * 3 / 2) return;
        var nv12 = new byte[stride * frameHeight * 3 / 2];
        Marshal.Copy(data, nv12, 0, nv12.Length);
        var uv = stride * frameHeight;
        for (var y = 0; y < fh; y++)
        {
            var sy = y * h / fh;
            for (var x = 0; x < fw; x++)
            {
                var sx = x * w / fw;
                int c = 298 * (nv12[sy * stride + sx] - 16), u = nv12[uv + sy / 2 * stride + (sx & ~1)] - 128, v = nv12[uv + sy / 2 * stride + (sx & ~1) + 1] - 128;
                bgra[y * fw + x] = (uint)Math.Clamp((c + 541 * u + 128) >> 8, 0, 255) | (uint)Math.Clamp((c - 55 * u - 136 * v + 128) >> 8, 0, 255) << 8
                    | (uint)Math.Clamp((c + 459 * v + 128) >> 8, 0, 255) << 16 | 0xFF000000u;
            }
        }
    }

    public void Dispose()
    {
        stopping = true;
        wake.Release();
        thread.Join(1000);
        hide(track);
    }
}
