using app.Attributes;
using app.goal;
using app.variable;
using app.module.action.http.code;
using app.module.action.signing;

namespace app.module.action.http;

/// <summary>
/// Downloads bytes from a URL. Returns the raw bytes in Data — chain with file.save
/// to persist to disk. One-concern-per-action (OBP): download fetches, save writes.
/// Reports progress via an optional callback goal.
/// </summary>
[Action("download", Cacheable = false)]
[RequiresCapability("network")]
public partial class download : IContext
{
    /// <summary>URL to download from. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? Header { get; init; }

    /// <summary>Download timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial data.@this<global::app.type.item.number.@this> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Unsigned { get; init; }

    /// <summary>Custom signing options for the download request.</summary>
    public partial data.@this<sign>? SignOptions { get; init; }

    /// <summary>Goal to call with TransferProgress updates during download.</summary>
    [GoalCallback("progress")]
    public partial data.@this<GoalCall>? OnProgress { get; init; }

    /// <summary>Base URL for resolving relative URLs. Unset = URLs must be absolute.</summary>
    public partial data.@this<global::app.type.item.text.@this>? BaseUrl { get; init; }

    /// <summary>Header merged into every request; per-request <see cref="Header"/> win on conflict.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? DefaultHeaders { get; init; }

    /// <summary>Whether to follow HTTP redirects. Default: true.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> FollowRedirects { get; init; }

    /// <summary>Maximum redirects to follow. Default: 10.</summary>
    [Default(10)]
    public partial data.@this<global::app.type.item.number.@this> MaxRedirects { get; init; }

    /// <summary>Max download size in bytes. Default 100MB.</summary>
    [Default(100 * 1024 * 1024)]
    public partial data.@this<global::app.type.item.number.@this> MaxDownloadSize { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<data.@this> Run() => await Http.DownloadAsync(this);
}
