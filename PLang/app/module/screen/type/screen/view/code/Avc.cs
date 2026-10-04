namespace app.module.screen.type.screen.view.code;

/// <summary>
/// H.264 from MP4 (avc1) decoded with openh264: MP4 keeps each NAL unit behind its length and the parameter sets in
/// the avcC record; openh264 reads start codes. So the parameter sets go first, and each sample's lengths become
/// start codes.
/// </summary>
internal sealed class Avc : IDecoder
{
    private readonly Video video = new(0);
    private readonly int lengthSize;
    private byte[]? parameters;   // SPS and PPS as Annex B, given before the first sample

    internal Avc(byte[] avcC)
    {
        // avcC: version, profile, compat, level, 0b111111xx (length size - 1), 0b111xxxxx (SPS count), then each
        // SPS [u16 length][bytes], then [u8 PPS count] and each PPS likewise
        if (avcC.Length < 7) throw new InvalidOperationException("no avcC");
        lengthSize = (avcC[4] & 3) + 1;
        using var annexB = new MemoryStream();
        var at = 5;
        for (var set = 0; set < 2 && at < avcC.Length; set++)
        {
            var count = set == 0 ? avcC[at++] & 0x1F : avcC[at++];
            for (var i = 0; i < count && at + 2 <= avcC.Length; i++)
            {
                var n = avcC[at] << 8 | avcC[at + 1];
                at += 2;
                if (at + n > avcC.Length) break;
                annexB.Write([0, 0, 0, 1]);
                annexB.Write(avcC, at, n);
                at += n;
            }
        }
        parameters = annexB.ToArray();
    }

    public Yuv? Decode(ReadOnlyMemory<byte> sample)
    {
        using var annexB = new MemoryStream(sample.Length + 64);
        if (parameters != null) { annexB.Write(parameters); parameters = null; }
        var s = sample.Span;
        for (var at = 0; at + lengthSize <= s.Length;)
        {
            var n = 0;
            for (var i = 0; i < lengthSize; i++) n = n << 8 | s[at + i];
            at += lengthSize;
            if (n <= 0 || at + n > s.Length) break;
            annexB.Write([0, 0, 0, 1]);
            annexB.Write(s.Slice(at, n));
            at += n;
        }
        return video.Decode(annexB.ToArray());
    }

    public void Dispose() => video.Dispose();
}
