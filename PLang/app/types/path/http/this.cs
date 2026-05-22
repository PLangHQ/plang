using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using app.errors;
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;
using WriteVerb = global::app.types.path.permission.verb.Write;
using DeleteVerb = global::app.types.path.permission.verb.Delete;

namespace app.types.path.http;

/// <summary>
/// <c>http://</c> / <c>https://</c> Path — the second scheme, proving
/// polymorphism with a non-filesystem backend. Verb impls map onto HTTP
/// methods (GET/POST/DELETE/HEAD). Every request is signed with PLang's
/// built-in identity unless the resource is hit unsigned.
/// </summary>
/// <remarks>
/// <para>
/// <b>Error shape — "let the server respond".</b> A non-2xx response is NOT
/// an exception: it becomes <c>data.@this.Fail</c> carrying the status
/// (<c>Error.Key</c> = "NotFound"/"MethodNotAllowed"/… , <c>StatusCode</c> =
/// the HTTP code). Network failures (DNS, connection refused, timeout) become
/// <c>data.@this.Fail</c> with <c>Error.Key = "NetworkError"</c>. PLang
/// programs branch on these via <c>on error</c>.
/// </para>
/// <para>
/// <b>HttpClient lifecycle.</b> One <c>static readonly</c> client shared
/// process-wide — the dotnet-recommended lifecycle (connection pooling, DNS
/// caching). Stateless after init, so multi-App-safe. No per-instance caching:
/// each <c>HttpPath</c> constructs a fresh request every verb call.
/// </para>
/// </remarks>
[PathScheme("http")]
[PathScheme("https")]
public sealed class @this : global::app.types.path.@this
{
    /// <summary>Process-shared HTTP client — see the class remarks.</summary>
    private static readonly HttpClient _client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = true,
    })
    {
        Timeout = TimeSpan.FromSeconds(100),
    };

    private readonly Uri _uri;

    public @this(string raw, actor.context.@this? context = null, object? content = null, string? source = null)
        : base(raw, context, content, source)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Not a valid http(s) URL: '{raw}'", nameof(raw));
        _uri = uri;
    }

    /// <summary>Scheme registry factory entry — mirrors FilePath.Resolve.</summary>
    public static new @this Resolve(string rawPath, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);
        return new @this(rawPath, context) { Raw = rawPath };
    }

    public override string Scheme => _uri.Scheme.ToLowerInvariant();

    /// <summary>The parsed request URI.</summary>
    public Uri Uri => _uri;

    /// <summary>
    /// Canonical-form URL — two HttpPaths addressing the same logical resource
    /// produce the same <c>Absolute</c>, so Permission grants match reliably.
    /// Rules:
    /// <list type="number">
    ///   <item>Scheme + host lowercased.</item>
    ///   <item>Default port stripped (80 for http, 443 for https); non-default kept.</item>
    ///   <item>Path normalized (<c>/a/../b</c> → <c>/b</c>) via <see cref="Uri"/>.</item>
    ///   <item>Root path is the single-slash form (<c>https://host/</c>).</item>
    ///   <item>Query parameters sorted by key; duplicate keys keep original order.</item>
    ///   <item>Fragment stripped (client-side, addresses no server resource).</item>
    /// </list>
    /// </summary>
    public override string Absolute
    {
        get
        {
            var scheme = _uri.Scheme.ToLowerInvariant();
            var host = _uri.Host.ToLowerInvariant();

            // Rule 2: strip default port.
            var sb = new StringBuilder();
            sb.Append(scheme).Append("://").Append(host);
            bool isDefaultPort = _uri.IsDefaultPort
                || (scheme == "http" && _uri.Port == 80)
                || (scheme == "https" && _uri.Port == 443);
            if (!isDefaultPort && _uri.Port >= 0)
                sb.Append(':').Append(_uri.Port);

            // Rule 3 + 4: normalized path; root collapses to "/".
            var path = _uri.AbsolutePath;
            if (string.IsNullOrEmpty(path)) path = "/";
            sb.Append(path);

            // Rule 5: sort query keys; preserve order within a repeated key.
            if (_uri.Query.Length > 1)
            {
                var pairs = _uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                var sorted = pairs
                    .Select((p, i) => (Pair: p, Key: p.Split('=', 2)[0], Index: i))
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ThenBy(x => x.Index)
                    .Select(x => x.Pair);
                sb.Append('?').Append(string.Join('&', sorted));
            }

            // Rule 6: fragment dropped (never appended).
            return sb.ToString();
        }
    }

    // --- Reads ---------------------------------------------------------------

    public override async Task<data.@this> ReadText()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return await Send(HttpMethod.Get, content: null, readBody: true);
    }

    public override async Task<data.@this> ReadBytes()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return await Send(HttpMethod.Get, content: null, readBody: true, asBytes: true);
    }

    public override async Task<data.@this> ExistsAsync()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, _uri);
            await SignRequest(req, null, "HEAD");
            using var resp = await _client.SendAsync(req);
            // Exists answers a question — 2xx → true, 4xx → false, both Success.
            if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300)
                return data.@this.Ok(true);
            if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500)
                return data.@this.Ok(false);
            return data.@this.FromError(MapStatus(resp.StatusCode));
        }
        catch (System.Exception ex) when (IsNetworkError(ex))
        {
            return data.@this.FromError(NetworkError(ex));
        }
    }

    /// <summary>HTTP has no universal directory listing — returns a Fail so the
    /// uniform failure-shape contract holds across schemes.</summary>
    public override Task<data.@this> List() =>
        Task.FromResult(data.@this.FromError(new Error(
            "HTTP scheme does not support directory listing.", "NotSupported", 400)));

    public override async Task<data.@this> Stat()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb(Metadata: true) }) is { } early) return early;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, _uri);
            await SignRequest(req, null, "HEAD");
            using var resp = await _client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode == 404)
                    return data.@this.Ok(new StatInfo(Exists: false));
                return data.@this.FromError(MapStatus(resp.StatusCode));
            }
            return data.@this.Ok(new StatInfo(
                Exists: true,
                IsFile: true,
                Length: resp.Content.Headers.ContentLength,
                Modified: resp.Content.Headers.LastModified?.UtcDateTime));
        }
        catch (System.Exception ex) when (IsNetworkError(ex))
        {
            return data.@this.FromError(NetworkError(ex));
        }
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this> WriteText(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        return await Send(HttpMethod.Post, new StringContent(content, Encoding.UTF8), readBody: false);
    }

    public override async Task<data.@this> WriteBytes(byte[] content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        return await Send(HttpMethod.Post, new ByteArrayContent(content), readBody: false);
    }

    /// <summary>HTTP append maps onto a second POST — servers that support
    /// appending interpret it; others overwrite or 405. "Let the server respond."</summary>
    public override async Task<data.@this> Append(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        return await Send(HttpMethod.Post, new StringContent(content, Encoding.UTF8), readBody: false);
    }

    /// <summary>HTTP has no mkdir — Fail with a uniform shape.</summary>
    public override Task<data.@this> Mkdir() =>
        Task.FromResult(data.@this.FromError(new Error(
            "HTTP scheme does not support directory creation.", "NotSupported", 400)));

    // --- Destructive ---------------------------------------------------------

    public override async Task<data.@this> Delete()
    {
        if (await AuthGate(new Verb { Delete = new DeleteVerb() }) is { } early) return early;
        return await Send(HttpMethod.Delete, content: null, readBody: false);
    }

    // --- HTTP plumbing -------------------------------------------------------

    /// <summary>
    /// Issues one HTTP request and shapes the response. 2xx → Ok (body when
    /// <paramref name="readBody"/>); non-2xx → Fail with the status; network
    /// failure → Fail/NetworkError.
    /// </summary>
    private async Task<data.@this> Send(HttpMethod method, HttpContent? content, bool readBody, bool asBytes = false)
    {
        try
        {
            using var req = new HttpRequestMessage(method, _uri) { Content = content };
            string? bodyForSign = content is StringContent sc ? await sc.ReadAsStringAsync() : null;
            await SignRequest(req, bodyForSign, method.Method);

            using var resp = await _client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return data.@this.FromError(MapStatus(resp.StatusCode));

            if (!readBody) return data.@this.Ok();

            if (asBytes)
                return data.@this.Ok(await resp.Content.ReadAsByteArrayAsync());
            return data.@this.Ok(await resp.Content.ReadAsStringAsync());
        }
        catch (System.Exception ex) when (IsNetworkError(ex))
        {
            return data.@this.FromError(NetworkError(ex));
        }
    }

    /// <summary>
    /// Signs the request with PLang's built-in identity via the
    /// <c>signing.sign</c> action — same mechanism the http module uses.
    /// Adds the <c>X-Signature</c> header. Best-effort: if signing fails the
    /// request still goes out unsigned (the server decides — "let the server
    /// respond").
    /// </summary>
    private async Task SignRequest(HttpRequestMessage request, string? body, string method)
    {
        if (Context == null) return;
        try
        {
            var sign = new modules.signing.sign
            {
                Context = Context,
                Data = new data.@this("", body ?? ""),
                Headers = new data.@this<Dictionary<string, object>>("", new Dictionary<string, object>
                {
                    ["url"] = _uri.ToString(),
                    ["method"] = method,
                }),
            };
            var signResult = await Context.App.RunAction<modules.signing.sign>(sign, Context);
            if (signResult.Success && signResult.Signature != null)
            {
                var json = JsonSerializer.Serialize(signResult.Signature);
                request.Headers.TryAddWithoutValidation("X-Signature", json);
            }
        }
        catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // Signing is best-effort — an unsigned request lets the server respond.
        }
    }

    /// <summary>Maps an HTTP status code to a typed Error (status preserved).</summary>
    private static Error MapStatus(HttpStatusCode status)
    {
        int code = (int)status;
        string key = code switch
        {
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "NotFound",
            405 => "MethodNotAllowed",
            408 => "RequestTimeout",
            429 => "TooManyRequests",
            >= 500 => "ServerError",
            _ => "HttpError",
        };
        return new Error($"HTTP {code} {status}", key, code);
    }

    private static bool IsNetworkError(System.Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException
            or System.IO.IOException;

    private static Error NetworkError(System.Exception ex) =>
        new($"Network failure reaching the resource: {ex.Message}", "NetworkError", 503);
}
