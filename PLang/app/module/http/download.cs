using app.Attributes;
using app.goal;
using app.module.http.code;
using app.module.signing;

namespace app.module.http;

/// <summary>
/// Downloads bytes from a URL. Returns the raw bytes in Data — chain with file.save
/// to persist to disk. One-concern-per-action (OBP): download fetches, save writes.
/// Reports progress via an optional callback goal.
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

    /// <summary>Goal to call with TransferProgress updates during download.</summary>
    [GoalCallback("progress")]
    public partial data.@this<global::app.goal.step.action.@this>? OnProgress { get; init; }

    /// <summary>Base URL a relative URL joins — an absolute http(s) url. Unset = URLs must be absolute.</summary>
    public partial data.@this<global::app.type.item.path.http.@this>? BaseUrl { get; init; }

    /// <summary>Header merged into every request; per-request <see cref="Header"/> win on conflict.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? DefaultHeaders { get; init; }

    /// <summary>How redirects are followed — whether, and how many at most. Left out, up to ten are followed.</summary>
    public partial data.@this<global::app.module.http.type.redirect.@this>? Redirect { get; init; }

    /// <summary>Max download size in bytes. Default 100MB.</summary>
    [Default(100 * 1024 * 1024)]
    public partial data.@this<global::app.type.item.number.@this> MaxDownloadSize { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<data.@this> Start() => await Http.DownloadAsync(this);
}
