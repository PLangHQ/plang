using app.Attributes;
using app.goals.goal;
using app.variables;
using app.modules.http.code;
using app.modules.signing;

namespace app.modules.http;

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
    public partial data.@this<string> Url { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial data.@this<Dictionary<string, object>>? Headers { get; init; }

    /// <summary>Download timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial data.@this<int> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial data.@this<bool> Unsigned { get; init; }

    /// <summary>Custom signing options for the download request.</summary>
    public partial data.@this<sign>? SignOptions { get; init; }

    /// <summary>Goal to call with TransferProgress updates during download.</summary>
    [GoalCallback("progress")]
    public partial data.@this<GoalCall>? OnProgress { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<data.@this> Run() => await Http.DownloadAsync(this);
}
