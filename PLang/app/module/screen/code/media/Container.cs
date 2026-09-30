using System.Buffers;
using System.Buffers.Binary;

namespace app.module.screen.code.media;

/// <summary>One coded video frame out of a container: when it shows and for how long (seconds, the
/// source's time), whether a decoder can start at it, and its bytes as the host's decoder takes them
/// (VP9: as they are; H.264: Annex B, SPS and PPS before each key frame) — a slice of the bytes it came
/// in, not a copy.</summary>
internal readonly record struct Coded(double Time, double Duration, bool Key, ReadOnlyMemory<byte> Bytes);

/// <summary>
/// A video buffer's container, read as its bytes arrive in pieces of any size (a page appends what it
/// fetched): the frames it holds come out as each one is whole. WebM (VP9) and fragmented MP4 (H.264)
/// — what pages feed MediaSource. Frames are slices of what was appended (or, for H.264, of one buffer
/// an append's frames are written into): only an unfinished piece is kept, copied, for the next append.
/// </summary>
internal abstract class Container
{
    private byte[] held = [];

    /// <summary>"vp9" or "h264".</summary>
    internal string Codec { get; private protected set; } = "";
    internal int Width { get; private protected set; }
    internal int Height { get; private protected set; }

    /// <summary>Added to every frame's time (MediaSource's timestampOffset).</summary>
    internal double Offset { get; set; }

    /// <summary>A container for <paramref name="mime"/> (<c>video/webm; codecs="vp9"</c>,
    /// <c>video/mp4; codecs="avc1…"</c>), or null.</summary>
    internal static Container? For(string mime)
    {
        var m = mime.ToLowerInvariant();
        if (m.StartsWith("video/webm") && (m.Contains("vp9") || m.Contains("vp09"))) return new WebM();
        if (m.StartsWith("video/mp4") && m.Contains("avc1")) return new Mp4();
        return null;
    }

    /// <summary>The next bytes; the frames that are whole now (slices of <paramref name="bytes"/>:
    /// valid while it is).</summary>
    internal List<Coded> Take(ReadOnlyMemory<byte> bytes)
    {
        var all = bytes;
        if (held.Length > 0)
        {
            var joined = new byte[held.Length + bytes.Length];
            held.CopyTo(joined, 0);
            bytes.Span.CopyTo(joined.AsSpan(held.Length));
            all = joined;
        }
        var frames = new List<Coded>();
        var used = Read(all, frames);
        held = used < all.Length ? all[used..].ToArray() : [];
        return frames;
    }

    /// <summary>What was held of an unfinished piece is dropped (MediaSource's abort).</summary>
    internal virtual void Reset() => held = [];

    /// <summary>Reads whole pieces from <paramref name="bytes"/> into <paramref name="frames"/>;
    /// returns how many bytes it used (the rest waits for more).</summary>
    private protected abstract int Read(ReadOnlyMemory<byte> bytes, List<Coded> frames);
}

/// <summary>
/// WebM (Matroska): EBML elements. Masters that hold what matters (Segment, Tracks, TrackEntry, Video,
/// Cluster, BlockGroup, Info) are entered — their size may be unknown, as a stream's often is —
/// everything else is skipped; a block's time is its cluster's plus its own, in TimecodeScale units.
/// </summary>
internal sealed class WebM : Container
{
    private const uint Segment = 0x18538067, Info = 0x1549A966, TimecodeScale = 0x2AD7B1, Tracks = 0x1654AE6B,
        TrackEntry = 0xAE, TrackNumber = 0xD7, CodecId = 0x86, VideoElement = 0xE0, PixelWidth = 0xB0, PixelHeight = 0xBA,
        DefaultDuration = 0x23E383, Cluster = 0x1F43B675, Timecode = 0xE7, SimpleBlock = 0xA3, BlockGroup = 0xA0,
        Block = 0xA1, BlockDuration = 0x9B;

    private static readonly HashSet<uint> Entered = [Segment, Info, Tracks, TrackEntry, VideoElement, Cluster, BlockGroup];
    private static readonly HashSet<uint> Values = [TimecodeScale, TrackNumber, CodecId, PixelWidth, PixelHeight,
        DefaultDuration, Timecode, SimpleBlock, Block, BlockDuration];

    private double scale = 1e-3;   // TimecodeScale in seconds: 1 ms by default
    private double frameDuration = 1 / 30.0;
    private long cluster;
    private long skip;             // bytes still to drop of an element being skipped
    private (double time, bool key, ReadOnlyMemory<byte> bytes)? pending;   // a block waits for the next to know its duration
    private double? blockDuration;

    internal WebM() => Codec = "vp9";

    internal override void Reset()
    {
        base.Reset();
        skip = 0;
    }

    private protected override int Read(ReadOnlyMemory<byte> memory, List<Coded> frames)
    {
        var bytes = memory.Span;
        var at = 0;
        if (skip > 0)
        {
            var dropped = (int)Math.Min(skip, bytes.Length);
            skip -= dropped;
            at = dropped;
        }
        while (at < bytes.Length)
        {
            if (!Vint(bytes, at, out var id, out var idLength, keepMarker: true)) break;
            if (!Vint(bytes, at + idLength, out var size, out var sizeLength, keepMarker: false)) break;
            var unknown = size == (1L << (7 * sizeLength)) - 1;
            var body = at + idLength + sizeLength;
            if (Entered.Contains((uint)id)) { at = body; continue; }   // its children follow
            if (unknown) { at = body; continue; }   // an unknown-size element we don't enter: read on
            if (!Values.Contains((uint)id))
            {
                // skipped: what is here now, and the rest as it comes
                var here = bytes.Length - body;
                if (size > here) { skip = size - here; return bytes.Length; }
                at = body + (int)size;
                continue;
            }
            if (body + size > bytes.Length) break;   // not whole yet
            Element((uint)id, memory.Slice(body, (int)size), frames);
            at = body + (int)size;
        }
        // the last block isn't held for the next append: it goes, its duration the usual one
        Flush(double.NaN, frames);
        return at;
    }

    private void Element(uint id, ReadOnlyMemory<byte> value, List<Coded> frames)
    {
        var v = value.Span;
        switch (id)
        {
            case TimecodeScale: scale = Unsigned(v) * 1e-9; break;
            case PixelWidth: Width = (int)Unsigned(v); break;
            case PixelHeight: Height = (int)Unsigned(v); break;
            case DefaultDuration: frameDuration = Unsigned(v) * 1e-9; break;
            case Timecode: cluster = (long)Unsigned(v); break;
            case BlockDuration: blockDuration = Unsigned(v) * scale; break;
            case SimpleBlock or Block:
            {
                if (!Vint(v, 0, out _, out var n, keepMarker: false) || v.Length < n + 3) return;
                var relative = BinaryPrimitives.ReadInt16BigEndian(v[n..]);
                var flags = v[n + 2];
                var time = (cluster + relative) * scale + Offset;
                // a Block (in a BlockGroup) counts as a key frame; SimpleBlock says so in its flags
                var key = id != SimpleBlock || (flags & 0x80) != 0;
                Flush(time, frames);
                pending = (time, key, value[(n + 3)..]);
                break;
            }
        }
    }

    /// <summary>The block before gets its duration: up to this one (<paramref name="next"/>), or the
    /// usual one when none follows yet.</summary>
    private void Flush(double next, List<Coded> frames)
    {
        if (pending is not { } p) return;
        var duration = blockDuration ?? (next > p.time ? next - p.time : frameDuration);
        if (duration > 0 && duration < 1) frameDuration = duration;
        frames.Add(new Coded(p.time, duration, p.key, p.bytes));
        pending = null;
        blockDuration = null;
    }

    /// <summary>An EBML variable-length number at <paramref name="at"/>: an element's id (its length
    /// marker kept) or size (taken off).</summary>
    private static bool Vint(ReadOnlySpan<byte> b, int at, out long value, out int length, bool keepMarker)
    {
        value = 0; length = 0;
        if (at >= b.Length || b[at] == 0) return false;
        length = System.Numerics.BitOperations.LeadingZeroCount((uint)b[at]) - 23;
        if (at + length > b.Length) return false;
        value = keepMarker ? b[at] : b[at] & (0xFF >> length);
        for (var i = 1; i < length; i++) value = (value << 8) | b[at + i];
        return true;
    }

    private static ulong Unsigned(ReadOnlySpan<byte> v)
    {
        ulong x = 0;
        foreach (var b in v) x = (x << 8) | b;
        return x;
    }
}

/// <summary>
/// Fragmented MP4: the init (moov: the video track's timescale, its avcC — how long NAL lengths are,
/// the SPS and PPS — and trex defaults), then fragments (moof + mdat): each sample's time from tfdt and
/// the durations before it, its composition offset, whether it is a sync sample. H.264 samples become
/// Annex B (start codes where the lengths were; SPS and PPS before each key frame), written for all of
/// an append into one buffer.
/// </summary>
internal sealed class Mp4 : Container
{
    private static readonly byte[] StartCode = [0, 0, 0, 1];

    private uint timescale = 90000, track;
    private int lengthSize = 4;
    private byte[] parameterSets = [];
    private uint trexDuration, trexSize, trexFlags;

    internal Mp4() => Codec = "h264";

    private protected override int Read(ReadOnlyMemory<byte> memory, List<Coded> frames)
    {
        var bytes = memory.Span;
        var written = new ArrayBufferWriter<byte>();
        var found = new List<(double time, double duration, bool key, int at, int length)>();
        var at = 0;
        int moof = -1, moofSize = 0;
        while (at + 8 <= bytes.Length)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);
            var type = BinaryPrimitives.ReadUInt32BigEndian(bytes[(at + 4)..]);
            var header = 8;
            if (size == 1)
            {
                if (at + 16 > bytes.Length) break;
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes[(at + 8)..]);
                header = 16;
            }
            else if (size == 0) size = bytes.Length - at;
            if (size < header || at + size > bytes.Length) break;   // not whole yet
            switch (type)
            {
                case 0x6D6F6F76: Moov(bytes.Slice(at + header, (int)size - header)); break;   // moov
                case 0x6D6F6F66: moof = at; moofSize = (int)size; break;                      // moof: its samples are in the mdat after it
                case 0x6D646174:                                                               // mdat
                    if (moof >= 0) Fragment(bytes.Slice(moof, moofSize), bytes.Slice(moof, at + (int)size - moof), written, found);
                    moof = -1;
                    break;
            }
            at += (int)size;
        }
        // the frames: slices of the one buffer written (it doesn't move any more)
        var all = written.WrittenMemory;
        foreach (var f in found) frames.Add(new Coded(f.time, f.duration, f.key, all.Slice(f.at, f.length)));
        return moof >= 0 ? moof : at;   // a moof without its mdat yet waits, with it
    }

    private void Moov(ReadOnlySpan<byte> moov)
    {
        foreach (var (type, start, length) in Boxes(moov))
        {
            var box = moov.Slice(start, length);
            if (type == "trak") Video(box);
            if (type == "mvex")
                foreach (var (t, s, n) in Boxes(box))
                {
                    var trex = box.Slice(s, n);
                    if (t != "trex" || BinaryPrimitives.ReadUInt32BigEndian(trex[4..]) != track) continue;
                    trexDuration = BinaryPrimitives.ReadUInt32BigEndian(trex[12..]);
                    trexSize = BinaryPrimitives.ReadUInt32BigEndian(trex[16..]);
                    trexFlags = BinaryPrimitives.ReadUInt32BigEndian(trex[20..]);
                }
        }
    }

    /// <summary>A trak: when it is the video, its id, timescale, size and avcC.</summary>
    private void Video(ReadOnlySpan<byte> trak)
    {
        uint id = 0;
        var video = false;
        foreach (var (type, start, length) in Boxes(trak))
        {
            var box = trak.Slice(start, length);
            if (type == "tkhd") id = BinaryPrimitives.ReadUInt32BigEndian(box[(box[0] == 1 ? 20 : 12)..]);
            if (type != "mdia") continue;
            foreach (var (t, s, n) in Boxes(box))
            {
                var b = box.Slice(s, n);
                if (t == "mdhd") timescale = BinaryPrimitives.ReadUInt32BigEndian(b[(b[0] == 1 ? 20 : 12)..]);
                if (t == "hdlr" && b.Length >= 12 && b.Slice(8, 4).SequenceEqual("vide"u8)) video = true;
                if (t != "minf") continue;
                foreach (var (t2, s2, n2) in Boxes(b))
                {
                    if (t2 != "stbl") continue;
                    var stbl = b.Slice(s2, n2);
                    foreach (var (t3, s3, n3) in Boxes(stbl))
                        if (t3 == "stsd") Entry(stbl.Slice(s3, n3)[8..]);
                }
            }
        }
        if (video) track = id;
    }

    /// <summary>The sample entry (avc1): its size, and its avcC.</summary>
    private void Entry(ReadOnlySpan<byte> entries)
    {
        foreach (var (type, start, length) in Boxes(entries))
        {
            if (type is not ("avc1" or "avc3")) continue;
            var entry = entries.Slice(start, length);
            Width = BinaryPrimitives.ReadUInt16BigEndian(entry[24..]);
            Height = BinaryPrimitives.ReadUInt16BigEndian(entry[26..]);
            var children = entry[78..];
            foreach (var (t, s, n) in Boxes(children))
            {
                if (t != "avcC") continue;
                var avcC = children.Slice(s, n);
                lengthSize = (avcC[4] & 3) + 1;
                var sets = new ArrayBufferWriter<byte>();
                var at = 5;
                for (var kind = 0; kind < 2; kind++)
                {
                    var count = kind == 0 ? avcC[at++] & 31 : avcC[at++];
                    for (var i = 0; i < count; i++)
                    {
                        var size = BinaryPrimitives.ReadUInt16BigEndian(avcC[at..]);
                        sets.Write(StartCode);
                        sets.Write(avcC.Slice(at + 2, size));
                        at += 2 + size;
                    }
                }
                parameterSets = sets.WrittenSpan.ToArray();
            }
        }
    }

    /// <summary>A moof and its mdat (<paramref name="whole"/> starts at the moof): its samples, written.</summary>
    private void Fragment(ReadOnlySpan<byte> moof, ReadOnlySpan<byte> whole, ArrayBufferWriter<byte> written, List<(double, double, bool, int, int)> found)
    {
        var body = moof[8..];
        foreach (var (type, start, length) in Boxes(body))
        {
            if (type != "traf") continue;
            var traf = body.Slice(start, length);
            uint duration = trexDuration, size = trexSize, flags = trexFlags;
            ulong baseTime = 0;
            var ours = false;
            foreach (var (t, s, n) in Boxes(traf))
            {
                var b = traf.Slice(s, n);
                if (t == "tfhd")
                {
                    var f = BinaryPrimitives.ReadUInt32BigEndian(b) & 0xFFFFFF;
                    ours = BinaryPrimitives.ReadUInt32BigEndian(b[4..]) == track;
                    var at = 8;
                    if ((f & 0x01) != 0) at += 8;   // base data offset: the moof is the base here (default-base-is-moof)
                    if ((f & 0x02) != 0) at += 4;
                    if ((f & 0x08) != 0) { duration = BinaryPrimitives.ReadUInt32BigEndian(b[at..]); at += 4; }
                    if ((f & 0x10) != 0) { size = BinaryPrimitives.ReadUInt32BigEndian(b[at..]); at += 4; }
                    if ((f & 0x20) != 0) flags = BinaryPrimitives.ReadUInt32BigEndian(b[at..]);
                }
                if (t == "tfdt") baseTime = b[0] == 1 ? BinaryPrimitives.ReadUInt64BigEndian(b[4..]) : BinaryPrimitives.ReadUInt32BigEndian(b[4..]);
                if (t == "trun" && ours) baseTime = Run(b, whole, baseTime, duration, size, flags, written, found);
            }
        }
    }

    /// <summary>A trun's samples, written; the decode time after them.</summary>
    private ulong Run(ReadOnlySpan<byte> trun, ReadOnlySpan<byte> whole, ulong time, uint duration, uint size, uint flags,
        ArrayBufferWriter<byte> written, List<(double, double, bool, int, int)> found)
    {
        var version = trun[0];
        var f = BinaryPrimitives.ReadUInt32BigEndian(trun) & 0xFFFFFF;
        var count = BinaryPrimitives.ReadUInt32BigEndian(trun[4..]);
        var at = 8;
        var data = 0;
        if ((f & 0x01) != 0) { data = BinaryPrimitives.ReadInt32BigEndian(trun[at..]); at += 4; }
        uint? first = null;
        if ((f & 0x04) != 0) { first = BinaryPrimitives.ReadUInt32BigEndian(trun[at..]); at += 4; }
        for (var i = 0; i < count; i++)
        {
            uint d = duration, s = size, fl = i == 0 && first is { } ff ? ff : flags;
            long offset = 0;
            if ((f & 0x100) != 0) { d = BinaryPrimitives.ReadUInt32BigEndian(trun[at..]); at += 4; }
            if ((f & 0x200) != 0) { s = BinaryPrimitives.ReadUInt32BigEndian(trun[at..]); at += 4; }
            if ((f & 0x400) != 0) { fl = BinaryPrimitives.ReadUInt32BigEndian(trun[at..]); at += 4; }
            if ((f & 0x800) != 0) { offset = version == 0 ? BinaryPrimitives.ReadUInt32BigEndian(trun[at..]) : BinaryPrimitives.ReadInt32BigEndian(trun[at..]); at += 4; }
            if (data + s > whole.Length) break;
            var key = (fl & 0x10000) == 0;   // not sample_is_non_sync_sample
            var begin = written.WrittenCount;
            AnnexB(whole.Slice(data, (int)s), key, written);
            found.Add((((long)time + offset) / (double)timescale + Offset, d / (double)timescale, key, begin, written.WrittenCount - begin));
            data += (int)s;
            time += d;
        }
        return time;
    }

    /// <summary>Length-prefixed NAL units written as Annex B; the parameter sets first on a key frame.</summary>
    private void AnnexB(ReadOnlySpan<byte> sample, bool key, ArrayBufferWriter<byte> written)
    {
        if (key) written.Write(parameterSets);
        var at = 0;
        while (at + lengthSize <= sample.Length)
        {
            var n = 0;
            for (var i = 0; i < lengthSize; i++) n = (n << 8) | sample[at + i];
            at += lengthSize;
            if (n <= 0 || at + n > sample.Length) break;
            written.Write(StartCode);
            written.Write(sample.Slice(at, n));
            at += n;
        }
    }

    /// <summary>The boxes in <paramref name="bytes"/>: type, and where its body is.</summary>
    private static List<(string type, int start, int length)> Boxes(ReadOnlySpan<byte> bytes)
    {
        var boxes = new List<(string, int, int)>();
        var at = 0;
        while (at + 8 <= bytes.Length)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);
            var header = 8;
            if (size == 1 && at + 16 <= bytes.Length) { size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes[(at + 8)..]); header = 16; }
            else if (size == 0) size = bytes.Length - at;
            if (size < header || at + size > bytes.Length) break;
            boxes.Add((System.Text.Encoding.ASCII.GetString(bytes.Slice(at + 4, 4)), at + header, (int)size - header));
            at += (int)size;
        }
        return boxes;
    }
}
