using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Sends an HTTP request and returns the parsed response.
/// Supports JSON, XML, text, binary, and application/plang responses.
/// When signing is enabled (default), attaches X-Signature header and verifies signed responses.
/// </summary>
[Action("request")]
public partial class request : IContext
{
    /// <summary>Target URL. Relative URLs resolve against Config.BaseUrl. Bare domains get https:// prefix.</summary>
    public partial string Url { get; init; }

    /// <summary>HTTP method. Default: GET.</summary>
    [Default(HttpMethod.GET)]
    public partial HttpMethod Method { get; init; }

    /// <summary>Request body. Strings sent as-is; objects JSON-serialized. Null for bodyless methods (GET, HEAD).</summary>
    public partial object? Body { get; init; }

    /// <summary>Per-request headers. Merged with Config.DefaultHeaders (step-level wins on conflict).</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Content-Type for the request body. Default: "application/json".</summary>
    [Default("application/json")]
    public partial string ContentType { get; init; }

    /// <summary>Character encoding for the request body. Default: "utf-8".</summary>
    [Default("utf-8")]
    public partial string Encoding { get; init; }

    /// <summary>Request timeout in seconds. Default: 30. Overrides Config.TimeoutInSec.</summary>
    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    /// <summary>When true, skips request signing. Default: false (requests are signed).</summary>
    [Default(false)]
    public partial bool Unsigned { get; init; }

    /// <summary>Custom signing options. Overrides default signing behavior (contracts, headers, expiry).</summary>
    public partial sign? SignOptions { get; init; }

    /// <summary>Goal to call for each streamed chunk. Receives chunk data via MemoryStack variable.</summary>
    public partial GoalCall? OnStream { get; init; }

    /// <summary>Stream format: Line (NDJSON), SSE (Server-Sent Events), or Bytes (raw chunks).</summary>
    public partial StreamFormat? StreamAs { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IHttpProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.SendAsync(this);
    }
}
