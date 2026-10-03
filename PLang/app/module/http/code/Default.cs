using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using app.actor.context;
using app.error;
using app.goal;
using PlangType = app.type.@this;
using app.module.signing;
using AppType = app.@this;
using SysHttpMethod = System.Net.Http.HttpMethod;
using Call = app.goal.step.action.@this;

namespace app.module.http.code;

/// <summary>
/// Default HTTP provider. Owns all HTTP behavior — actions delegate to this via `this`.
/// Lazily creates HttpClient on first request. Swappable via app.Code.
/// </summary>
public sealed class Default : IHttp
{
    public string Name { get; init; } = "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private readonly HttpMessageHandler? _handler;
    private readonly Dictionary<global::app.module.http.type.redirect.@this, HttpClient> _clients = new();

    public Default() { }

    /// <summary>
    /// Test constructor: injects a custom HttpMessageHandler.
    /// All real provider logic runs — only the HTTP transport is swapped.
    /// </summary>
    public Default(HttpMessageHandler handler) => _handler = handler;

    // --- IHttp: action-level methods ---

    public Task<data.@this> SendAsync(request action) => ExecuteHttpAsync(action.Context, async () =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var app = action.Context.App;
        // T? convention — plang-null pass converts these !/Clr reads (value-door-plang-null branch)
        var unsigned = (await action.Unsigned.Value())!.Value;
        System.TimeSpan timeout = (await action.Timeout.Value())!;
        var content = (await action.Content.Value())!;
        var contentType = content.Mime.ToString();
        var encoding = content.Encoding.ToString();
        var redirect = (await action.Redirect.Value())!;

        // the url the action goes to — the one its build expected
        var target = await ((global::app.module.http.IAddressed)action).Target();
        if (!target.Success) return target;
        var resolvedUrl = (await target.Value())!.ToString();

        var defaultHeaders = action.DefaultHeaders == null || !await action.DefaultHeaders.ToBooleanAsync() ? null
            : (await action.DefaultHeaders.Value()).Clr<Dictionary<string, object>>();
        var headers = MergeHeaders(action.Header == null || !await action.Header.ToBooleanAsync() ? null
            : (await action.Header.Value()).Clr<Dictionary<string, object>>(), defaultHeaders);
        // One Content-Type: a Content-Type header is the content type, in place of the parameter — it names the
        // format the body is written in, and it is sent once, never joined with the parameter's.
        if (headers.Remove("Content-Type", out var named)) contentType = named;

        // Build body
        HttpContent? httpContent = null;
        // A body is content when given — presence, not truthiness: a body of 0 or false is sent. One left
        // out (or null) is none, so a no-body request (GET, etc.) skips serialization.
        var bodyVal = action.Body == null || !action.Body.HasValue ? null : await action.Body.Value();
        if (bodyVal != null)
        {
            // the content type as sent: its media type and parameters; the encoding is its charset unless it names one
            var mediaType = MediaTypeHeaderValue.Parse(contentType);
            mediaType.CharSet ??= encoding;
            if (string.Equals(mediaType.MediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && bodyVal.Clr<Dictionary<string, object>>() is { } formDict)
            {
                var formValues = new Dictionary<string, string>();
                foreach (var kvp in formDict)
                    formValues[kvp.Key] = kvp.Value?.ToString() ?? "";
                httpContent = new FormUrlEncodedContent(formValues);
            }
            else
            {
                // The body is a channel the value is written to: it opens the value (a file sends its content)
                // and the content-type's format writes it; one value, so no line framing.
                await using var body = new global::app.channel.type.stream.@this("body", new MemoryStream(),
                    global::app.channel.ChannelDirection.Output, ownsStream: true) { Mime = contentType };
                var serialized = await body.Write(action.Body!);
                if (!serialized.Success) return serialized;
                httpContent = new ByteArrayContent(((MemoryStream)body.Stream).ToArray());
                httpContent.Headers.ContentType = mediaType;
            }
        }

        var httpMethod = ToSystemMethod((await action.Method.Value())!.Value);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        var completionOption = (action.OnStream == null ? null : await action.OnStream.Value()) != null
            ? HttpCompletionOption.ResponseHeadersRead
            : HttpCompletionOption.ResponseContentRead;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(timeout);

        var response = await SendHttpAsync(requestMessage, completionOption, redirect, cts.Token);

        if ((action.OnStream == null ? null : await action.OnStream.Value()) != null)
        {
            var maxSSEBuffer = (await action.MaxSSEBufferSize.Value())!.ToInt64();
            return await HandleStreamingAsync(
                response, requestMessage, (await action.OnStream.Value()), (action.StreamAs == null ? null : await action.StreamAs.Value())?.Value,
                unsigned, app, action.Context, maxSSEBuffer, cts.Token);
        }

        var maxResponseSize = (await action.MaxResponseSize.Value())!.ToInt64();

        using (response)
        {
            return await ParseResponseAsync(response, requestMessage, unsigned, app, action.Context, maxResponseSize, sw.Elapsed);
        }
    });

    public Task<data.@this> DownloadAsync(download action) => ExecuteHttpAsync(action.Context, async () =>
    {
        var app = action.Context.App;
        // T? convention — plang-null pass converts these (value-door-plang-null branch)
        var unsigned = (await action.Unsigned.Value())!.Value;
        System.TimeSpan timeout = (await action.Timeout.Value())!;
        var redirect = (await action.Redirect.Value())!;

        // the url the action goes to — the one its build expected
        var target = await ((global::app.module.http.IAddressed)action).Target();
        if (!target.Success) return target;
        var resolvedUrl = (await target.Value())!.ToString();

        var defaultHeaders = action.DefaultHeaders == null || !await action.DefaultHeaders.ToBooleanAsync() ? null
            : (await action.DefaultHeaders.Value()).Clr<Dictionary<string, object>>();
        var headers = MergeHeaders(action.Header == null || !await action.Header.ToBooleanAsync() ? null
            : (await action.Header.Value()).Clr<Dictionary<string, object>>(), defaultHeaders);
        var requestMessage = new HttpRequestMessage(SysHttpMethod.Get, resolvedUrl);
        ApplyHeaders(requestMessage, headers);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(timeout);

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, redirect, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorResponseAsync(response, requestMessage, action.Context, cts.Token);
        }

        var totalBytes = response.Content.Headers.ContentLength;
        var maxDownloadSize = (await action.MaxDownloadSize.Value())!;
        var onProgress = action.OnProgress == null ? null : await action.OnProgress.Value();
        System.Func<data.@this, Task>? report = onProgress == null ? null
            : progress => RunCallbackAsync(onProgress, progress, null, "progress", app, action.Context, cts.Token);

        // the digest the body must have, its algorithm the value's
        var expected = action.Hash == null || !action.Hash.IsInitialized ? null : await action.Hash.Value();
        if (action.Hash != null && action.Hash.IsInitialized && !action.Hash.Success) return action.Hash;
        if (expected != null && expected.Algorithm == null)
            return action.Context.Error(new global::app.error.Error(
                "a download's Hash names its algorithm: sha256:<hex>", "HashInvalid", 400));
        using var digest = expected?.Algorithm!.Digest();

        using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var body = new body(responseStream, totalBytes, maxDownloadSize, sent: false, report, digest, action.Context);

        var path = action.Path == null || !action.Path.IsInitialized ? null : await action.Path.Value();
        if (action.Path != null && action.Path.IsInitialized && !action.Path.Success) return action.Path;
        if (path == null)
        {
            using var buffer = new MemoryStream();
            await body.CopyToAsync(buffer, cts.Token);
            var refused = expected?.Mismatch(digest!.Hash);
            await body.Done(refused);
            return refused != null ? action.Context.Error(refused) : action.Context.Ok(buffer.ToArray());
        }

        // the body goes beside the path under a temporary name, and moves in only when it is whole and matches: a
        // failed or mismatched download leaves nothing at the path
        var part = global::app.type.item.path.@this.Resolve(path.Raw + ".part", action.Context);
        data.@this written;
        try { written = await part.Write(body, action.Context); }
        catch (System.Exception)
        {
            // a body cut short (over its cap, too slow, cancelled) leaves no part behind; the failure is still the answer
            await part.Delete(action.Context);
            throw;
        }
        var mismatch = written.Success ? expected?.Mismatch(digest!.Hash) : written.Error;
        await body.Done(mismatch);
        if (!written.Success) return written;
        if (mismatch != null)
        {
            await part.Delete(action.Context);
            return action.Context.Error(mismatch);
        }
        var moved = await part.MoveTo(path, (global::app.type.item.@bool.@this)true, action.Context);
        return moved.Success ? action.Context.Ok<global::app.type.item.path.@this>(path) : moved;
    });

    public Task<data.@this> UploadAsync(upload action) => ExecuteHttpAsync(action.Context, async () =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var app = action.Context.App;
        // T? convention — plang-null pass converts these (value-door-plang-null branch)
        var unsigned = (await action.Unsigned.Value())!.Value;
        System.TimeSpan timeout = (await action.Timeout.Value())!;
        var encoding = (await action.Encoding.Value())!.Clr<string>()!;
        var redirect = (await action.Redirect.Value())!;

        // the url the action goes to — the one its build expected
        var target = await ((global::app.module.http.IAddressed)action).Target();
        if (!target.Success) return target;
        var resolvedUrl = (await target.Value())!.ToString();

        var defaultHeaders = action.DefaultHeaders == null || !await action.DefaultHeaders.ToBooleanAsync() ? null
            : (await action.DefaultHeaders.Value()).Clr<Dictionary<string, object>>();
        var headers = MergeHeaders(action.Header == null || !await action.Header.ToBooleanAsync() ? null
            : (await action.Header.Value()).Clr<Dictionary<string, object>>(), defaultHeaders);

        var (httpContent, contentErr) = await ResolveUploadContentAsync(action, app, encoding);
        if (contentErr != null) return action.Context.Error(contentErr);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(timeout);

        // with a progress goal, the content is sent through a body that reports it
        var onProgress = action.OnProgress == null ? null : await action.OnProgress.Value();
        if (onProgress != null && httpContent != null)
            httpContent = new content(httpContent,
                progress => RunCallbackAsync(onProgress, progress, null, "progress", app, action.Context, cts.Token), action.Context);

        var httpMethod = ToSystemMethod((await action.Method.Value())!.Value);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseContentRead, redirect, cts.Token);

        var maxResponseSize = (await action.MaxResponseSize.Value())!.ToInt64();
        return await ParseResponseAsync(response, requestMessage, unsigned, app, action.Context, maxResponseSize, sw.Elapsed);
    });

    // --- Unified error handling ---

    private async Task<data.@this> ExecuteHttpAsync(actor.context.@this context, Func<Task<data.@this>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException
            or IOException or UnauthorizedAccessException or FormatException)
        {
            return context.Error(new failure(ex));
        }
    }

    // --- Size-limited reads (security: untrusted external data) ---

    private const long DefaultMaxResponseSize = 100 * 1024 * 1024; // 100MB
    private const long MaxErrorBodySize = 4 * 1024; // 4KB for error messages

    /// <summary>
    /// Reads HTTP content as byte array with a size cap and slow-loris guard.
    /// Returns Data so the size/throughput failures carry their own keys instead
    /// of being laundered through the outer catch.
    /// </summary>
    private static async Task<data.@this<global::app.type.item.binary.@this>> ReadLimitedBytesAsync(
        HttpContent content, long maxBytes, actor.context.@this context, CancellationToken ct = default)
    {
        using var stream = await content.ReadAsStreamAsync(ct);
        using var limited = new MemoryStream();
        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;
        var throughputStart = DateTimeOffset.UtcNow;
        long throughputBytes = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxBytes)
                return context.Error<global::app.type.item.binary.@this>(new ServiceError(
                    $"Response body exceeds maximum size of {(new global::app.type.item.size.@this(maxBytes, context))}",
                    "ResponseTooLarge", 413));
            limited.Write(buffer, 0, bytesRead);

            throughputBytes += bytesRead;
            var elapsed = (DateTimeOffset.UtcNow - throughputStart).TotalSeconds;
            if (elapsed >= 30)
            {
                if (throughputBytes / elapsed < 1024)
                    return context.Error<global::app.type.item.binary.@this>(new ServiceError(
                        "Response too slow — possible slow-loris attack",
                        "SlowResponse", 408));
                throughputStart = DateTimeOffset.UtcNow;
                throughputBytes = 0;
            }
        }

        return context.Ok<global::app.type.item.binary.@this>(limited.ToArray());
    }

    /// <summary>
    /// Reads HTTP content as UTF-8 string with a byte size limit. Thin wrapper
    /// over <see cref="ReadLimitedBytesAsync"/> — size-cap / slow-loris logic
    /// lives in one place.
    /// </summary>
    private static async Task<data.@this<global::app.type.item.text.@this>> ReadLimitedStringAsync(
        HttpContent content, long maxBytes, actor.context.@this context, CancellationToken ct = default)
    {
        var bytes = await ReadLimitedBytesAsync(content, maxBytes, context, ct);
        if (!bytes.Success) return context.Error<global::app.type.item.text.@this>(bytes.Error!);
        return context.Ok<global::app.type.item.text.@this>(Encoding.UTF8.GetString((await bytes.Value())!.Clr<byte[]>()!));
    }

    // --- Internal HTTP transport ---

    private Task<HttpResponseMessage> SendHttpAsync(
        HttpRequestMessage request, HttpCompletionOption completionOption,
        global::app.module.http.type.redirect.@this redirect, CancellationToken ct)
        => Client(redirect).SendAsync(request, completionOption, ct);

    public void Dispose()
    {
        foreach (var client in _clients.Values) client.Dispose();
        _clients.Clear();
    }

    // One HttpClient per distinct redirect policy — the redirect record is the key (value-equal). The
    // policy is baked into the handler at construction, so a client is reused across requests with the
    // same policy (socket reuse / pooling) while each request still picks its own. The only lowering to
    // CLR is at the SocketsHttpHandler (BCL) boundary.
    private HttpClient Client(global::app.module.http.type.redirect.@this redirect)
    {
        if (!_clients.TryGetValue(redirect, out var client))
        {
            client = _handler != null
                ? new HttpClient(_handler, disposeHandler: false)
                : new HttpClient(new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    AllowAutoRedirect = redirect.Follow.Value,
                    MaxAutomaticRedirections = redirect.Max.ToInt32()
                });
            _clients[redirect] = client;
        }
        return client;
    }
    // --- Header helpers ---

    private static Dictionary<string, string> MergeHeaders(
        Dictionary<string, object>? stepHeaders,
        Dictionary<string, object>? defaultHeaders)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (defaultHeaders != null)
        {
            foreach (var kvp in defaultHeaders)
                merged[kvp.Key] = kvp.Value?.ToString() ?? "";
        }

        if (stepHeaders != null)
        {
            foreach (var kvp in stepHeaders)
                merged[kvp.Key] = kvp.Value?.ToString() ?? "";
        }

        return merged;
    }

    private static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string> headers)
    {
        foreach (var kvp in headers)
        {
            // Sanitize CRLF to prevent header injection
            var value = kvp.Value.Replace("\r", "").Replace("\n", "");
            if (IsContentHeader(kvp.Key))
            {
                // a content header replaces what the content already carries — one value, never joined
                request.Content?.Headers.Remove(kvp.Key);
                request.Content?.Headers.TryAddWithoutValidation(kvp.Key, value);
            }
            else
                request.Headers.TryAddWithoutValidation(kvp.Key, value);
        }
    }

    private static bool IsContentHeader(string name) =>
        name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Range", StringComparison.OrdinalIgnoreCase);


    // --- Response parsing ---

    private async Task<data.@this> ParseResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        bool unsigned,
        AppType app,
        actor.context.@this context,
        long maxResponseSize = DefaultMaxResponseSize,
        System.TimeSpan duration = default)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorResponseAsync(response, request, context);
        }

        // application/plang response — keeps the historic shape (deserialized
        // Data flows through); no Response wrapping here.
        if (contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase))
        {
            if (unsigned)
            {
                var err = context.Error(new ServiceError(
                    "Unsigned request received application/plang response — this is not allowed",
                    "UnsignedPlang", 403));
                BuildProperties(err, request, response);
                return err;
            }

            return await ParsePlangResponseAsync(response, request, app, context, maxResponseSize);
        }

        // The response body is decoded by its Content-Type's format into LAZY Data, born with the asker's
        // context — the body is NOT deserialized at read time, it materializes on first touch (navigation /
        // As<T>). A status check (%response!status%) reads a Property and never touches the body. A response
        // with no Content-Type is web text.
        var bytesRead = await ReadLimitedBytesAsync(response.Content, maxResponseSize, context);
        if (!bytesRead.Success)
        {
            BuildProperties(bytesRead, request, response);
            return bytesRead;
        }

        var format = context.App.type.list.Mime(string.IsNullOrEmpty(contentType) ? "text/plain" : contentType);
        var result = await format.Decode((await bytesRead.Value())!.Clr<byte[]>()!, context, "http");
        // Metadata (status, headers, duration, url, ...) rides as Properties —
        // read with `!`. BuildProperties populates the protocol metadata; duration
        // is the one timing fact only this layer knows.
        BuildProperties(result, request, response);
        // Duration as seconds (double) — Properties hold wire-supported primitives,
        // so a raw TimeSpan can't ride; total-seconds is queryable (%resp!Duration%).
        result.Properties["Duration"] = duration.TotalSeconds;
        return result;
    }

    /// <summary>
    /// An application/plang response is a whole Data, read by plang's own format: it verifies the
    /// signature it arrived in, so the answer is the Data sent — or the failure it carries, or why it
    /// couldn't be trusted. Same door serves <c>StreamPlangAsync</c>'s per-line NDJSON read.
    /// </summary>
    private async Task<data.@this> ParsePlangResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        AppType app,
        actor.context.@this context,
        long maxResponseSize = DefaultMaxResponseSize)
    {
        var bodyRead = await ReadLimitedStringAsync(response.Content, maxResponseSize, context);
        if (!bodyRead.Success)
        {
            BuildProperties(bodyRead, request, response);
            return bodyRead;
        }
        var body = (await bodyRead.Value())!.Clr<string>()!;

        // Read in the transport's own view — the sender signed what it sent in Out. A parse failure is a
        // keyed Error (PlangDeserializeError), not a throw.
        var read = await context.App.type.list["wire"].kind["plang"]!.Decode(Encoding.UTF8.GetBytes(body), context);
        BuildProperties(read, request, response);
        return read;
    }

    /// <summary>
    /// Reads an error HTTP response and builds a Data error with properties.
    /// </summary>
    private static async Task<data.@this> ReadErrorResponseAsync(
        HttpResponseMessage response, HttpRequestMessage request, actor.context.@this context, CancellationToken ct = default)
    {
        var errorBody = "";
        global::app.error.Error? unread = null;
        // An error body that can't be read (size cap, slow sender, network) is said on the error — the status
        // still answers, and why its body is missing isn't lost.
        try
        {
            var read = await ReadLimitedStringAsync(response.Content, MaxErrorBodySize, context, ct);
            if (read.Success) errorBody = (await read.Value())!.Clr<string>()!;
            else unread = read.Error;
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            unread = new ServiceError($"the error response's body couldn't be read: {ex.Message}", "HttpBodyUnreadable", 500) { Exception = ex };
        }
        var err = context.Error(new ServiceError(
            $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}".Trim(),
            "HttpError", (int)response.StatusCode));
        if (unread != null) err.Error!.list.Add(unread);
        BuildProperties(err, request, response);
        return err;
    }

    // --- Response metadata ---

    private static void BuildProperties(data.@this data, HttpRequestMessage request, HttpResponseMessage response)
    {
        var props = data.Properties;

        props["Url"] = request.RequestUri?.ToString();
        props["Method"] = request.Method.Method;

        var reqHeaders = new Dictionary<string, object?>();
        foreach (var h in request.Headers)
            reqHeaders[h.Key] = string.Join(", ", h.Value);
        props["RequestHeaders"] = reqHeaders;

        if (request.Content != null)
        {
            props["ContentType"] = request.Content.Headers.ContentType?.ToString();
            props["ContentLength"] = request.Content.Headers.ContentLength;
        }

        // %response!status% — its code, the server's own reason (%response!status.text%), whether it is a success
        // (%response!status.ok%); it compares with a number by its code (%response!status% == 200).
        props["Status"] = new global::app.type.item.status.@this((int)response.StatusCode, response.ReasonPhrase ?? "");

        var respHeaders = new Dictionary<string, object?>();
        foreach (var h in response.Headers)
            respHeaders[h.Key] = string.Join(", ", h.Value);
        props["Headers"] = respHeaders;

        var contentHeaders = new Dictionary<string, object?>();
        foreach (var h in response.Content.Headers)
            contentHeaders[h.Key] = string.Join(", ", h.Value);
        props["ContentHeaders"] = contentHeaders;

        if (response.Content.Headers.ContentType?.CharSet != null)
            props["Charset"] = response.Content.Headers.ContentType.CharSet;
    }

    // --- Streaming ---

    private async Task<data.@this> HandleStreamingAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        Call onStream,
        StreamFormat? streamAs,
        bool unsigned,
        AppType app,
        actor.context.@this context,
        long maxSSEBufferSize,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                return await ReadErrorResponseAsync(response, request, context, ct);
            }
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var format = streamAs ?? DetectStreamFormat(contentType);

        var isPlang = contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase);
        if (isPlang && unsigned)
        {
            using (response)
            {
                var err = context.Error(new ServiceError(
                    "Unsigned request received application/plang streaming response — this is not allowed",
                    "UnsignedPlang", 403));
                BuildProperties(err, request, response);
                return err;
            }
        }

        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);

            switch (format)
            {
                case StreamFormat.Bytes:
                    await StreamBytesAsync(stream, onStream, app, context, ct);
                    break;

                case StreamFormat.SSE:
                    await StreamSSEAsync(stream, onStream, app, context, maxSSEBufferSize, ct);
                    break;

                default:
                    if (isPlang)
                        await StreamPlangAsync(stream, onStream, app, context, ct);
                    else
                        await StreamLinesAsync(stream, onStream, app, context, ct);
                    break;
            }

            var result = context.Ok();
            BuildProperties(result, request, response);
            return result;
        }
    }

    /// <summary>
    /// Runs the held callback call once for one runtime value. The value is run-state: it binds as
    /// the variable <paramref name="name"/> (the name the slot's <c>[GoalCallback]</c> advertises —
    /// <c>%chunk%</c>, <c>%progress%</c>) in the context the call runs under; the held call then runs
    /// as itself, binding its authored arguments after, so an authored name wins a collision.
    /// </summary>
    private static async Task RunCallbackAsync(
        Call held, object? value, PlangType? type, string name,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        // A Data payload rides AS the variable — re-boxing would nest a bare Data, which the store
        // seam rejects.
        data.@this bound;
        if (value is data.@this dv) { dv.Name = name; bound = dv; }
        else bound = new data.@this(name, value, type, context: context);
        await context.Variable.Set(name, bound);

        var result = await held.Start(context);
        if (!result.Success)
            await app.actor.list.System.Channel[global::app.channel.list.@this.Error].WriteText(result.Error?.Message ?? "");
    }

    private static StreamFormat DetectStreamFormat(string contentType)
    {
        if (contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
            return StreamFormat.SSE;
        return StreamFormat.Line;
    }

    private static async Task StreamLinesAsync(
        Stream stream, Call onStream,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            await RunCallbackAsync(onStream, line, context.App.type.list["text"], "chunk", app, context, ct);
        }
    }

    private static async Task StreamSSEAsync(
        Stream stream, Call onStream,
        AppType app, actor.context.@this context, long maxBufferSize, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataBuffer = new StringBuilder();
        int consecutiveOverflows = 0;
        const int maxConsecutiveOverflows = 3;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                if (dataBuffer.Length > 0)
                    await RunCallbackAsync(onStream, dataBuffer.ToString(), context.App.type.list["text"], "chunk", app, context, ct);
                break;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var data = line.Length > 5 ? line[5..].TrimStart() : "";

                // Guard against unbounded SSE messages (no blank-line boundary)
                if (dataBuffer.Length + data.Length + 1 > maxBufferSize)
                {
                    consecutiveOverflows++;
                    if (consecutiveOverflows >= maxConsecutiveOverflows)
                        throw new global::app.error.AppException(
                            $"SSE stream disconnected after {maxConsecutiveOverflows} consecutive buffer overflows — possible attack",
                            "SSEBufferOverflow", 413);

                    await app.actor.list.System.Channel[global::app.channel.list.@this.Error].WriteAsync(
                        context.Error(new ServiceError(
                            $"SSE message exceeds maximum buffer size of {maxBufferSize / (1024 * 1024)}MB",
                            "SSEBufferOverflow", 413)));
                    dataBuffer.Clear();
                    continue;
                }

                if (dataBuffer.Length > 0) dataBuffer.Append('\n');
                dataBuffer.Append(data);
            }
            else if (line.Length == 0 && dataBuffer.Length > 0)
            {
                consecutiveOverflows = 0; // successful event resets counter
                await RunCallbackAsync(onStream, dataBuffer.ToString(), context.App.type.list["text"], "chunk", app, context, ct);
                dataBuffer.Clear();
            }
        }
    }

    private static async Task StreamBytesAsync(
        Stream stream, Call onStream,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            var chunk = new byte[bytesRead];
            System.Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

            await RunCallbackAsync(onStream, chunk, null, "chunk", app, context, ct);
        }
    }

    private async Task StreamPlangAsync(
        Stream stream, Call onStream,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            // Each NDJSON line is a whole Data in plang's own format, its signature verified. What the read
            // answers is that chunk's answer — the Data sent, the failure it carries, or why it couldn't be
            // read or trusted — so the callback's on error sees every one.
            var data = await context.App.type.list["wire"].kind["plang"]!.Decode(Encoding.UTF8.GetBytes(line), context, ct: ct);
            await RunCallbackAsync(onStream, data, null, "chunk", app, context, ct);
        }
    }

    // --- Upload content resolution ---

    // HttpContent is a transport artifact, never a PLang value — it rides as a plain
    // (HttpContent?, error) tuple, not Data<HttpContent>.
    private static async Task<(HttpContent? Content, global::app.error.Error? Error)> ResolveUploadContentAsync(
        upload action, global::app.@this app, string encoding)
    {
        var content = await action.Content.Value();
        var context = action.Context;

        // The value writes ITSELF in the body's format — no Lower + STJ (which emits the wrapper's C#
        // property bag), no type-shape branch. Text writes a leaf bare and a container as json; json writes json.
        async Task<string> Body(string mime)
        {
            using var ms = new MemoryStream();
            await context.App.type.list.Mime(mime).Encode(ms, action.Content, context, encoding: Encoding.GetEncoding(encoding));
            return Encoding.GetEncoding(encoding).GetString(ms.ToArray());
        }

        if ((action.As == null ? null : await action.As.Value()) is { } asChoice && asChoice is { } && (ContentAs?)asChoice is { } contentAs)
        {
            switch (contentAs)
            {
                case ContentAs.File: return await CreateFileContentAsync(app, context, content!.ToString()!);
                case ContentAs.Base64: return (CreateBase64Content(content!.ToString()!), null);
                case ContentAs.Form: return await CreateFormContentAsync(app, context, content!);
                case ContentAs.Text:
                    return (new StringContent(await Body("text/plain"), Encoding.GetEncoding(encoding)), null);
                default:
                    return (new StringContent(content!.ToString()!, Encoding.GetEncoding(encoding)), null);
            }
        }

        // Auto-detect
        if (content is global::app.type.item.dict.@this
            || content is Clr { Value: Dictionary<string, object> or JsonElement { ValueKind: JsonValueKind.Object } })
        {
            return await CreateFormContentAsync(app, context, content);
        }

        if (content is global::app.type.item.text.@this)
        {
            var str = content.ToString()!;
            // Try as file path — gated through path.Exists (AuthGate(Read)).
            // Out-of-root probes prompt or deny; in-root fast-passes. Any failure
            // (including denial) falls through to "treat as a string body" —
            // matches the prior "if not a file, send as string" shape.
            var p = global::app.type.item.path.@this.Resolve(str, context);
            var exists = await p.Exists(context);
            if (exists.Success && await exists.ToBooleanAsync())
                return await CreateFileContentAsync(app, context, str);

            return (new StringContent(str, Encoding.GetEncoding(encoding)), null);
        }

        // The native value writes ITSELF as json content (list/object), never the C# property bag.
        return (new StringContent(
            await Body("application/json"),
            Encoding.GetEncoding(encoding),
            "application/json"), null);
    }

    // internal so HttpStaticFileDenialTests can invoke the handler's read
    // path directly (driving the full upload action requires a real HTTP
    // endpoint).
    internal static async Task<(HttpContent? Content, global::app.error.Error? Error)> CreateFileContentAsync(global::app.@this app, actor.context.@this context, string path)
    {
        // The file's raw bytes, through the gate — out-of-root paths the actor hasn't granted bubble up as Fail.
        var resolved = global::app.type.item.path.@this.Resolve(path, context);
        var read = await resolved.Bytes(context);
        if (!read.Success || await read.Value() == null)
            return (null, read.Error
                ?? new ServiceError($"Could not read file: {path}", "FileReadError", 500));
        var content = new ByteArrayContent((await read.Value())!.Clr<byte[]>()!);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return (content, null);
    }

    private static HttpContent CreateBase64Content(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static async Task<(HttpContent? Content, global::app.error.Error? Error)> CreateFormContentAsync(global::app.@this app, actor.context.@this context, object content)
    {
        var form = new MultipartFormDataContent();
        Dictionary<string, object> fields;

        // A native dict value (the production shape — Content resolved from %var%) lowers to its
        // entries via its own Clr; a raw CLR Dictionary passes straight through.
        if (((content as global::app.type.item.@this)?.Clr<Dictionary<string, object>>()
             ?? content as Dictionary<string, object>) is { } dict)
            fields = dict;
        else if (content is JsonElement je)
        {
            fields = new Dictionary<string, object>();
            foreach (var prop in je.EnumerateObject())
                fields[prop.Name] = prop.Value.ToString();
        }
        else
            fields = new Dictionary<string, object> { ["data"] = content };

        foreach (var kvp in fields)
        {
            var value = kvp.Value?.ToString() ?? "";
            if (value.StartsWith('@'))
            {
                // The file's raw bytes, through the gate: out-of-root form fields the actor hasn't
                // authorized get denied at the gate, not silently exfiltrated.
                var fp = global::app.type.item.path.@this.Resolve(value[1..], context);
                var read = await fp.Bytes(context);
                if (!read.Success || await read.Value() == null)
                    return (null, read.Error
                        ?? new ServiceError($"Could not read form file: {value[1..]}", "FileReadError", 500));
                var fileContent = new ByteArrayContent((await read.Value())!.Clr<byte[]>()!);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, kvp.Key, fp.Name.ToString());
            }
            else
            {
                form.Add(new StringContent(value), kvp.Key);
            }
        }

        return (form, null);
    }

    // --- Static utilities ---

    private static SysHttpMethod ToSystemMethod(HttpMethod method) => method switch
    {
        HttpMethod.GET => SysHttpMethod.Get,
        HttpMethod.POST => SysHttpMethod.Post,
        HttpMethod.PUT => SysHttpMethod.Put,
        HttpMethod.DELETE => SysHttpMethod.Delete,
        HttpMethod.PATCH => SysHttpMethod.Patch,
        HttpMethod.HEAD => SysHttpMethod.Head,
        HttpMethod.OPTIONS => SysHttpMethod.Options,
        HttpMethod.QUERY => new SysHttpMethod("QUERY"),
        _ => SysHttpMethod.Get
    };
}
