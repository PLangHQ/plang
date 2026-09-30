using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace app.module.screen.code.media;

/// <summary>
/// A redirected video, on the host: it keeps the coded frames PlangOS sends (in time order, each
/// append's run where its time puts it), follows the page's clock (the page's audio runs it: its time,
/// and how it goes on between messages), decodes with Windows a few frames ahead, and shows the one due
/// now — scaled into the video element's box as the page would (contain: its shape kept, black bars) —
/// as a layer the window shows where the page has the key colour. A seek starts again at the key frame
/// before; frames already past on the way are decoded (the next needs them), not shown.
/// </summary>
internal sealed class Player : IDisposable
{
    private const int Ahead = 3;          // frames decoded before they are due
    private const double Late = 1.0;      // s behind the clock: start again at a key frame, when that skips work

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
    private (int aw, int ah, int w, int h, int fw, int fh) scaled;
    private bool scalerTried;
    private int next;
    private double last = double.NegativeInfinity, pruned;
    private bool broken, hidden = true, flip, drained;
    private readonly uint[][] pictures = [[], []];
    private byte[] nv12 = [];
    private IntPtr onScreen;                         // the decoded picture shown now: kept to show again
    private (int x, int y, int w, int h) shownBox;   // where it was shown

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
            {
                var run = new List<Coded>();
                var n0 = 0;
                foreach (var f in head["frames"]!.AsArray())
                {
                    var n = f![3]!.GetValue<int>();
                    run.Add(new Coded(f[0]!.GetValue<double>(), f[1]!.GetValue<double>(), f[2]!.GetValue<bool>(), bytes.Slice(n0, n)));
                    n0 += n;
                }
                if (run.Count == 0) break;
                lock (gate)
                {
                    codec = head["codec"]?.GetValue<string>() ?? codec;
                    width = head["width"]?.GetValue<int>() ?? width;
                    height = head["height"]?.GetValue<int>() ?? height;
                    // the run goes where its time puts it (a page may append an earlier part later),
                    // kept in its own (decoding) order
                    var start = run[0].Time;
                    var at = frames.Count;
                    for (var i = 0; i < frames.Count; i++) if (frames[i].Key && frames[i].Time > start) { at = i; break; }
                    frames.InsertRange(at, run);
                    if (at < next) next += run.Count;
                }
                break;
            }
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
                    var removed = false;
                    for (var i = frames.Count - 1; i >= 0; i--)
                    {
                        if (frames[i].Time < start || frames[i].Time >= end) continue;
                        frames.RemoveAt(i);
                        if (i < next) next--;
                        removed = true;
                    }
                    if (removed) seek = true;   // what it was decoding from may be gone
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
            wake.Wait(TimeSpan.FromMilliseconds(double.IsFinite(wait) ? Math.Clamp(wait, 1, 8) : 8));
            if (stopping || broken) continue;
            try { wait = Step(); }
            catch (Exception ex)
            {
                // whatever went wrong, it ends this video, never the host
                broken = true;
                hide(track);   // the page's own box shows again, not a black one
                failed(ex.Message);
            }
        }
        if (fine) timeEndPeriod(1);
        try
        {
            if (onScreen != IntPtr.Zero) decoder?.Recycle(onScreen);
            Drop(decoded.Count);
            scaler?.Dispose();
            decoder?.Dispose();
        }
        catch (Exception) { /* going anyway */ }
    }

    /// <summary>One turn: decode what is due soon, show what is due now (or show again what is shown,
    /// when its box moved or it was hidden). How long (ms) until the next frame is due.</summary>
    private double Step()
    {
        double t;
        (int x, int y, int w, int h)? place;
        bool seeking;
        int count;
        lock (gate)
        {
            t = Clock(Now);
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
        Prune(t);
        if (decoder == null && !Open()) return 8;
        if (seeking || (t - last > Late && decoded.Count == 0 && last > double.NegativeInfinity && KeyBefore(t) > next))
        {
            decoder!.Flush();
            Drop(decoded.Count);
            next = KeyBefore(t);
            last = double.NegativeInfinity;
            drained = false;
        }
        Decode(t);
        // what is due: the newest decoded frame not after now (and after what is shown)
        var due = -1;
        for (var i = 0; i < decoded.Count; i++) if (decoded[i].time <= t + 0.004 && decoded[i].time > last) due = i;
        if (due >= 0)
        {
            var (when, picture) = decoded[due];
            decoded.RemoveAt(due);
            Drop(due);   // those before it are too late
            if (onScreen != IntPtr.Zero) decoder!.Recycle(onScreen);
            onScreen = picture;
            last = when;
            Show(element);
        }
        else if (onScreen != IntPtr.Zero && (hidden || shownBox != element)) Show(element);   // paused, and moved or shown again
        lock (gate)
        {
            if (decoded.Count == 0 || paused || rate <= 0) return 8;
            return (decoded[0].time - Clock(Now)) * 1000 / rate;   // until the next is due
        }
    }

    /// <summary>Decodes until a frame after <paramref name="t"/> is ready, or enough are. Frames already
    /// past (catching up after a seek, or behind) are given back at once: they don't count, and aren't
    /// shown. At the end of what there is, the decoder is drained: its held-back frames come out.</summary>
    private void Decode(double t)
    {
        while (Ahead > Waiting(t) && (decoded.Count == 0 || decoded[^1].time <= t))
        {
            Coded frame;
            lock (gate)
            {
                if (next >= frames.Count)
                {
                    if (!drained && decoded.Count == 0) { decoder!.Drain(); drained = true; Out(t); }
                    return;
                }
                frame = frames[next++];
            }
            drained = false;
            var sample = Transform.Sample(frame.Bytes, frame.Time, frame.Duration);
            try
            {
                while (!decoder!.Feed(sample)) Out(t);
            }
            finally { Transform.Let(sample); }
            Out(t);
        }
    }

    /// <summary>How many decoded frames aren't past yet.</summary>
    private int Waiting(double t)
    {
        var n = 0;
        foreach (var d in decoded) if (d.time > t - 0.05) n++;
        return n;
    }

    /// <summary>What the decoder gives now, kept in time order — but a frame long past (before the
    /// newest one past) or without a time is given back at once.</summary>
    private void Out(double t)
    {
        for (var s = decoder!.Take(); s != IntPtr.Zero; s = decoder.Take())
        {
            var when = Transform.Time(s);
            if (!double.IsFinite(when) || when <= last) { decoder.Recycle(s); continue; }
            var at = decoded.Count;
            for (var i = 0; i < decoded.Count; i++) if (decoded[i].time > when) { at = i; break; }
            decoded.Insert(at, (when, s));
            // of the frames past, only the newest is kept (it may be the one to show)
            while (decoded.Count > 1 && decoded[1].time <= t) { decoder.Recycle(decoded[0].sample); decoded.RemoveAt(0); }
        }
    }

    /// <summary>The first <paramref name="n"/> decoded frames given back.</summary>
    private void Drop(int n)
    {
        for (var i = 0; i < n; i++) decoder!.Recycle(decoded[i].sample);
        decoded.RemoveRange(0, n);
    }

    /// <summary>Where decoding starts for time <paramref name="t"/>: the last key frame (in decoding
    /// order) at or before it; the first one when there is none before.</summary>
    private int KeyBefore(double t)
    {
        lock (gate)
        {
            int key = -1, first = -1;
            for (var i = 0; i < frames.Count; i++)
            {
                if (!frames[i].Key) continue;
                if (first < 0) first = i;
                if (frames[i].Time <= t + 0.001) key = i;
            }
            return key >= 0 ? key : Math.Max(0, first);
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
            var old = -1;
            for (var i = 0; i < frames.Count; i++) if (frames[i].Key && frames[i].Time > t - 30) { old = i; break; }
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
        Guid? kind = c switch { "vp9" => Transform.Vp9, "h264" => Transform.H264, "av1" => Transform.Av1, _ => null };
        decoder = kind is { } k ? Transform.Decoder(k, w, h, lowLatency: false) : null;
        if (decoder != null) return true;
        broken = true;
        hide(track);
        failed($"Windows has no {c.ToUpperInvariant()} decoder");
        return false;
    }

    /// <summary>The picture on screen into the element's box: its shape kept (contain), scaled by
    /// Windows' video processor, or — when there is none — here, nearest pixel.</summary>
    private void Show((int x, int y, int w, int h) element)
    {
        if (stopping || onScreen == IntPtr.Zero) return;
        int w, h;
        lock (gate) { w = width; h = height; }
        if (w <= 0 || h <= 0) return;
        var s = Math.Min(element.w / (double)w, element.h / (double)h);
        int fw = Math.Max(2, (int)Math.Round(w * s)) & ~1, fh = Math.Max(2, (int)Math.Round(h * s)) & ~1;
        var inner = (element.x + (element.w - fw) / 2, element.y + (element.h - fh) / 2, fw, fh);
        var type = decoder!.OutputType();
        var (aw, ah) = Transform.Size(type);
        // made again when what it takes (the decoder's frame, the picture in it) or gives (the box) changes
        if (scaled != (aw, ah, w, h, fw, fh))
        {
            scaler?.Dispose();
            scaler = null;
            scaled = (aw, ah, w, h, fw, fh);
            scalerTried = false;
        }
        if (scaler == null && !scalerTried)
        {
            scalerTried = true;
            scaler = Transform.Scaler(type, w, h, fw, fh);
        }
        // two pictures, taking turns: the window keeps the last one shown (it merges it again when the
        // page changes under it) while the next is made
        flip = !flip;
        ref var bgra = ref pictures[flip ? 1 : 0];
        if (bgra.Length != fw * fh) bgra = new uint[fw * fh];
        var scaledOut = false;
        if (scaler != null && scaler.Feed(onScreen))
        {
            var output = scaler.Take();
            if (output != IntPtr.Zero)
            {
                var buffer = Transform.Locked(output, out var data, out var length);
                if (buffer != IntPtr.Zero)
                {
                    // Marshal.Copy has no uint[]: the runtime lets a uint[] be seen as the int[] it is bit for bit
                    Marshal.Copy(data, (int[])(object)bgra, 0, Math.Min(bgra.Length, length / 4));
                    Transform.Unlocked(buffer);
                    scaledOut = true;
                }
                scaler.Recycle(output);
            }
        }
        if (!scaledOut)
        {
            var buffer = Transform.Locked(onScreen, out var data, out var length);
            if (buffer == IntPtr.Zero) return;
            try { Nearest(data, length, Transform.Stride(type, aw), aw, ah, w, h, bgra, fw, fh); }
            finally { Transform.Unlocked(buffer); }
        }
        if (stopping) return;
        show(track, element, inner, bgra);
        shownBox = element;
        hidden = false;
    }

    /// <summary>NV12 to BGRA, nearest pixel (when Windows has no video processor).</summary>
    private void Nearest(IntPtr data, int length, int stride, int frameWidth, int frameHeight, int w, int h, uint[] bgra, int fw, int fh)
    {
        var size = stride * frameHeight * 3 / 2;
        if (length < size) return;
        if (nv12.Length != size) nv12 = new byte[size];
        Marshal.Copy(data, nv12, 0, size);
        var uv = stride * frameHeight;
        for (var y = 0; y < fh; y++)
        {
            var sy = Math.Min(frameHeight - 1, y * h / fh);
            for (var x = 0; x < fw; x++)
            {
                var sx = Math.Min(frameWidth - 1, x * w / fw);
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
