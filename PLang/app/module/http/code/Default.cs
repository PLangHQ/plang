using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app.channel.serializer;
using app.actor.context;
using app.error;
using app.goal;
using app.variable;
using PlangType = app.type.@this;
using app.config;
using app.module.signing;
using AppType = app.@this;
using SysHttpMethod = System.Net.Http.HttpMethod;

namespace app.module.http.code;

/// <summary>
/// Default HTTP provider. Owns all HTTP behavior — actions delegate to this via `this`.
/// Lazily creates HttpClient on first request. Swappable via app.Code.
/// </summary>
public sealed class Default : IHttp
{
    public string Name { get; init; } = "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private readonly HttpMessageHandler? _handler;
    private HttpClient? _client;

    public Default() { }

    /// <summary>
    /// Test constructor: injects a custom HttpMessageHandler.
    /// All real provider logic runs — only the HTTP transport is swapped.
    /// </summary>
    public Default(HttpMessageHandler handler) => _handler = handler;

    private readonly JsonSerializerOptions _transportInOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { global::app.channel.serializer.filter.Transport.ForInbound }
        }
    };

    // Case-insensitive read for HTTP responses (signing, JSON body parsing, plang).
    // Stage 27 disperse-from-Json target — was Utils.Json.CaseInsensitiveRead.
    private readonly JsonSerializerOptions _caseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true), new app.data.EmptyStringToNullEnumConverterFactory(), new global::app.channel.serializer.TimeSpanIso8601() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // --- IHttp: action-level methods ---

    public Task<data.@this> SendAsync(request action) => ExecuteHttpAsync(async () =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var app = action.Context.App;
        var config = app.Config.For<Config>(action.Context);

        var unsigned = ((await action.Unsigned.Value())?.Value ?? false) || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec == null ? config.Resolve("TimeoutInSec", 30)
            : (await action.TimeoutInSec.Value())?.ToDouble() ?? 0;
        if (timeout <= 0) timeout = config.Resolve("TimeoutInSec", 30);
        string contentType = (action.ContentType == null ? null : await action.ContentType.Value()) is { } ctv ? ctv.Clr<string>()! : config.Resolve("ContentType", "application/json");
        var encoding = (await action.Encoding.Value())?.Clr<string>() ?? config.Resolve("Encoding", "utf-8");

        var urlResult = ResolveUrl((await action.Url.Value())!.Clr<string>()!, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = (await urlResult.Value())!.Clr<string>()!;

        var headers = MergeHeaders(action.Headers == null ? null
            : global::app.type.item.@this.Lower<Dictionary<string, object>>(await action.Headers.Value()), config);

        // Build body
        HttpContent? httpContent = null;
        var bodyVal = action.Body == null ? null : await action.Body.Value();
        if (bodyVal != null)
        {
            if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && global::app.type.item.@this.Lower<Dictionary<string, object>>(bodyVal) is { } formDict)
            {
                var formValues = new Dictionary<string, string>();
                foreach (var kvp in formDict)
                    formValues[kvp.Key] = kvp.Value?.ToString() ?? "";
                httpContent = new FormUrlEncodedContent(formValues);
            }
            else
            {
                // All I/O goes through the channel: the serializer for the
                // content-type renders the body value into the request stream via
                // its OWN converter (a dict/list/item serializes as itself), instead
                // of raw STJ on the `object`-typed value — which bypasses the
                // converter and reflects the base item property bag.
                var ms = new MemoryStream();
                var serialized = await action.Context.Actor.Channel.Serializers
                    .GetOrDefault(contentType).SerializeAsync(ms, action.Body!);
                if (!serialized.Success) return serialized;
                var bytes = ms.ToArray();
                // SerializeAsync frames with a trailing newline (NDJSON streaming);
                // an HTTP body is a single document — drop the frame delimiter.
                int n = bytes.Length;
                while (n > 0 && (bytes[n - 1] == (byte)'\n' || bytes[n - 1] == (byte)'\r')) n--;
                httpContent = new ByteArrayContent(bytes, 0, n);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType) { CharSet = encoding };
            }
        }

        var httpMethod = ToSystemMethod((await action.Method.Value())!.Value);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, (action.SignOptions == null ? null : await action.SignOptions.Value()), resolvedUrl, httpMethod.Method);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        var completionOption = (action.OnStream == null ? null : await action.OnStream.Value()) != null
            ? HttpCompletionOption.ResponseHeadersRead
            : HttpCompletionOption.ResponseContentRead;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        var response = await SendHttpAsync(requestMessage, completionOption, config, cts.Token);

        if ((action.OnStream == null ? null : await action.OnStream.Value()) != null)
        {
            var maxSSEBuffer = config.Resolve("MaxSSEBufferSize", 10L * 1024 * 1024);
            return await HandleStreamingAsync(
                response, requestMessage, (await action.OnStream.Value()), (action.StreamAs == null ? null : await action.StreamAs.Value())?.Value,
                unsigned, app, action.Context, maxSSEBuffer, cts.Token);
        }

        var maxResponseSize = config.Resolve("MaxResponseSize", DefaultMaxResponseSize);

        using (response)
        {
            return await ParseResponseAsync(response, requestMessage, unsigned, app, action.Context, maxResponseSize, sw.Elapsed);
        }
    });

    public Task<data.@this> DownloadAsync(download action) => ExecuteHttpAsync(async () =>
    {
        var app = action.Context.App;
        var config = app.Config.For<Config>(action.Context);

        var unsigned = ((await action.Unsigned.Value())?.Value ?? false) || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec == null ? config.Resolve("TimeoutInSec", 30)
            : (await action.TimeoutInSec.Value())?.ToDouble() ?? 0;
        if (timeout <= 0) timeout = config.Resolve("TimeoutInSec", 30);
        var urlResult = ResolveUrl((await action.Url.Value())!.Clr<string>()!, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = (await urlResult.Value())!.Clr<string>()!;

        var headers = MergeHeaders(action.Headers == null ? null
            : global::app.type.item.@this.Lower<Dictionary<string, object>>(await action.Headers.Value()), config);
        var requestMessage = new HttpRequestMessage(SysHttpMethod.Get, resolvedUrl);
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, (action.SignOptions == null ? null : await action.SignOptions.Value()), resolvedUrl, "GET");
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, config, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var (err, _) = await ReadErrorResponseAsync(response, requestMessage, ct: cts.Token);
            return err;
        }

        var totalBytes = response.Content.Headers.ContentLength;
        var maxDownloadSize = config.Resolve("MaxDownloadSize", DefaultMaxResponseSize);
        using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var buffer = new MemoryStream();

        await StreamWithProgressAsync(
            responseStream, buffer, totalBytes, maxDownloadSize, (action.OnProgress == null ? null : await action.OnProgress.Value()), app, action.Context, cts.Token);

        return global::app.data.@this.Ok(buffer.ToArray());
    });

    public Task<data.@this> UploadAsync(upload action) => ExecuteHttpAsync(async () =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var app = action.Context.App;
        var config = app.Config.For<Config>(action.Context);

        var unsigned = ((await action.Unsigned.Value())?.Value ?? false) || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec == null ? config.Resolve("TimeoutInSec", 30)
            : (await action.TimeoutInSec.Value())?.ToDouble() ?? 0;
        if (timeout <= 0) timeout = config.Resolve("TimeoutInSec", 30);
        var encoding = (await action.Encoding.Value())?.Clr<string>() ?? config.Resolve("Encoding", "utf-8");

        var urlResult = ResolveUrl((await action.Url.Value())!.Clr<string>()!, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = (await urlResult.Value())!.Clr<string>()!;

        var headers = MergeHeaders(action.Headers == null ? null
            : global::app.type.item.@this.Lower<Dictionary<string, object>>(await action.Headers.Value()), config);

        var (httpContent, contentErr) = await ResolveUploadContentAsync(action, app, encoding);
        if (contentErr != null) return global::app.data.@this.FromError(contentErr);

        var httpMethod = ToSystemMethod((await action.Method.Value())!.Value);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, (action.SignOptions == null ? null : await action.SignOptions.Value()), resolvedUrl, httpMethod.Method);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseContentRead, config, cts.Token);

        var maxResponseSize = config.Resolve("MaxResponseSize", DefaultMaxResponseSize);
        return await ParseResponseAsync(response, requestMessage, unsigned, app, action.Context, maxResponseSize, sw.Elapsed);
    });

    public data.@this Configure(configure action)
    {
        // Redirect config can't change after first request (SocketsHttpHandler is immutable)
        if (_client != null && (action.FollowRedirects?.Peek() is { IsNull: false } || action.MaxRedirects?.Peek() is { IsNull: false }))
            return global::app.data.@this.FromError(new ServiceError(
                "Cannot change FollowRedirects/MaxRedirects after first HTTP request",
                "ConfigLocked", 409));

        action.Context.App.Config.Apply<Config>(action, action.Context, (action.Default.Peek() as global::app.type.@bool.@this)?.Value ?? false);
        return global::app.data.@this.Ok();
    }

    // --- Unified error handling ---

    private async Task<data.@this> ExecuteHttpAsync(Func<Task<data.@this>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException
            or IOException or UnauthorizedAccessException or FormatException)
        {
            var (key, statusCode) = ex switch
            {
                TaskCanceledException => ("Timeout", 408),
                HttpRequestException hre => ("HttpError", (int)(hre.StatusCode ?? 0)),
                IOException or UnauthorizedAccessException => ("IOError", 500),
                FormatException => ("InvalidContent", 400),
                _ => ("HttpError", 500)
            };
            return global::app.data.@this.FromError(new ServiceError(ex.Message, key, statusCode));
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
    private static async Task<data.@this<global::app.type.binary.@this>> ReadLimitedBytesAsync(
        HttpContent content, long maxBytes, CancellationToken ct = default)
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
                return data.@this<global::app.type.binary.@this>.FromError(new ServiceError(
                    $"Response body exceeds maximum size of {FormatBytes(maxBytes)}",
                    "ResponseTooLarge", 413));
            limited.Write(buffer, 0, bytesRead);

            throughputBytes += bytesRead;
            var elapsed = (DateTimeOffset.UtcNow - throughputStart).TotalSeconds;
            if (elapsed >= 30)
            {
                if (throughputBytes / elapsed < 1024)
                    return data.@this<global::app.type.binary.@this>.FromError(new ServiceError(
                        "Response too slow — possible slow-loris attack",
                        "SlowResponse", 408));
                throughputStart = DateTimeOffset.UtcNow;
                throughputBytes = 0;
            }
        }

        return data.@this<global::app.type.binary.@this>.Ok(limited.ToArray());
    }

    /// <summary>
    /// Reads HTTP content as UTF-8 string with a byte size limit. Thin wrapper
    /// over <see cref="ReadLimitedBytesAsync"/> — size-cap / slow-loris logic
    /// lives in one place.
    /// </summary>
    private static async Task<data.@this<global::app.type.text.@this>> ReadLimitedStringAsync(
        HttpContent content, long maxBytes, CancellationToken ct = default)
    {
        var bytes = await ReadLimitedBytesAsync(content, maxBytes, ct);
        if (!bytes.Success) return data.@this<global::app.type.text.@this>.FromError(bytes.Error!);
        return data.@this<global::app.type.text.@this>.Ok(Encoding.UTF8.GetString((await bytes.Value())!.Clr<byte[]>()!));
    }

    // --- Internal HTTP transport ---

    private Task<HttpResponseMessage> SendHttpAsync(
        HttpRequestMessage request, HttpCompletionOption completionOption,
        ModuleView<Config> config, CancellationToken ct)
    {
        _client ??= CreateClient(config);
        return _client.SendAsync(request, completionOption, ct);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    private HttpClient CreateClient(ModuleView<Config> config) => _handler != null
        ? new HttpClient(_handler, disposeHandler: false)
        : new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AllowAutoRedirect = config.Resolve("FollowRedirects", true),
            MaxAutomaticRedirections = config.Resolve("MaxRedirects", 10)
        });

    // --- Signing ---

    /// <summary>
    /// Signs a request via app.RunAction&lt;sign&gt;().
    /// Returns null if unsigned, the sign result Data on success (navigate .Signature for Signature).
    /// </summary>
    private static async Task<data.@this?> SignRequestAsync(
        actor.context.@this context,
        bool unsigned,
        signing.sign? signOptions,
        string url,
        string method)
    {
        if (unsigned) return null;

        // Body is intentionally excluded from the request signature for now — the
        // signing scheme is being reworked; signing url+method+headers keeps the
        // path working without forcing the body to materialize as a string here.
        var httpSign = new signing.sign
        {
            Context = context,
            Data = new data.@this("", ""),
            Headers = new data.@this<global::app.type.dict.@this>("", global::app.type.dict.@this.FromRaw(new Dictionary<string, object>
            {
                ["url"] = url,
                ["method"] = method
            }, context)),
            Contracts = signOptions?.Contracts,
            Expires = signOptions?.Expires
        };

        return await context.App.RunAction<signing.sign>(httpSign, context);
    }

    private void ApplySignature(HttpRequestMessage request, data.@this signResult)
    {
        var signatureJson = JsonSerializer.Serialize(signResult.Signature, _caseInsensitiveRead);
        request.Headers.TryAddWithoutValidation("X-Signature", signatureJson);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/plang"));
    }

    // --- Header helpers ---

    private static Dictionary<string, string> MergeHeaders(
        Dictionary<string, object>? stepHeaders,
        ModuleView<Config> config)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var defaults = config.Resolve<Dictionary<string, object>?>("DefaultHeaders", null);
        if (defaults != null)
        {
            foreach (var kvp in defaults)
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
                request.Content?.Headers.TryAddWithoutValidation(kvp.Key, value);
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

    // --- URL resolution ---

    private static data.@this<global::app.type.text.@this> ResolveUrl(string url, ModuleView<Config> config)
    {
        var baseUrl = config.Resolve<string?>("BaseUrl", null);

        if (url.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(baseUrl))
                return data.@this<global::app.type.text.@this>.FromError(new ServiceError(
                    "Relative URL requires a BaseUrl configuration. Use 'configure http, base url https://...'",
                    "NoBaseUrl", 400));

            baseUrl = baseUrl.TrimEnd('/');
            return data.@this<global::app.type.text.@this>.Ok(baseUrl + url);
        }

        if (!url.Contains("://"))
            url = "https://" + url;

        // Security: only allow http/https schemes (blocks file://, gopher://, etc.)
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme != "http" && uri.Scheme != "https")
                return data.@this<global::app.type.text.@this>.FromError(new ServiceError(
                    $"Only http:// and https:// URLs are allowed, got {uri.Scheme}://",
                    "InvalidUrlScheme", 400));
        }

        return data.@this<global::app.type.text.@this>.Ok(url);
    }

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
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            var (errorData, errorBody) = await ReadErrorResponseAsync(response, request);

            if (!unsigned && !string.IsNullOrEmpty(errorBody))
            {
                try { await TryExtractSignedErrorIdentity(errorBody, app, context); }
                catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best effort — don't mask the original error */ }
            }

            return errorData;
        }

        // application/plang response — keeps the historic shape (deserialized
        // Data flows through); no Response wrapping here.
        if (contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase))
        {
            if (unsigned)
            {
                var err = global::app.data.@this.FromError(new ServiceError(
                    "Unsigned request received application/plang response — this is not allowed",
                    "UnsignedPlang", 403));
                BuildProperties(err, request, response);
                return err;
            }

            return await ParsePlangResponseAsync(response, request, app, context, maxResponseSize);
        }

        // The response body enters through the one channel boundary: the http
        // channel stamps {type, kind} from Content-Type and produces LAZY Data —
        // the body is NOT deserialized at read time, it materializes on first
        // touch (navigation / As<T>) through the reader registry. A status check
        // (%response!status%) reads a Property and never touches the body.
        var bytesRead = await ReadLimitedBytesAsync(response.Content, maxResponseSize);
        if (!bytesRead.Success)
        {
            BuildProperties(bytesRead, request, response);
            return bytesRead;
        }

        var channel = new global::app.channel.type.http.@this(contentType, (await bytesRead.Value())!.Clr<byte[]>()!, context);
        var result = await channel.Read();
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
    /// Parses application/plang response: deserialize as data.@this (with Signature via [In]),
    /// verify signature, set %!ServiceIdentity%.
    ///
    /// **Why not Serializers.GetByContentType?** application/plang is the engine's
    /// own transport — `_transportInOptions` carries the [In] signature-inflow
    /// shape that the generic JSON serializer doesn't know about. Routing
    /// through the registry would lose Signature parsing; this raw
    /// `JsonSerializer.Deserialize&lt;data.@this&gt;` is intentional. Same rationale
    /// applies to <c>StreamPlangAsync</c>'s per-line NDJSON deserialize.
    /// </summary>
    private async Task<data.@this> ParsePlangResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        AppType app,
        actor.context.@this context,
        long maxResponseSize = DefaultMaxResponseSize)
    {
        var bodyRead = await ReadLimitedStringAsync(response.Content, maxResponseSize);
        if (!bodyRead.Success)
        {
            BuildProperties(bodyRead, request, response);
            return bodyRead;
        }
        var body = (await bodyRead.Value())!.Clr<string>()!;

        data.@this? data;
        try
        {
            data = JsonSerializer.Deserialize<data.@this>(body, _transportInOptions);
        }
        catch (JsonException ex)
        {
            var err = global::app.data.@this.FromError(new ServiceError(
                $"Failed to deserialize application/plang response: {ex.Message}",
                "PlangDeserializeError", 400));
            BuildProperties(err, request, response);
            return err;
        }

        if (data == null)
        {
            var err = global::app.data.@this.FromError(new ServiceError(
                "application/plang response deserialized to null",
                "PlangDeserializeError", 400));
            BuildProperties(err, request, response);
            return err;
        }

        // Data.Signature is populated from the wire via [In] — pass straight to verify
        var verifyAction = new signing.verify
        {
            Context = context,
            Data = data
        };

        var verifyResult = await app.RunAction<signing.verify>(verifyAction, context);
        if (!verifyResult.Success)
        {
            BuildProperties(verifyResult, request, response);
            return verifyResult;
        }

        await context.Variable.Set("!ServiceIdentity", data.Signature?.Identity);

        BuildProperties(data, request, response);
        return data;
    }

    /// <summary>
    /// Tries to extract identity from a signed error response body.
    /// The error body may be a Data with Signature, or have a "signature" field.
    /// </summary>
    private async Task TryExtractSignedErrorIdentity(
        string errorBody, AppType app, actor.context.@this context)
    {
        // Try deserializing as data.@this with transport options (may have Signature via [In])
        data.@this? data = null;
        try { data = JsonSerializer.Deserialize<data.@this>(errorBody, _transportInOptions); }
        catch (JsonException) { /* not valid data.@this JSON — try legacy format below */ }

        if (data?.Signature != null)
        {
            var verifyAction = new signing.verify { Context = context, Data = data };
            var verifyResult = await app.RunAction<signing.verify>(verifyAction, context);
            if (verifyResult.Success)
                await context.Variable.Set("!ServiceIdentity", data.Signature.Identity);
            return;
        }

        // Legacy: look for a "signature" field in arbitrary JSON
        using var doc = JsonDocument.Parse(errorBody);
        if (!doc.RootElement.TryGetProperty("signature", out var sigElement))
            return;

        var signedData = JsonSerializer.Deserialize<Signature>(sigElement.GetRawText(),
            _caseInsensitiveRead);
        if (signedData == null) return;

        var legacyData = new data.@this("");
        legacyData.Signature = signedData;

        var legacyVerify = new signing.verify { Context = context, Data = legacyData };
        var legacyResult = await app.RunAction<signing.verify>(legacyVerify, context);
        if (legacyResult.Success)
            await context.Variable.Set("!ServiceIdentity", signedData.Identity);
    }

    /// <summary>
    /// Reads an error HTTP response and builds a Data error with properties.
    /// Returns the error Data and the raw error body (for signed error extraction).
    /// </summary>
    private static async Task<(data.@this Error, string Body)> ReadErrorResponseAsync(
        HttpResponseMessage response, HttpRequestMessage request, CancellationToken ct = default)
    {
        var errorBody = "";
        try
        {
            var read = await ReadLimitedStringAsync(response.Content, MaxErrorBodySize, ct);
            // Best effort: ignore size-cap / slow-loris failures here, proceed with empty body.
            if (read.Success) errorBody = (await read.Value())!.Clr<string>()!;
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best effort — read failed (network/IO), proceed with empty */ }
        var err = global::app.data.@this.FromError(new ServiceError(
            $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}".Trim(),
            "HttpError", (int)response.StatusCode));
        BuildProperties(err, request, response);
        return (err, errorBody);
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

        props["StatusCode"] = (int)response.StatusCode;
        // `status` is the numeric code (the architect's %response!status% == 200);
        // the human reason phrase rides as `reason`.
        props["Status"] = (int)response.StatusCode;
        props["Reason"] = response.ReasonPhrase;
        props["IsSuccess"] = response.IsSuccessStatusCode;

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
        GoalCall onStream,
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
                var (err, _) = await ReadErrorResponseAsync(response, request, ct);
                return err;
            }
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var format = streamAs ?? DetectStreamFormat(contentType);

        var isPlang = contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase);
        if (isPlang && unsigned)
        {
            using (response)
            {
                var err = global::app.data.@this.FromError(new ServiceError(
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

            var result = global::app.data.@this.Ok();
            BuildProperties(result, request, response);
            return result;
        }
    }

    /// <summary>
    /// Creates a new GoalCall with the given value injected as the callback parameter, then runs it.
    /// Parameter name comes from the template GoalCall's first parameter, or the defaultName.
    /// </summary>
    private static async Task RunCallbackAsync(
        GoalCall template, object? value, PlangType? type, string defaultName,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        var paramName = template.Parameters?.Count > 0 ? template.Parameters[0].Name : defaultName;
        // A Data payload rides AS the parameter (parameters are Data boxes) —
        // re-boxing would nest a bare Data, which the store seam rejects.
        data.@this param;
        if (value is data.@this dv) { dv.Name = paramName; param = dv; }
        else param = new data.@this(paramName, value, type);
        var call = new GoalCall
        {
            Name = template.Name,
            PrPath = template.PrPath,
            Parameters = new List<data.@this> { param }
        };
        var result = await app.RunGoalAsync(call, context, ct);
        if (!result.Success)
            await app.System.Channel.WriteTextAsync(global::app.channel.list.@this.Error, result.Error?.Message ?? "");
    }

    private static StreamFormat DetectStreamFormat(string contentType)
    {
        if (contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
            return StreamFormat.SSE;
        return StreamFormat.Line;
    }

    private static async Task StreamLinesAsync(
        Stream stream, GoalCall onStream,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            await RunCallbackAsync(onStream, line, PlangType.String, "chunk", app, context, ct);
        }
    }

    private static async Task StreamSSEAsync(
        Stream stream, GoalCall onStream,
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
                    await RunCallbackAsync(onStream, dataBuffer.ToString(), PlangType.String, "chunk", app, context, ct);
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
                        throw new InvalidOperationException(
                            $"SSE stream disconnected after {maxConsecutiveOverflows} consecutive buffer overflows — possible attack");

                    await app.System.Channel.WriteAsync(global::app.channel.list.@this.Error,
                        global::app.data.@this.FromError(new ServiceError(
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
                await RunCallbackAsync(onStream, dataBuffer.ToString(), PlangType.String, "chunk", app, context, ct);
                dataBuffer.Clear();
            }
        }
    }

    private static async Task StreamBytesAsync(
        Stream stream, GoalCall onStream,
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
        Stream stream, GoalCall onStream,
        AppType app, actor.context.@this context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            // Each NDJSON line is a data object with Signature populated via [In]
            data.@this? data;
            try
            {
                data = JsonSerializer.Deserialize<data.@this>(line, _transportInOptions);
            }
            catch (JsonException)
            {
                await app.System.Channel.WriteAsync(global::app.channel.list.@this.Error,
                    global::app.data.@this.FromError(new ServiceError("Malformed NDJSON line in application/plang stream", "PlangStreamError", 400)));
                continue;
            }
            if (data == null) continue;

            // Verify signature — pass Data straight to verify
            var verifyAction = new signing.verify
            {
                Context = context,
                Data = data
            };

            var verifyResult = await app.RunAction<signing.verify>(verifyAction, context);
            if (!verifyResult.Success)
            {
                await RunCallbackAsync(onStream, verifyResult, null, "chunk", app, context, ct);
                continue;
            }

            await context.Variable.Set("!ServiceIdentity", data.Signature?.Identity);
            await RunCallbackAsync(onStream, data, null, "chunk", app, context, ct);
        }
    }

    // --- Progress reporting ---

    private static async Task<long> StreamWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        long maxBytes,
        GoalCall? onProgress,
        AppType app,
        actor.context.@this context,
        CancellationToken ct)
    {
        var buffer = new byte[8192];
        long bytesTransferred = 0;
        var lastReport = DateTimeOffset.UtcNow;
        var throughputStart = DateTimeOffset.UtcNow;
        long throughputBytes = 0;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            bytesTransferred += bytesRead;

            // F1: size limit on file downloads
            if (bytesTransferred > maxBytes)
                throw new InvalidOperationException(
                    $"Download exceeds maximum size of {FormatBytes(maxBytes)}");

            await destination.WriteAsync(buffer, 0, bytesRead, ct);

            // F3: slow-loris throughput check
            throughputBytes += bytesRead;
            var elapsed = (DateTimeOffset.UtcNow - throughputStart).TotalSeconds;
            if (elapsed >= 30)
            {
                var bytesPerSec = throughputBytes / elapsed;
                if (bytesPerSec < 1024) // < 1KB/sec for 30s
                    throw new InvalidOperationException(
                        $"Transfer too slow ({bytesPerSec:F0} bytes/sec) — possible slow-loris attack");
                throughputStart = DateTimeOffset.UtcNow;
                throughputBytes = 0;
            }

            if (onProgress != null)
            {
                var now = DateTimeOffset.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 500)
                {
                    lastReport = now;
                    var progress = new TransferProgress
                    {
                        BytesTransferred = bytesTransferred,
                        TotalBytes = totalBytes,
                        Percentage = totalBytes > 0 ? (double)bytesTransferred / totalBytes.Value * 100 : null
                    };
                    await RunCallbackAsync(onProgress, progress, null, "progress", app, context, ct);
                }
            }
        }

        return bytesTransferred;
    }

    // --- Upload content resolution ---

    // HttpContent is a transport artifact, never a PLang value — it rides as a plain
    // (HttpContent?, error) tuple, not Data<HttpContent>.
    private static async Task<(HttpContent? Content, global::app.error.IError? Error)> ResolveUploadContentAsync(
        upload action, global::app.@this app, string encoding)
    {
        var content = await action.Content.Value();
        var context = action.Context;
        if ((action.As == null ? null : await action.As.Value()) is { } asChoice && asChoice is { } && (ContentAs?)asChoice is { } contentAs)
        {
            return contentAs switch
            {
                ContentAs.File => await CreateFileContentAsync(app, context, content!.ToString()!),
                ContentAs.Base64 => (CreateBase64Content(content!.ToString()!), (global::app.error.IError?)null),
                ContentAs.Form => await CreateFormContentAsync(app, context, content!),
                ContentAs.Text => (new StringContent(
                    content is global::app.type.text.@this ? content.ToString()! : JsonSerializer.Serialize(content),
                    Encoding.GetEncoding(encoding)), (global::app.error.IError?)null),
                _ => (new StringContent(content!.ToString()!, Encoding.GetEncoding(encoding)), (global::app.error.IError?)null)
            };
        }

        // Auto-detect
        if (content is global::app.type.dict.@this
            || content is global::app.type.item.clr { Value: Dictionary<string, object> or JsonElement { ValueKind: JsonValueKind.Object } })
        {
            return await CreateFormContentAsync(app, context, content);
        }

        if (content is global::app.type.text.@this)
        {
            var str = content.ToString()!;
            // Try as file path — gated through path.ExistsAsync (AuthGate(Read)).
            // Out-of-root probes prompt or deny; in-root fast-passes. Any failure
            // (including denial) falls through to "treat as a string body" —
            // matches the prior "if not a file, send as string" shape.
            var p = global::app.type.path.@this.Resolve(str, context);
            var exists = await p.ExistsAsync();
            if (exists.Success && await exists.ToBooleanAsync())
                return await CreateFileContentAsync(app, context, str);

            return (new StringContent(str, Encoding.GetEncoding(encoding)), null);
        }

        return (new StringContent(
            JsonSerializer.Serialize(content),
            Encoding.GetEncoding(encoding),
            "application/json"), null);
    }

    // internal so HttpStaticFileDenialTests can invoke the handler's read
    // path directly (driving the full upload action requires a real HTTP
    // endpoint).
    internal static async Task<(HttpContent? Content, global::app.error.IError? Error)> CreateFileContentAsync(global::app.@this app, actor.context.@this context, string path)
    {
        // Gated read via path verb. AuthGate(Read) fires inside ReadBytes;
        // out-of-root paths the actor hasn't granted bubble up as Fail.
        var resolved = global::app.type.path.@this.Resolve(path, context);
        var read = await resolved.ReadBytes();
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

    private static async Task<(HttpContent? Content, global::app.error.IError? Error)> CreateFormContentAsync(global::app.@this app, actor.context.@this context, object content)
    {
        var form = new MultipartFormDataContent();
        Dictionary<string, object> fields;

        if (content is Dictionary<string, object> dict)
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
                // Gated read via path verb. AuthGate(Read) fires; out-of-root
                // form fields the actor hasn't authorized get denied at the
                // gate, not silently exfiltrated.
                var fp = global::app.type.path.@this.Resolve(value[1..], context);
                var read = await fp.ReadBytes();
                if (!read.Success || await read.Value() == null)
                    return (null, read.Error
                        ?? new ServiceError($"Could not read form file: {value[1..]}", "FileReadError", 500));
                var fileContent = new ByteArrayContent((await read.Value())!.Clr<byte[]>()!);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, kvp.Key, fp.FileName);
            }
            else
            {
                form.Add(new StringContent(value), kvp.Key);
            }
        }

        return (form, null);
    }

    // --- Static utilities ---

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024 * 1024)}MB",
        >= 1024 => $"{bytes / 1024}KB",
        _ => $"{bytes} bytes"
    };

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
