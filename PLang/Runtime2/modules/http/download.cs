using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Downloads a file from a URL and saves it to the local file system.
/// Reports progress via an optional callback goal. Returns the saved file path.
/// </summary>
[Action("download", Cacheable = false)]
public partial class download : IContext
{
    /// <summary>URL to download from. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial string Url { get; init; }

    /// <summary>Local file path to save the downloaded content to.</summary>
    public partial string SaveTo { get; init; }

    /// <summary>Behavior when the target file already exists. Default: Error.</summary>
    [Default(FileExists.Error)]
    public partial FileExists IfExists { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Download timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial bool Unsigned { get; init; }

    /// <summary>Custom signing options for the download request.</summary>
    public partial sign? SignOptions { get; init; }

    /// <summary>Goal to call with TransferProgress updates during download.</summary>
    [GoalCallback("progress")]
    public partial GoalCall? OnProgress { get; init; }

    [Provider]
    public partial IHttpProvider Http { get; }

    public async Task<Data> Run() => await Http.DownloadAsync(this);
}
