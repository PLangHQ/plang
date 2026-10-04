namespace app.module.screen.type.screen.code;

/// <summary>
/// An H.264 stream's avcC record (from its MP4): MP4 keeps each NAL unit behind its length and the parameter sets
/// here; decoders read start codes (Annex B). So the parameter sets go first, and each sample's lengths become start
/// codes.
/// </summary>
internal sealed class Avcc
{
    private readonly int lengthSize;

    /// <summary>The SPS and PPS as Annex B: given before the first sample.</summary>
    internal byte[] Parameters { get; }

    internal Avcc(byte[] avcC)
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
        Parameters = annexB.ToArray();
    }

    /// <summary>A sample as Annex B, after <paramref name="first"/> (the parameter sets, before the first sample).</summary>
    internal byte[] AnnexB(ReadOnlySpan<byte> sample, byte[]? first = null)
    {
        using var annexB = new MemoryStream(sample.Length + 64 + (first?.Length ?? 0));
        if (first != null) annexB.Write(first);
        for (var at = 0; at + lengthSize <= sample.Length;)
        {
            var n = 0;
            for (var i = 0; i < lengthSize; i++) n = n << 8 | sample[at + i];
            at += lengthSize;
            if (n <= 0 || at + n > sample.Length) break;
            annexB.Write([0, 0, 0, 1]);
            annexB.Write(sample.Slice(at, n));
            at += n;
        }
        return annexB.ToArray();
    }
}
