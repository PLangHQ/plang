using App.Goals.Goal;
using App.Variables;
using App.modules.http.providers;
using App.modules.signing;

namespace App.modules.http;

/// <summary>
/// Downloads a file from a URL and saves it to the local file system.
/// Reports progress via an optional callback goal. Returns the saved file path.
/// </summary>
[Action("download", Cacheable = false)]
public partial class download : IContext
{
    /// <summary>URL to download from. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial Data.@this<string> Url { get; init; }

    /// <summary>Local file path to save the downloaded content to.</summary>
    public partial Data.@this<string> SaveTo { get; init; }

    /// <summary>Behavior when the target file already exists. Default: Error.</summary>
    [Default(FileExists.Error)]
    public partial Data.@this<FileExists> IfExists { get; init; }

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

    [Provider]
    public partial IHttpProvider Http { get; }

    public async Task<Data.@this> Run() => await Http.DownloadAsync(this);
}
