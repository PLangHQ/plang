using app.Attributes;
using app.Goals.Goal;
using app.Variables;
using app.modules.http.code;
using app.modules.signing;

namespace app.modules.http;

/// <summary>
/// Downloads bytes from a URL. Returns the raw bytes in Data — chain with file.save
/// to persist to disk. One-concern-per-action (OBP): download fetches, save writes.
/// Reports progress via an optional callback goal.
/// </summary>
[System.ComponentModel.Description("Download bytes from a URL and return them in Data; chain with file.save to write to disk")]
[Action("download", Cacheable = false)]
[RequiresCapability("network")]
public partial class download : IContext
{
    /// <summary>URL to download from. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial Data.@this<string> Url { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial Data.@this<Dictionary<string, object>>? Headers { get; init; }

    /// <summary>Download timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial Data.@this<int> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial Data.@this<bool> Unsigned { get; init; }

    /// <summary>Custom signing options for the download request.</summary>
    public partial Data.@this<sign>? SignOptions { get; init; }

    /// <summary>Goal to call with TransferProgress updates during download.</summary>
    [GoalCallback("progress")]
    public partial Data.@this<GoalCall>? OnProgress { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<Data.@this> Run() => await Http.DownloadAsync(this);
}
