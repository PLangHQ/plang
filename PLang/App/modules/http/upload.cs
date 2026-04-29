using App.Attributes;
using App.Goals.Goal;
using App.Variables;
using App.modules.http.providers;
using App.modules.signing;

namespace App.modules.http;

/// <summary>
/// Uploads content to a URL. Supports file upload, base64, form data, and text.
/// Content type is auto-detected from the Content value or explicitly set via As.
/// </summary>
[System.ComponentModel.Description("Upload content (file, form data, JSON, or bytes) to a URL via multipart or body POST")]
[Action("upload", Cacheable = false)]
[RequiresCapability("network")]
public partial class upload : IContext
{
    /// <summary>URL to upload to. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial Data.@this<string> Url { get; init; }

    /// <summary>Content to upload. Type determines format: string path = file, Dictionary = form, object = JSON.</summary>
    public partial Data.@this Content { get; init; }

    /// <summary>HTTP method. Default: POST.</summary>
    [Default(HttpMethod.POST)]
    public partial Data.@this<HttpMethod> Method { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial Data.@this<Dictionary<string, object>>? Headers { get; init; }

    /// <summary>Character encoding. Default: "utf-8".</summary>
    [Default("utf-8")]
    public partial Data.@this<string> Encoding { get; init; }

    /// <summary>Upload timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial Data.@this<int> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial Data.@this<bool> Unsigned { get; init; }

    /// <summary>Custom signing options for the upload request.</summary>
    public partial Data.@this<sign>? SignOptions { get; init; }

    /// <summary>Explicit content format hint. Null = auto-detect from Content type.</summary>
    public partial Data.@this<ContentAs>? As { get; init; }

    /// <summary>Goal to call with TransferProgress updates during upload.</summary>
    [GoalCallback("progress")]
    public partial Data.@this<GoalCall>? OnProgress { get; init; }

    [Provider]
    public partial IHttpProvider Http { get; }

    public async Task<Data.@this> Run() => await Http.UploadAsync(this);
}
