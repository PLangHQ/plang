using app.module.screen.type.screen.code;

namespace app.module.screen.type.screen.window.code;

/// <summary>The Windows host's decoding: Windows' own decoders (Media Foundation, on the CPU) — H.264, and AV1 where
/// the AV1 Video Extension is installed. Which ones are here is asked of Windows once.</summary>
internal sealed class Decoding : IDecoding
{
    // AV1 only: Windows' own H.264 decoder took ~2.5 cores for one 1080p50 stream on Ingi's machine (ruv.is) — more than
    // the page's own player in PlangOS and the pictures sent as before. H.264 stays there; AV1 (YouTube) comes here.
    private readonly Lazy<string[]> codecs = new(() => OperatingSystem.IsWindows() ? [.. new[] { "av01" }.Where(Mft.Here)] : []);

    public string[] Codecs => codecs.Value;

    // none, or it won't take the stream: it throws why, which Media says
    public IDecoder? Make(string codec, byte[] config, int width, int height) => new Mft(codec, config, width, height);
}
