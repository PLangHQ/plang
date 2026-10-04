using System.Buffers.Binary;

namespace app.module.browser.type.video.code;

/// <summary>
/// Fragmented MP4 as a page's player feeds it to Media Source ("video/mp4", any site): an init segment (ftyp, moov)
/// says the video track's codec, its configuration and its size; each media segment (moof, mdat) holds samples —
/// one coded picture each, with its time, its duration and whether a decoder can start at it (a key frame). Only what
/// a decoder needs is read; the samples are slices of the segment, not copies.
/// </summary>
internal sealed class Mp4
{
    /// <summary>The track's codec, as its sample entry names it: <c>av01</c>, <c>avc1</c> (or <c>avc3</c>).</summary>
    internal string Codec { get; private set; } = "";

    /// <summary>The codec's configuration record (av1C, avcC) — what the decoder is started with.</summary>
    internal byte[] Config { get; private set; } = [];

    internal int Width { get; private set; }
    internal int Height { get; private set; }

    /// <summary>Ticks a second of the track's times.</summary>
    internal uint Timescale { get; private set; } = 1;

    private uint _track;
    private uint _defaultDuration, _defaultSize, _defaultFlags;

    /// <summary>A coded picture: when it shows (seconds), how long, a key frame or not, and its bytes.</summary>
    internal readonly record struct Sample(double Time, double Duration, bool Key, ReadOnlyMemory<byte> Bytes);

    /// <summary>The init segment read; false when it holds no video track this reads (AV1 or H.264).</summary>
    internal bool Init(byte[] init)
    {
        foreach (var moov in Inside(init, 0, init.Length, "moov"))
        {
            foreach (var trak in Inside(init, moov.start, moov.end, "trak")) Trak(init, trak.start, trak.end);
            foreach (var mvex in Inside(init, moov.start, moov.end, "mvex"))
                foreach (var trex in Inside(init, mvex.start, mvex.end, "trex"))
                {
                    var b = init.AsSpan(trex.start, trex.end - trex.start);
                    if (b.Length < 24 || BinaryPrimitives.ReadUInt32BigEndian(b[4..]) != _track) continue;
                    _defaultDuration = BinaryPrimitives.ReadUInt32BigEndian(b[12..]);
                    _defaultSize = BinaryPrimitives.ReadUInt32BigEndian(b[16..]);
                    _defaultFlags = BinaryPrimitives.ReadUInt32BigEndian(b[20..]);
                }
        }
        return Codec is "av01" or "avc1" or "avc3";
    }

    // a track: its id (tkhd), its timescale (mdhd) and, when it is video this reads, its sample entry (stsd)
    private void Trak(byte[] d, int start, int end)
    {
        uint id = 0;
        foreach (var tkhd in Inside(d, start, end, "tkhd"))
            id = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(tkhd.start + (d[tkhd.start] == 1 ? 20 : 12)));
        foreach (var mdia in Inside(d, start, end, "mdia"))
        {
            uint timescale = 1;
            foreach (var mdhd in Inside(d, mdia.start, mdia.end, "mdhd"))
                timescale = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(mdhd.start + (d[mdhd.start] == 1 ? 20 : 12)));
            foreach (var minf in Inside(d, mdia.start, mdia.end, "minf"))
            foreach (var stbl in Inside(d, minf.start, minf.end, "stbl"))
            foreach (var stsd in Inside(d, stbl.start, stbl.end, "stsd"))
            {
                // stsd: version/flags, count, then the sample entry box
                var at = stsd.start + 8;
                if (at + 86 > stsd.end) continue;
                var (codec, entry, entryEnd) = Box(d, at);
                if (codec is not ("av01" or "avc1" or "avc3") || entryEnd > stsd.end || entryEnd - at < 86) continue;
                // a visual sample entry: 6 reserved, 2 index, 16 pre-defined, then width and height; its own boxes
                // (av1C, avcC) follow 78 bytes into it
                Width = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(entry + 24));
                Height = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(entry + 26));
                foreach (var name in new[] { "av1C", "avcC" })
                    foreach (var c in Inside(d, entry + 78, entryEnd, name)) Config = d[c.start..c.end];
                Codec = codec;
                Timescale = timescale;
                _track = id;
            }
        }
    }

    // the boxes named <paramref name="type"/> directly inside d[start..end]: where each one's body starts and ends
    private static List<(int start, int end)> Inside(byte[] d, int start, int end, string type)
    {
        var found = new List<(int, int)>();
        for (var at = start; at + 8 <= end;)
        {
            var (t, s, e) = Box(d, at);
            if (e > end || e <= at) break;
            if (t == type) found.Add((s, e));
            at = e;
        }
        return found;
    }

    /// <summary>A media segment's samples of the video track, in decode order.</summary>
    internal List<Sample> Samples(ReadOnlyMemory<byte> segment)
    {
        var samples = new List<Sample>();
        var span = segment.Span;
        var at = 0;
        while (at + 8 <= span.Length)
        {
            var (type, start, end) = Box(span, at);
            if (end > span.Length || end <= at) break;
            if (type == "moof") Moof(segment, at, start, end, samples);
            at = end;
        }
        return samples;
    }

    private void Moof(ReadOnlyMemory<byte> segment, int moof, int start, int end, List<Sample> samples)
    {
        var span = segment.Span;
        for (var at = start; at + 8 <= end;)
        {
            var (type, s, e) = Box(span, at);
            if (type == "traf") Traf(segment, moof, s, e, samples);
            at = e;
        }
    }

    private void Traf(ReadOnlyMemory<byte> segment, int moof, int start, int end, List<Sample> samples)
    {
        var span = segment.Span;
        uint track = 0, duration = _defaultDuration, size = _defaultSize, flags = _defaultFlags;
        long baseOffset = moof;   // default-base-is-moof (the only base fMP4 for MSE uses)
        ulong decode = 0;
        for (var at = start; at + 8 <= end;)
        {
            var (type, s, e) = Box(span, at);
            var b = span[s..e];
            switch (type)
            {
                case "tfhd":
                {
                    var tf = BinaryPrimitives.ReadUInt32BigEndian(b) & 0xFFFFFF;
                    track = BinaryPrimitives.ReadUInt32BigEndian(b[4..]);
                    var o = 8;
                    if ((tf & 0x01) != 0) { baseOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(b[o..]); o += 8; }
                    if ((tf & 0x02) != 0) o += 4;   // sample description index
                    if ((tf & 0x08) != 0) { duration = BinaryPrimitives.ReadUInt32BigEndian(b[o..]); o += 4; }
                    if ((tf & 0x10) != 0) { size = BinaryPrimitives.ReadUInt32BigEndian(b[o..]); o += 4; }
                    if ((tf & 0x20) != 0) flags = BinaryPrimitives.ReadUInt32BigEndian(b[o..]);
                    break;
                }
                case "tfdt":
                    decode = b[0] == 1 ? BinaryPrimitives.ReadUInt64BigEndian(b[4..]) : BinaryPrimitives.ReadUInt32BigEndian(b[4..]);
                    break;
                case "trun" when track == _track:
                {
                    var rf = BinaryPrimitives.ReadUInt32BigEndian(b) & 0xFFFFFF;
                    var signed = b[0] == 1;
                    var count = BinaryPrimitives.ReadUInt32BigEndian(b[4..]);
                    var o = 8;
                    long data = baseOffset;
                    if ((rf & 0x001) != 0) { data += BinaryPrimitives.ReadInt32BigEndian(b[o..]); o += 4; }
                    uint? first = null;
                    if ((rf & 0x004) != 0) { first = BinaryPrimitives.ReadUInt32BigEndian(b[o..]); o += 4; }
                    for (var i = 0; i < count; i++)
                    {
                        uint d = duration, z = size, f = i == 0 && first is { } ff ? ff : flags;
                        long shift = 0;
                        if ((rf & 0x100) != 0) { d = BinaryPrimitives.ReadUInt32BigEndian(b[o..]); o += 4; }
                        if ((rf & 0x200) != 0) { z = BinaryPrimitives.ReadUInt32BigEndian(b[o..]); o += 4; }
                        if ((rf & 0x400) != 0) { f = BinaryPrimitives.ReadUInt32BigEndian(b[o..]); o += 4; }
                        if ((rf & 0x800) != 0)
                        {
                            shift = signed ? BinaryPrimitives.ReadInt32BigEndian(b[o..]) : BinaryPrimitives.ReadUInt32BigEndian(b[o..]);
                            o += 4;
                        }
                        if (data < 0 || data + z > segment.Length) return;
                        // is_non_sync_sample (bit 16) unset: a decoder can start here
                        var key = (f & 0x10000) == 0;
                        samples.Add(new Sample(((long)decode + shift) / (double)Timescale, d / (double)Timescale, key,
                            segment.Slice((int)data, (int)z)));
                        data += z;
                        decode += d;
                    }
                    break;
                }
            }
            at = e;
        }
    }

    // a box at: its type, where its body starts and where it ends (32-bit size, or 64-bit when size is 1)
    private static (string type, int start, int end) Box(ReadOnlySpan<byte> span, int at)
    {
        long size = BinaryPrimitives.ReadUInt32BigEndian(span[at..]);
        var type = System.Text.Encoding.ASCII.GetString(span.Slice(at + 4, 4));
        var start = at + 8;
        if (size == 1 && at + 16 <= span.Length) { size = (long)BinaryPrimitives.ReadUInt64BigEndian(span[(at + 8)..]); start += 8; }
        else if (size == 0) size = span.Length - at;
        return (type, start, (int)Math.Min(at + size, int.MaxValue));
    }
}
