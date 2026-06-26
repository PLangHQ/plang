using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using app.error;
using Verb = global::app.type.permission.Verb;

namespace app.type.path.http;

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
public sealed partial class @this : global::app.type.path.@this
{
    /// <summary>Process-shared HTTP client — see the class remarks.</summary>
    /// <remarks>
    /// <c>AllowAutoRedirect = false</c> by design. Letting
    /// the client follow 3xx silently lets a consented host trade us into
    /// reading something else — IMDS at <c>169.254.169.254</c>, loopback
    /// services, private-IP ranges — with the user only ever consenting to
    /// the original URL. Redirects are now handled in <see cref="SendWithRedirects"/>:
    /// each hop builds a fresh <see cref="@this"/>, calls <see cref="@this.AuthGate"/>
    /// on it (separate consent prompt for the new host), and signs the new
    /// destination's URL fresh — the prior hop's <c>X-Signature</c> never
    /// reaches a different origin.
    /// </remarks>
    private static readonly HttpClient _client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = false,
    })
    {
        Timeout = TimeSpan.FromSeconds(100),
    };

    /// <summary>Maximum redirect hops a single verb call will follow. Each
    /// hop costs the user a fresh consent prompt; the cap protects against
    /// loops and exhaustion attacks regardless.</summary>
    private const int MaxRedirectHops = 5;

    private readonly Uri _uri;

    public @this(string raw, actor.context.@this? context = null)
        : base(raw, context)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Not a valid http(s) URL: '{raw}'", nameof(raw));

        // Strip embedded userinfo at construction (security v2 S4.b).
        // Otherwise the consent prompt (which renders Absolute) hides the
        // userinfo while _uri still carries it on the wire — and the
        // userinfo-stripped Absolute persists as the grant key, so an
        // attacker-controlled redirect to `user:pwd@trusted-host/` would
        // silently match the existing grant for `trusted-host/`. Stripping
        // here makes Absolute, the fetched URL, and the grant key all the
        // same string.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var builder = new UriBuilder(uri) { UserName = "", Password = "" };
            uri = builder.Uri;
        }
        _uri = uri;
    }

    /// <summary>Scheme registry factory entry — mirrors FilePath.Resolve.</summary>
    public static new @this Resolve(string rawPath, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);
        return new @this(rawPath, context) { Raw = rawPath };
    }

    [Out, Store] public override string Scheme => _uri.Scheme.ToLowerInvariant();

    /// <summary>The parsed request URI.</summary>
    public Uri Uri => _uri;

    /// <summary>
    /// Warn the user before persisting a URL whose query string carries
    /// likely secrets (tokens, keys, signatures). Answering 'a' writes the
    /// full Absolute — query string included — into the local permission
    /// store; this hint lands in the consent prompt so a user who would
    /// strip <c>?token=…</c> before sharing the URL elsewhere gets the same
    /// signal here. Suppressed when there is no query string.
    /// </summary>
    protected override string AuthorizationHint(global::app.type.permission.Verb verb)
    {
        if (string.IsNullOrEmpty(_uri.Query) || _uri.Query == "?") return "";
        return "(note: answering 'a' saves the full URL — query string included — to your local permission store)";
    }

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
    internal override string Absolute
    {
        get
        {
            var scheme = _uri.Scheme.ToLowerInvariant();
            // Use IdnHost (punycode) instead of Host (Unicode) so a homograph
            // attack on the consent prompt — e.g. Cyrillic 'а' in аpple.com —
            // renders as `xn--pple-43d.com` and the user sees that something
            // is off. Same canonical form lands in the persisted grant key,
            // so cache hits also use punycode and a homograph variant doesn't
            // silently match a previously-trusted ASCII host. (security v2 S4.a)
            var host = _uri.IdnHost.ToLowerInvariant();

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
        var verb = Verb.Read;
        if (await AuthGate(verb) is { } early) return early;
        return await Send(HttpMethod.Get, content: null, readBody: true, verb);
    }

    public override async Task<data.@this<global::app.type.binary.@this>> ReadBytes()
    {
        var verb = Verb.Read;
        if (await AuthGate(verb) is { } early) return data.@this<global::app.type.binary.@this>.From(early);
        return data.@this<global::app.type.binary.@this>.From(await Send(HttpMethod.Get, content: null, readBody: true, verb, asBytes: true));
    }

    public override async Task<data.@this<global::app.type.@bool.@this>> ExistsAsync()
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.@bool.@this>.From(early);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, _uri);
            await SignRequest(req, null, "HEAD");
            using var resp = await _client.SendAsync(req);
            // Exists answers a question — 2xx → true, 4xx → false, both Success.
            if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300)
                return Context!.Ok<global::app.type.@bool.@this>(true);
            if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500)
                return Context!.Ok<global::app.type.@bool.@this>(false);
            return Context!.Error<global::app.type.@bool.@this>(MapStatus(resp.StatusCode));
        }
        catch (System.Exception ex) when (IsNetworkError(ex))
        {
            return Context!.Error<global::app.type.@bool.@this>(NetworkError(ex));
        }
    }

    /// <summary>
    /// HTTP has no universal directory listing. <paramref name="pattern"/> /
    /// <paramref name="recursive"/> are filesystem-only — no-ops here. Returns a
    /// Fail, but routes through <see cref="@this.AuthGate"/> first so the verb
    /// surface is consistent with every other HttpPath verb.
    /// </summary>
    public override async Task<data.@this<global::app.type.list.@this<global::app.type.path.@this>>> List(string pattern, bool recursive)
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.list.@this<global::app.type.path.@this>>.From(early);
        return Context!.Error<global::app.type.list.@this<global::app.type.path.@this>>(new Error(
            "HTTP scheme does not support directory listing.", "NotSupported", 400));
    }

    /// <summary>
    /// Truthiness of an http path is "does the resource exist" — an HTTP HEAD.
    /// Reuses <see cref="ExistsAsync"/>; a denied or errored probe answers false.
    /// 
    /// </summary>
    public override async Task<bool> AsBooleanAsync()
    {
        var existsResult = await ExistsAsync();
        return existsResult.Success && await existsResult.ToBooleanAsync();
    }

    public override async Task<data.@this<global::app.type.path.@this.StatInfo>> Stat()
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.path.@this.StatInfo>.From(early);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, _uri);
            await SignRequest(req, null, "HEAD");
            using var resp = await _client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode == 404)
                    return Context!.Ok<global::app.type.path.@this.StatInfo>(new StatInfo(Exists: false));
                return Context!.Error<global::app.type.path.@this.StatInfo>(MapStatus(resp.StatusCode));
            }
            return Context!.Ok<global::app.type.path.@this.StatInfo>(new StatInfo(
                Exists: true,
                IsFile: true,
                Length: resp.Content.Headers.ContentLength,
                Modified: resp.Content.Headers.LastModified?.UtcDateTime));
        }
        catch (System.Exception ex) when (IsNetworkError(ex))
        {
            return Context!.Error<global::app.type.path.@this.StatInfo>(NetworkError(ex));
        }
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this<global::app.type.path.@this>> WriteText(string content)
    {
        var verb = Verb.Write;
        if (await AuthGate(verb) is { } early) return data.@this<global::app.type.path.@this>.From(early);
        var sent = await Send(HttpMethod.Post, new StringContent(content, Encoding.UTF8), readBody: false, verb);
        return sent.Success ? Context!.Ok<global::app.type.path.@this>(this) : data.@this<global::app.type.path.@this>.From(sent);
    }

    public override async Task<data.@this<global::app.type.path.@this>> WriteBytes(byte[] content)
    {
        var verb = Verb.Write;
        if (await AuthGate(verb) is { } early) return data.@this<global::app.type.path.@this>.From(early);
        var sent = await Send(HttpMethod.Post, new ByteArrayContent(content), readBody: false, verb);
        return sent.Success ? Context!.Ok<global::app.type.path.@this>(this) : data.@this<global::app.type.path.@this>.From(sent);
    }

    /// <summary>HTTP append maps onto a second POST — servers that support
    /// appending interpret it; others overwrite or 405. "Let the server respond."</summary>
    public override async Task<data.@this<global::app.type.path.@this>> Append(string content)
    {
        var verb = Verb.Write;
        if (await AuthGate(verb) is { } early) return data.@this<global::app.type.path.@this>.From(early);
        var sent = await Send(HttpMethod.Post, new StringContent(content, Encoding.UTF8), readBody: false, verb);
        return sent.Success ? Context!.Ok<global::app.type.path.@this>(this) : data.@this<global::app.type.path.@this>.From(sent);
    }

    /// <summary>
    /// HTTP has no mkdir — Fail, routed through <see cref="@this.AuthGate"/>
    /// first for verb-surface consistency.
    /// </summary>
    public override async Task<data.@this<global::app.type.path.@this>> Mkdir()
    {
        if (await AuthGate(Verb.Write) is { } early) return data.@this<global::app.type.path.@this>.From(early);
        return Context!.Error<global::app.type.path.@this>(new Error(
            "HTTP scheme does not support directory creation.", "NotSupported", 400));
    }

    /// <summary>
    /// Write the value to the resource: a byte payload POSTs as bytes,
    /// everything else POSTs as text. Authorization happens inside
    /// WriteBytes/WriteText.
    /// </summary>
    public override async Task<data.@this<global::app.type.path.@this>> Save(data.@this? value)
    {
        var raw = value == null ? null : await value.Value();
        if (raw is global::app.type.binary.@this bin) return await WriteBytes(bin.Value);
        return await WriteText(raw?.ToString() ?? "");
    }

    // --- Destructive ---------------------------------------------------------

    /// <summary>
    /// HTTP DELETE. <paramref name="recursive"/> / <paramref name="ignoreIfNotFound"/>
    /// are filesystem-only — no-ops here; the server decides.
    /// </summary>
    public override async Task<data.@this<global::app.type.path.@this>> Delete(bool recursive, bool ignoreIfNotFound)
    {
        var verb = Verb.Delete;
        if (await AuthGate(verb) is { } early) return data.@this<global::app.type.path.@this>.From(early);
        var sent = await Send(HttpMethod.Delete, content: null, readBody: false, verb);
        return sent.Success ? Context!.Ok<global::app.type.path.@this>(this) : data.@this<global::app.type.path.@this>.From(sent);
    }

    // --- HTTP plumbing -------------------------------------------------------

    /// <summary>
    /// Issues an HTTP request and shapes the response. 2xx → Ok (body when
    /// <paramref name="readBody"/>); non-2xx (non-3xx) → Fail with the status;
    /// network failure → Fail/NetworkError. 3xx responses delegate to
    /// <see cref="FollowRedirect"/> — each hop costs a fresh
    /// <see cref="@this.AuthGate"/> prompt on the new URL and signs with that
    /// host's destination, so the user's consent and identity never cross
    /// the trust boundary of an origin they didn't explicitly OK.
    ///
    /// <paramref name="verb"/> rides along so the redirect hop's AuthGate
    /// uses the same verb the calling FS-action started with.
    /// </summary>
    private Task<data.@this> Send(HttpMethod method, HttpContent? content, bool readBody, Verb verb, bool asBytes = false)
        => SendWithHops(method, content, readBody, verb, asBytes, MaxRedirectHops);

    private async Task<data.@this> SendWithHops(HttpMethod method, HttpContent? content, bool readBody, Verb verb, bool asBytes, int hopsLeft)
    {
        try
        {
            using var req = new HttpRequestMessage(method, _uri) { Content = content };
            string? bodyForSign = content is StringContent sc ? await sc.ReadAsStringAsync() : null;
            await SignRequest(req, bodyForSign, method.Method);

            using var resp = await _client.SendAsync(req);

            // 3xx → manual redirect with consent + fresh signing on each hop.
            if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400 && resp.Headers.Location != null)
                return await FollowRedirect(resp, method, content, readBody, verb, asBytes, hopsLeft);

            if (!resp.IsSuccessStatusCode)
                return Context!.Error(MapStatus(resp.StatusCode));

            if (!readBody) return Context!.Ok();

            // Wrap bytes born-native so ReadBytes→From<binary> extracts the value
            // (From's `source.Value is T` test matches only the wrapper, not raw byte[]).
            // The response Content-Type rides as a Property so the url door can stamp
            // by it (Content-Type rules over the URL extension); empty when the server
            // sent none.
            if (asBytes)
            {
                var bytesData = Context!.Ok((object)new global::app.type.binary.@this(await resp.Content.ReadAsByteArrayAsync()));
                bytesData.Properties.Set("contentType", resp.Content.Headers.ContentType?.MediaType ?? "");
                return bytesData;
            }
            return Context!.Ok(await resp.Content.ReadAsStringAsync());
        }
        catch (System.Exception ex) when (IsNetworkError(ex))
        {
            return Context!.Error(NetworkError(ex));
        }
    }

    /// <summary>
    /// Handles a 3xx by resolving the Location header against the current
    /// URI, constructing a fresh <see cref="@this"/>, gating it through
    /// <see cref="@this.AuthGate"/> (the user sees and consents to the new
    /// URL), and sending the next request from that path. Method/body
    /// preservation mirrors RFC 7231: 303 → GET (no body); 301/302/307/308 →
    /// keep method and body.
    /// </summary>
    private async Task<data.@this> FollowRedirect(HttpResponseMessage resp, HttpMethod method, HttpContent? content,
        bool readBody, Verb verb, bool asBytes, int hopsLeft)
    {
        if (hopsLeft <= 0)
            return Context!.Error(new Error(
                $"Too many redirects (>{MaxRedirectHops}) — refusing to follow further.",
                "TooManyRedirects", 508));

        var target = resp.Headers.Location!.IsAbsoluteUri
            ? resp.Headers.Location
            : new Uri(_uri, resp.Headers.Location);

        if (target.Scheme != "http" && target.Scheme != "https")
            return Context!.Error(new Error(
                $"Redirect target uses unsupported scheme '{target.Scheme}'.",
                "UnsupportedRedirectScheme", 400));

        // Method/body downgrade per RFC 7231 §6.4.4 — 303 is "see other" with GET.
        var nextMethod = (int)resp.StatusCode == 303 ? HttpMethod.Get : method;

        // Re-buffer the body for the next hop. HttpContent is single-send —
        // .NET disposes the underlying stream after the first SendAsync, so
        // passing the same instance into the next request fails with
        // "The request message was already sent." Buffer once into a
        // ByteArrayContent that can be (re-)read per hop, preserving any
        // headers the caller set on the original (Content-Type etc.).
        HttpContent? nextContent = null;
        if (nextMethod != HttpMethod.Get && content != null)
        {
            var bytes = await content.ReadAsByteArrayAsync();
            var rebuilt = new ByteArrayContent(bytes);
            foreach (var h in content.Headers)
                rebuilt.Headers.TryAddWithoutValidation(h.Key, h.Value);
            nextContent = rebuilt;
        }

        var nextPath = new @this(target.ToString(), Context);

        // The consent prompt for the new host. AuthGate returns null on grant.
        if (await nextPath.AuthGate(verb) is { } denial) return denial;

        return await nextPath.SendWithHops(nextMethod, nextContent, readBody, verb, asBytes, hopsLeft - 1);
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
            // Sign over a canonical request line — method, path, then the body. This
            // binds WHAT the request does (method + path) into the signed value, so it
            // is never empty (a bodyless GET still signs) and a redirect to a different
            // path produces a fresh, destination-specific signature. Path only by
            // design — the recipient validates the host against itself.
            var canonical = $"{method}\n{_uri.PathAndQuery}\n{body ?? ""}";
            var sign = new module.signing.sign
            {
                Context = Context,
                Data = new data.@this("", canonical, context: Context),
            };
            var signResult = await Context.App.RunAction<module.signing.sign>(sign, Context);
            if (signResult.Success)
            {
                var json = JsonSerializer.Serialize(signResult);
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
