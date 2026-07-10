using app.Attributes;
using app.goal;
using app.variable;
using app.module.http.code;
using app.module.signing;

namespace app.module.http;

/// <summary>
/// Sends an HTTP request and returns the parsed response.
/// Supports JSON, XML, text, binary, and application/plang responses.
/// When signing is enabled (default), attaches X-Signature header and verifies signed responses.
/// </summary>
[Action("request")]
[RequiresCapability("network")]
public partial class request : IContext
{
    /// <summary>Target URL. Relative URLs resolve against Config.BaseUrl. Bare domains get https:// prefix.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>HTTP method. Default: GET.</summary>
    [Default(HttpMethod.GET)]
    public partial data.@this<global::app.type.item.choice.@this<HttpMethod>> Method { get; init; }

    /// <summary>Request body. Strings sent as-is; objects JSON-serialized. Null for bodyless methods (GET, HEAD).</summary>
    public partial data.@this? Body { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders (step-level wins on conflict).</summary>
    public partial data.@this<global::app.type.dict.@this>? Headers { get; init; }

    /// <summary>Content-Type for the request body. Default: "application/json".</summary>
    [Default("application/json")]
    public partial data.@this<global::app.type.item.text.@this> ContentType { get; init; }

    /// <summary>Character encoding for the request body. Default: "utf-8".</summary>
    [Default("utf-8")]
    public partial data.@this<global::app.type.item.text.@this> Encoding { get; init; }

    /// <summary>Request timeout in seconds. Default: 30. Overrides Config.TimeoutInSec.</summary>
    [Default(30)]
    public partial data.@this<global::app.type.number.@this> TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false (requests are signed).</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Unsigned { get; init; }

    /// <summary>Custom signing options. Overrides default signing behavior (contracts, headers, expiry).</summary>
    public partial data.@this<sign>? SignOptions { get; init; }

    /// <summary>Goal to call for each streamed chunk.</summary>
    [GoalCallback("chunk")]
    public partial data.@this<GoalCall>? OnStream { get; init; }

    /// <summary>Stream format: Line (NDJSON), SSE (Server-Sent Events), or Bytes (raw chunks).</summary>
    public partial data.@this<global::app.type.item.choice.@this<StreamFormat>>? StreamAs { get; init; }

    /// <summary>Base URL for resolving relative request URLs. Unset = URLs must be absolute.</summary>
    public partial data.@this<global::app.type.item.text.@this>? BaseUrl { get; init; }

    /// <summary>Headers merged into every request; per-request <see cref="Headers"/> win on conflict.</summary>
    public partial data.@this<global::app.type.dict.@this>? DefaultHeaders { get; init; }

    /// <summary>Whether to follow HTTP redirects. Default: true.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> FollowRedirects { get; init; }

    /// <summary>Maximum redirects to follow. Default: 10.</summary>
    [Default(10)]
    public partial data.@this<global::app.type.number.@this> MaxRedirects { get; init; }

    /// <summary>Max response body size in bytes. Default 100MB — guards against OOM from untrusted servers.</summary>
    [Default(100 * 1024 * 1024)]
    public partial data.@this<global::app.type.number.@this> MaxResponseSize { get; init; }

    /// <summary>Max SSE message buffer in bytes. Default 10MB.</summary>
    [Default(10 * 1024 * 1024)]
    public partial data.@this<global::app.type.number.@this> MaxSSEBufferSize { get; init; }

    [Code]
    public partial IHttp Http { get; }

    // Returns plain Data — the response body is the lazy value (type/kind from
    // Content-Type); status/headers/duration ride as Properties (read with `!`).
    // The parallel http.response record dissolved (Decision 6).
    public async Task<data.@this> Run() => await Http.SendAsync(this);

    /// <summary>
    /// Compile-time hint: if Url is a literal with a recognized extension
    /// (e.g. "https://api/x.json"), surface that type so the trailing
    /// variable.set can stamp Response.Body's expected shape. Variable
    /// references and unknown extensions defer to runtime Content-Type dispatch.
    /// </summary>
    public Task<data.@this> Build() => HttpBuildHelpers.InferTypeFromUrl(__action, Context.App, "Url");
}
