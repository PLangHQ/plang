using app.Attributes;
using app.goal;
using app.module.http.code;
using app.module.signing;

namespace app.module.http;

/// <summary>
/// Uploads content to a URL. Supports file upload, base64, form data, and text.
/// Content type is auto-detected from the Content value or explicitly set via As.
/// </summary>
[Action("upload", Cacheable = false)]
[RequiresCapability("network")]
public partial class upload : IContext, IAddressed
{
    /// <summary>URL to upload to. Relative URLs resolve against Config.BaseUrl.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>Content to upload. Type determines format: string path = file, Dictionary = form, object = JSON.</summary>
    public partial data.@this Content { get; init; }

    /// <summary>HTTP method. Default: POST.</summary>
    [Default(HttpMethod.POST)]
    public partial data.@this<global::app.type.item.choice.@this<HttpMethod>> Method { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? Header { get; init; }

    /// <summary>Character encoding. Default: "utf-8".</summary>
    [Default("utf-8")]
    public partial data.@this<global::app.type.item.text.@this> Encoding { get; init; }

    /// <summary>How long the upload may take. Default: 30s.</summary>
    [Default("30s")]
    public partial data.@this<global::app.type.item.duration.@this> Timeout { get; init; }

    /// <summary>When true, an application/plang response is refused (UnsignedPlang). Default: false. The upload is
    /// not signed either way.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Unsigned { get; init; }

    /// <summary>Explicit content format hint. Null = auto-detect from Content type.</summary>
    public partial data.@this<global::app.type.item.choice.@this<ContentAs>>? As { get; init; }

    /// <summary>Goal to call with TransferProgress updates during upload.</summary>
    [GoalCallback("progress")]
    public partial data.@this<global::app.goal.step.action.@this>? OnProgress { get; init; }

    /// <summary>Base URL a relative URL joins — an absolute http(s) url. Unset = URLs must be absolute.</summary>
    public partial data.@this<global::app.type.item.path.http.@this>? BaseUrl { get; init; }

    /// <summary>Header merged into every request; per-request <see cref="Header"/> win on conflict.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? DefaultHeaders { get; init; }

    /// <summary>How redirects are followed — whether, and how many at most. Left out, up to ten are followed.</summary>
    public partial data.@this<global::app.module.http.type.redirect.@this> Redirect { get; init; }

    /// <summary>Max response body size in bytes. Default 100MB.</summary>
    [Default(100 * 1024 * 1024)]
    public partial data.@this<global::app.type.item.number.@this> MaxResponseSize { get; init; }

    [Code]
    public partial IHttp Http { get; }

    // Plain Data — body lazy (from Content-Type), metadata in Properties.
    public async Task<data.@this> Start() => await Http.UploadAsync(this);

    // A literal url says what its body will be by its extension — the path's Kind; none for a %variable% url
    // or an extension no type has.
    public async Task<data.@this> Build() => Url.HasVariable || BaseUrl?.HasVariable == true ? Context.Ok()
        : await (await ((IAddressed)this).Target()).Use(url =>
        {
            var kind = url.Kind(Context);
            return Task.FromResult(kind.IsNull ? Context.Ok() : Context.Ok(kind));
        });
}
