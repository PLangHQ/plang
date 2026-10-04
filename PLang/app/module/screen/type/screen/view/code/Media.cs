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
        private readonly PriorityQueue<double, double> fed = new();   // show times of what the decoder has, in show order
        private readonly List<(double time, Yuv picture)> decoded = new();
        private IDecoder? decoder;
        private int decoderGeneration = -1, next;
        private double fedUpTo = double.MinValue;

        private double clock;                 // the page's time when it said it
        private bool playing, shown;
        private float rate = 1;
        private long clockedAt;
        private Rect place;
        private (byte r, byte g, byte b) key;

        private byte[]? layer;                // the picture at its place's size, BGRA
        private double layerTime = double.NaN;

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
            if (at < next) next++;
        }

        internal void Clock(double time, bool isPlaying, float speed, Rect at, bool isShown, (byte, byte, byte) colour)
            => (clock, playing, rate, place, shown, key, clockedAt) = (time, isPlaying, speed, at, isShown, colour, Stopwatch.GetTimestamp());

        private double Now => clock + (playing ? Stopwatch.GetElapsedTime(clockedAt).TotalSeconds * rate : 0);

        /// <summary>Decodes up to the page's clock; true when a new picture is ready at its place.</summary>
        internal bool Present()
        {
            if (clockedAt == 0 || samples.Count == 0) return false;
            var now = Now;
            // a seek: back in time, or far beyond what was decoded — again from the key frame before it
            if (decoded.Count > 0 && now < decoded[0].time - 0.25 || fedUpTo != double.MinValue && now > fedUpTo + 2) Restart(now);
            if (decoder == null) Restart(now);
            // decode until a picture is known to be past now (or nothing is left)
            while (next < samples.Count && (decoded.Count == 0 || decoded[^1].time <= now) && samples[next].Decode <= now + 1)
                Feed(samples[next++]);
            // the newest picture not after now; older ones go
            var pick = -1;
            for (var i = 0; i < decoded.Count; i++) if (decoded[i].time <= now + 0.001) pick = i;
            if (pick < 0) return false;
            var (time, picture) = decoded[pick];
            decoded.RemoveRange(0, pick);
            Prune(now);
            if (time == layerTime || !shown || place.Width <= 0 || place.Height <= 0) return false;
            layer ??= new byte[place.Width * place.Height * 4];
            if (layer.Length != place.Width * place.Height * 4) layer = new byte[place.Width * place.Height * 4];
            picture.Into(layer, place.Width, place.Height);
            layerTime = time;
            return true;
        }

        private void Feed(Sample s)
        {
            if (s.Generation != decoderGeneration)
            {
                decoder?.Dispose();
                decoder = Make(generations[s.Generation]);
                decoderGeneration = s.Generation;
                fed.Clear();
            }
            fed.Enqueue(s.Time, s.Time);
            fedUpTo = Math.Max(fedUpTo, s.Time);
            Yuv? picture;
            try { picture = decoder?.Decode(s.Bytes); }
            catch (InvalidOperationException) { picture = null; }
            // a picture comes out in show order: it is the earliest show time fed and not yet given
            if (picture != null && fed.TryDequeue(out var time, out _)) decoded.Add((time, picture));
        }

        private void Restart(double now)
        {
            var from = 0;
            for (var i = 0; i < samples.Count; i++)
                if (samples[i].Key && samples[i].Time <= now + 0.001) from = i;
            next = from;
            decoder?.Dispose();
            decoder = null;
            decoderGeneration = -1;
            decoded.Clear();
            fed.Clear();
            fedUpTo = double.MinValue;
            if (next < samples.Count) Feed(samples[next++]);
        }

        // what is well behind the clock goes: from the key frame before (now − 3 s) on stays (a small seek back)
        private void Prune(double now)
        {
            var keep = 0;
            for (var i = 0; i < next; i++) if (samples[i].Key && samples[i].Time <= now - 3) keep = i;
            if (keep <= 0) return;
            samples.RemoveRange(0, keep);
            next -= keep;
        }

        private static IDecoder? Make((string codec, byte[] config) g) => g.codec switch
        {
            "av01" when Av1.Here => new Av1(),
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
