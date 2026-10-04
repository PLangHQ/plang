using app.module.screen.type.screen.code;

namespace app.module.screen.type.screen.view.code;

/// <summary>
/// H.264 from MP4 (avc1) decoded with openh264, which reads start codes: each sample as Annex B (<see cref="Avcc"/>),
/// the parameter sets before the first.
/// </summary>
internal sealed class Avc : IDecoder
{
    private readonly Video video = new(0);
    private readonly Avcc avcc;
    private byte[]? parameters;   // SPS and PPS as Annex B, given before the first sample

    internal Avc(byte[] avcC)
    {
        avcc = new Avcc(avcC);
        parameters = avcc.Parameters;
    }

    // openh264 gives pictures in the order it decodes them, one per sample (no reordering: constrained baseline);
    // each is its sample's
    public List<(double time, Yuv picture)> Decode(ReadOnlyMemory<byte> sample, double time)
    {
        var annexB = avcc.AnnexB(sample.Span, parameters);
        parameters = null;
        return video.Decode(annexB) is { } picture ? [(time, picture)] : [];
    }

    public void Dispose() => video.Dispose();
}
