using app.Attributes;
using app.goal;
using app.module.http.code;
using app.module.signing;

namespace app.module.http;

/// <summary>
/// Downloads from a URL. Download fetches; given a <see cref="Path"/> it writes the body there as it arrives, never
/// held whole, and answers the path — else it answers the bytes. Given a <see cref="Hash"/> the body is hashed as it
/// arrives and must match it. Reports progress via an optional callback goal.
/// </summary>
[Action("download", Cacheable = false)]
[RequiresCapability("network")]
public partial class download : IContext, IAddressed
{
    /// <summary>URL to download from. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? Header { get; init; }

    /// <summary>How long the download may take. Default: 30s.</summary>
    [Default("30s")]
    public partial data.@this<global::app.type.item.duration.@this> Timeout { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Unsigned { get; init; }

    /// <summary>Goal called with <c>%progress%</c> (received, total, percent) as the download goes, and once more when it
    /// is done — the last one's Data carries how it ended (a hash mismatch, a cap passed).</summary>
    [GoalCallback("progress")]
    public partial data.@this<global::app.goal.step.action.@this>? OnProgress { get; init; }

    /// <summary>Base URL a relative URL joins — an absolute http(s) url. Unset = URLs must be absolute.</summary>
    public partial data.@this<global::app.type.item.path.http.@this>? BaseUrl { get; init; }

    /// <summary>Header merged into every request; per-request <see cref="Header"/> win on conflict.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? DefaultHeaders { get; init; }

    /// <summary>How redirects are followed — whether, and how many at most. Left out, up to ten are followed.</summary>
    public partial data.@this<global::app.module.http.type.redirect.@this> Redirect { get; init; }

    /// <summary>Where the body is written as it arrives — the answer is then this path, and the body is never held
    /// whole. The write is gated as one; a body that fails its <see cref="Hash"/> leaves nothing here.</summary>
    public partial data.@this<global::app.type.item.path.@this>? Path { get; init; }

    /// <summary>The digest the body must have — <c>sha256:&lt;hex&gt;</c>, its algorithm the value's. The body is hashed as
    /// it arrives; a mismatch refuses the download, naming both hashes.</summary>
    public partial data.@this<global::app.module.crypto.type.hash.@this>? Hash { get; init; }

    /// <summary>The most a download may be, with or without a Path — a size (<c>500 MB</c>, <c>2 GiB</c>). Default 100 MiB.</summary>
    [Default("100 MiB")]
    public partial data.@this<global::app.type.item.size.@this> MaxDownloadSize { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<data.@this> Start() => await Http.DownloadAsync(this);
}
