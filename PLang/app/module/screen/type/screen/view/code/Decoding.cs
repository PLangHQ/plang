using app.module.screen.type.screen.code;

namespace app.module.screen.type.screen.view.code;

/// <summary>The Linux host's decoding: AV1 through dav1d, H.264 through openh264 — each one only when its library is
/// on the library path (the PlangOS image has both).</summary>
internal sealed class Decoding : IDecoding
{
    public string[] Codecs => [.. new[] { Av1.Here ? "av01" : null, Video.Here ? "avc1" : null }.OfType<string>()];

    // dav1d and openh264 read the size from the stream itself
    // one that won't start throws why, which Media says
    public IDecoder? Make(string codec, byte[] config, int width, int height) => codec switch
    {
        "av01" when Av1.Here => new Av1(config),
        "avc1" or "avc3" when Video.Here => new Avc(config),
        _ => null,
    };
}
