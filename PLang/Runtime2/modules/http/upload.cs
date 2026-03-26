using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Uploads content to a URL. Supports file upload, base64, form data, and text.
/// Content type is auto-detected from the Content value or explicitly set via As.
/// </summary>
[Action("upload", Cacheable = false)]
public partial class upload : IContext
{
    /// <summary>URL to upload to. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial string Url { get; init; }

    /// <summary>Content to upload. Type determines format: string path = file, Dictionary = form, object = JSON.</summary>
    public partial object Content { get; init; }

    /// <summary>HTTP method. Default: POST.</summary>
    [Default(HttpMethod.POST)]
    public partial HttpMethod Method { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Character encoding. Default: "utf-8".</summary>
    [Default("utf-8")]
    public partial string Encoding { get; init; }

    /// <summary>Upload timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial bool Unsigned { get; init; }

    /// <summary>Custom signing options for the upload request.</summary>
    public partial sign? SignOptions { get; init; }

    /// <summary>Explicit content format hint. Null = auto-detect from Content type.</summary>
    public partial ContentAs? As { get; init; }

    /// <summary>Goal to call with TransferProgress updates during upload.</summary>
    [GoalCallback("progress")]
    public partial GoalCall? OnProgress { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IHttpProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.UploadAsync(this);
    }
}
