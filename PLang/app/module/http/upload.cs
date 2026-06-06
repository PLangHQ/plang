using app.Attributes;
using app.goal;
using app.variable;
using app.module.http.code;
using app.module.signing;

namespace app.module.http;

/// <summary>
/// Uploads content to a URL. Supports file upload, base64, form data, and text.
/// Content type is auto-detected from the Content value or explicitly set via As.
/// </summary>
[Action("upload", Cacheable = false)]
[RequiresCapability("network")]
public partial class upload : IContext
{
    /// <summary>URL to upload to. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial data.@this<global::app.type.text.@this> Url { get; init; }

    /// <summary>Content to upload. Type determines format: string path = file, Dictionary = form, object = JSON.</summary>
    public partial data.@this Content { get; init; }

    /// <summary>HTTP method. Default: POST.</summary>
    [Default(HttpMethod.POST)]
    public partial data.@this<HttpMethod> Method { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial data.@this<Dictionary<string, object>>? Headers { get; init; }

    /// <summary>Character encoding. Default: "utf-8".</summary>
    [Default("utf-8")]
    public partial data.@this<global::app.type.text.@this> Encoding { get; init; }

    /// <summary>Upload timeout in seconds. Default: 30.</summary>
    [Default(30)]
    public partial data.@this<global::app.type.number.@this> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Unsigned { get; init; }

    /// <summary>Custom signing options for the upload request.</summary>
    public partial data.@this<sign>? SignOptions { get; init; }

    /// <summary>Explicit content format hint. Null = auto-detect from Content type.</summary>
    public partial data.@this<ContentAs>? As { get; init; }

    /// <summary>Goal to call with TransferProgress updates during upload.</summary>
    [GoalCallback("progress")]
    public partial data.@this<GoalCall>? OnProgress { get; init; }

    [Code]
    public partial IHttp Http { get; }

    // Plain Data — body lazy (from Content-Type), metadata in Properties.
    public async Task<data.@this> Run() => await Http.UploadAsync(this);

    public Task<data.@this> Build() => HttpBuildHelpers.InferTypeFromUrl(__action, __app, "Url");
}
