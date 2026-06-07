using app.Attributes;
using app.goal;
using app.variable;
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
public partial class download : IContext
{
    /// <summary>URL to download from. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial data.@this<global::app.type.text.@this> Url { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial data.@this<global::app.type.dict.@this>? Headers { get; init; }

    /// <summary>Download timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial data.@this<global::app.type.number.@this> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Unsigned { get; init; }

    /// <summary>Custom signing options for the download request.</summary>
    public partial data.@this<sign>? SignOptions { get; init; }

    /// <summary>Goal to call with TransferProgress updates during download.</summary>
    [GoalCallback("progress")]
    public partial data.@this<GoalCall>? OnProgress { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<data.@this> Run() => await Http.DownloadAsync(this);
}
