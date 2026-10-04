using System.Buffers.Binary;
using System.Diagnostics;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace app.module.screen.type.screen.view.code;

/// <summary>
/// The videos a page plays that this host plays itself (pass-through; see the browser's video type for the messages
/// 10–13): each stream's samples kept in decode order, decoded just up to the page's clock, its picture drawn at its
/// place on the screen wherever the page shows the key colour — under the player's controls and captions, which the
/// page draws over it. A seek starts again from the key frame before the new time; a new start of the same stream (a
/// quality change) a new decoder from its samples on. Sixty times a second.
/// </summary>
internal sealed class Media : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<uint, Stream> streams = new();
    private readonly Thread presenter;
    private volatile bool stopping;

    /// <summary>The codecs this host decodes (what it tells PlangOS).</summary>
    internal static string[] Codecs => [.. new[] { Av1.Here ? "av01" : null, Video.Here ? "avc1" : null }.OfType<string>()];

    /// <summary>Pictures shown so far (each new one at a stream's place).</summary>
    internal int Shown;

    /// <summary>What the streams have done so far: samples, pictures decoded, restarts (seeks), ticks with nothing new
    /// to show and why.</summary>
    internal string Numbers
    {
        get
        {
            lock (gate)
                return $"shown {Shown}; " + string.Join("; ", streams.Select(s => $"stream {s.Key}: {s.Value.Numbers}"));
        }
    }

    internal Media()
    {
        presenter = new Thread(Present) { IsBackground = true, Name = "screen view: video" };
        presenter.Start();
    }

    /// <summary>A media message from PlangOS (10 start, 11 sample, 12 clock, 13 end); false when it isn't one.</summary>
    internal bool Take(byte[] m)
    {
        if (m.Length < 5 || m[0] is < 10 or > 13) return false;
        var id = BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(1));
        lock (gate)
        {
            switch (m[0])
            {
                case 10 when m.Length >= 13:
                    if (!streams.TryGetValue(id, out var s)) streams[id] = s = new Stream();
                    s.Start(System.Text.Encoding.ASCII.GetString(m, 5, 4).Trim(), m[13..]);
                    break;
                case 11 when m.Length >= 30 && streams.TryGetValue(id, out var sampled):
                    sampled.Add(BinaryPrimitives.ReadDoubleLittleEndian(m.AsSpan(5)), BinaryPrimitives.ReadDoubleLittleEndian(m.AsSpan(13)),
                        m[29] == 1, m.AsMemory(30));
                    break;
                case 12 when m.Length >= 38 && streams.TryGetValue(id, out var clocked):
                    clocked.Clock(BinaryPrimitives.ReadDoubleLittleEndian(m.AsSpan(5)), m[13] == 1,
                        BinaryPrimitives.ReadSingleLittleEndian(m.AsSpan(14)),
                        new Rect(BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(18)), BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(22)),
                            BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(26)), BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(30))),
                        m[34] == 1, (m[35], m[36], m[37]));
                    break;
                case 13 when streams.Remove(id, out var ended):
                    ended.Dispose();
                    break;
            }
        }
        return true;
    }

    /// <summary>The videos drawn over <paramref name="screen"/> (BGRA, <paramref name="width"/> × <paramref name="height"/>):
    /// at each one's place, where the page shows its key colour.</summary>
    internal void Draw(byte[] screen, int width, int height)
    {
        lock (gate)
            foreach (var s in streams.Values) s.Draw(screen, width, height);
    }

    private void Present()
    {
        var tick = Stopwatch.StartNew();
        while (!stopping)
        {
            lock (gate)
                foreach (var s in streams.Values)
                    if (s.Present()) Shown++;
            var wait = 16 - (int)(tick.ElapsedMilliseconds % 16);
            Thread.Sleep(wait);
        }
    }

    public void Dispose()
    {
        stopping = true;
        presenter.Join();
        lock (gate)
        {
            foreach (var s in streams.Values) s.Dispose();
            streams.Clear();
        }
    }

    /// <summary>One video: its samples, its decoder, its clock and its place.</summary>
    private sealed class Stream : IDisposable
    {
        private readonly record struct Sample(double Time, double Decode, bool Key, byte[] Bytes, int Generation);

        private readonly List<Sample> samples = new();          // in decode order
        private readonly List<(string codec, byte[] config)> generations = new();
        private readonly List<(double time, Yuv picture)> decoded = new();
        private IDecoder? decoder;
        private int decoderGeneration = -1, clocks;
        private double lastFed = double.NegativeInfinity;   // the decode time of the last sample the decoder has

        // the next sample to decode: the first after the last one fed (found each time: samples come and go)
        private int Next
        {
            get
            {
                int lo = 0, hi = samples.Count;
                while (lo < hi) { var mid = (lo + hi) / 2; if (samples[mid].Decode <= lastFed) lo = mid + 1; else hi = mid; }
                return lo;
            }
        }
        private double fedUpTo = double.MinValue;

        private double clock;                 // the page's time when it said it
        private bool playing, shown;
        private float rate = 1;
        private long clockedAt;
        private Rect place;
        private (byte r, byte g, byte b) key;

        private byte[]? layer;                // the picture at its place's size, BGRA
        private double layerTime = double.NaN;
        private int added, decodedCount, restarts, noPicture, same, hidden;

        internal string Numbers => $"{added} samples ({samples.Count} kept), {decodedCount} decoded, {restarts} restarts, " +
            $"ticks: {noPicture} no picture yet, {same} same picture, {hidden} not shown; clock {clock:F2} playing {playing} at {place}; " +
            $"now {Now:F3}, {clocks} clocks (last {Stopwatch.GetElapsedTime(clockedAt).TotalSeconds:F1} s ago), decoded up to {(decoded.Count > 0 ? decoded[^1].time : double.NaN):F3}, next sample shows {(Next < samples.Count ? samples[Next].Time : double.NaN):F3} " +
            $"decodes {(Next < samples.Count ? samples[Next].Decode : double.NaN):F3}, first kept {(samples.Count > 0 ? samples[0].Time : double.NaN):F3} (decodes {(samples.Count > 0 ? samples[0].Decode : double.NaN):F3}), last {(samples.Count > 0 ? samples[^1].Time : double.NaN):F3} (decodes {(samples.Count > 0 ? samples[^1].Decode : double.NaN):F3}); last fed decodes {lastFed:F3}; {generations.Count} starts; " +
            $"times of the last 5 shown: {string.Join(" ", lastShown)}";
        private readonly Queue<string> lastShown = new();

        internal void Start(string codec, byte[] config) => generations.Add((codec, config));

        internal void Add(double time, double decode, bool key, ReadOnlyMemory<byte> bytes)
        {
            if (generations.Count == 0) return;
            var sample = new Sample(time, decode, key, bytes.ToArray(), generations.Count - 1);
            // mostly appended in order; a segment again (after a seek) replaces what was there
            var at = samples.Count;
            while (at > 0 && samples[at - 1].Decode > decode) at--;
            if (at > 0 && Math.Abs(samples[at - 1].Decode - decode) < 1e-6) { samples[at - 1] = sample; return; }
            samples.Insert(at, sample);
            added++;
        }

        internal void Clock(double time, bool isPlaying, float speed, Rect at, bool isShown, (byte, byte, byte) colour)
        {
            (clock, playing, rate, place, shown, key, clockedAt) = (time, isPlaying, speed, at, isShown, colour, Stopwatch.GetTimestamp());
            clocks++;
        }

        private double Now => clock + (playing ? Stopwatch.GetElapsedTime(clockedAt).TotalSeconds * rate : 0);

        /// <summary>Decodes up to the page's clock; true when a new picture is ready at its place.</summary>
        internal bool Present()
        {
            if (clockedAt == 0 || samples.Count == 0) return false;
            var now = Now;
            // a seek: back in time, or ahead past a key frame beyond what was decoded (decoding up to it would be
            // slower than starting there) — again from the key frame before now
            if (decoded.Count > 0 && now < decoded[0].time - 0.25 || now > fedUpTo + 1 && KeyBetween(fedUpTo, now)) Restart(now);
            if (decoder == null) Restart(now);
            // decode until a picture is known to be past now (or nothing is left)
            for (var n = Next; n < samples.Count && (decoded.Count == 0 || decoded[^1].time <= now) && samples[n].Decode <= now + 1
                               && Follows(samples[n]); n = Next)
                Feed(samples[n]);
            // the newest picture not after now; older ones go
            var pick = -1;
            for (var i = 0; i < decoded.Count; i++) if (decoded[i].time <= now + 0.001) pick = i;
            if (pick < 0) { noPicture++; return false; }
            var (time, picture) = decoded[pick];
            decoded.RemoveRange(0, pick);
            Prune(now);
            if (time == layerTime) { same++; return false; }
            if (!shown || place.Width <= 0 || place.Height <= 0) { hidden++; return false; }
            layer ??= new byte[place.Width * place.Height * 4];
            if (layer.Length != place.Width * place.Height * 4) layer = new byte[place.Width * place.Height * 4];
            picture.Into(layer, place.Width, place.Height);
            layerTime = time;
            lastShown.Enqueue($"{time:F3}@{now:F3}");
            if (lastShown.Count > 5) lastShown.Dequeue();
            return true;
        }

        // a sample the decoder may take next: one right after the last it took — or a key frame, where a decoder can
        // always start. Never across a gap (a segment still arriving while a later one is here): a picture whose
        // references the decoder never saw breaks the decoding from there on; the gap's samples come, or the clock
        // runs past it to a key frame (a jump)
        private bool Follows(Sample s)
        {
            if (s.Key) return true;
            if (double.IsNegativeInfinity(lastFed)) return false;   // a fresh decoder starts at a key frame
            return s.Decode - lastFed <= 0.1;
        }

        private bool KeyBetween(double after, double upTo)
        {
            for (var i = Next; i < samples.Count; i++)
                if (samples[i].Key && samples[i].Time > after && samples[i].Time <= upTo) return true;
            return false;
        }

        private void Feed(Sample s)
        {
            if (s.Generation != decoderGeneration)
            {
                decoder?.Dispose();
                decoder = Make(generations[s.Generation]);
                decoderGeneration = s.Generation;
            }
            fedUpTo = Math.Max(fedUpTo, s.Time);
            lastFed = s.Decode;
            List<(double time, Yuv picture)> pictures;
            try { pictures = decoder?.Decode(s.Bytes, s.Time) ?? []; }
            catch (InvalidOperationException) { pictures = []; }
            // each picture with its own sample's time (the decoder carries it), kept in show order
            foreach (var p in pictures)
            {
                var at = decoded.Count;
                while (at > 0 && decoded[at - 1].time > p.time) at--;
                decoded.Insert(at, p);
                decodedCount++;
            }
        }

        private void Restart(double now)
        {
            var from = 0;
            for (var i = 0; i < samples.Count; i++)
                if (samples[i].Key && samples[i].Time <= now + 0.001) from = i;
            decoder?.Dispose();
            decoder = null;
            decoderGeneration = -1;
            restarts++;
            decoded.Clear();
            fedUpTo = double.MinValue;
            lastFed = double.NegativeInfinity;
            if (from < samples.Count) Feed(samples[from]);
        }

        // what is well behind the clock goes: from the key frame before (now − 3 s) on stays (a small seek back)
        private void Prune(double now)
        {
            var keep = 0;
            var next = Next;
            for (var i = 0; i < next; i++) if (samples[i].Key && samples[i].Time <= now - 3) keep = i;
            if (keep > 0) samples.RemoveRange(0, keep);
        }

        private static IDecoder? Make((string codec, byte[] config) g) => g.codec switch
        {
            "av01" when Av1.Here => new Av1(g.config),
            "avc1" or "avc3" when Video.Here => new Avc(g.config),
            _ => null,
        };

        /// <summary>Its picture over <paramref name="screen"/> at its place, where the page shows the key colour.</summary>
        internal void Draw(byte[] screen, int width, int height)
        {
            if (layer == null || !shown || layer.Length != place.Width * place.Height * 4) return;
            int x0 = Math.Max(0, place.X), y0 = Math.Max(0, place.Y);
            int x1 = Math.Min(width, place.Right), y1 = Math.Min(height, place.Bottom);
            for (var y = y0; y < y1; y++)
            {
                var row = y * width * 4;
                var from = ((y - place.Y) * place.Width - place.X) * 4;
                for (var x = x0; x < x1; x++)
                {
                    var o = row + x * 4;
                    if (screen[o] != key.b || screen[o + 1] != key.g || screen[o + 2] != key.r) continue;
                    var l = from + x * 4;
                    screen[o] = layer[l]; screen[o + 1] = layer[l + 1]; screen[o + 2] = layer[l + 2]; screen[o + 3] = 255;
                }
            }
        }

        public void Dispose()
        {
            decoder?.Dispose();
            decoder = null;
        }
    }
}
