using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app.channels.serializers;
using app.actor.context;
using app.errors;
using app.goals.goal;
using app.variables;
using PlangType = app.data.type;
using app.config;
using app.modules.signing;
using AppType = app.@this;
using SysHttpMethod = System.Net.Http.HttpMethod;

namespace app.modules.http.code;

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
            Modifiers = { global::app.channels.serializers.filters.Transport.ForInbound }
        }
    };

    // Case-insensitive read for HTTP responses (signing, JSON body parsing, plang).
    // Stage 27 disperse-from-Json target — was Utils.Json.CaseInsensitiveRead.
    private readonly JsonSerializerOptions _caseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true), new app.data.EmptyStringToNullEnumConverterFactory(), new global::app.channels.serializers.TimeSpanIso8601() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // --- IHttp: action-level methods ---

    public Task<data.@this> SendAsync(request action) => ExecuteHttpAsync(async () =>
    {
        var app = action.Context.App;
        var config = app.Config.For<Config>(action.Context);

        var unsigned = action.Unsigned.Value || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec.Value > 0 ? action.TimeoutInSec.Value : config.Resolve("TimeoutInSec", 30);
        var contentType = action.ContentType.Value ?? config.Resolve("ContentType", "application/json");
        var encoding = action.Encoding.Value ?? config.Resolve("Encoding", "utf-8");

        var urlResult = ResolveUrl(action.Url.Value!, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = MergeHeaders(action.Headers?.Value, config);

        // Build body
        HttpContent? httpContent = null;
        string? bodyString = null;
        if (action.Body?.Value != null)
        {
            if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && action.Body.Value is Dictionary<string, object> formDict)
            {
                var formValues = new Dictionary<string, string>();
                foreach (var kvp in formDict)
                    formValues[kvp.Key] = kvp.Value?.ToString() ?? "";
                httpContent = new FormUrlEncodedContent(formValues);
            }
            else
            {
                bodyString = action.Body.Value is string s ? s : JsonSerializer.Serialize(action.Body.Value);
                var enc = Encoding.GetEncoding(encoding);
                httpContent = new StringContent(bodyString, enc, contentType);
            }
        }

        var httpMethod = ToSystemMethod(action.Method.Value);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, action.SignOptions?.Value, bodyString, resolvedUrl, httpMethod.Method);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        var completionOption = action.OnStream?.Value != null
            ? HttpCompletionOption.ResponseHeadersRead
            : HttpCompletionOption.ResponseContentRead;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        var response = await SendHttpAsync(requestMessage, completionOption, config, cts.Token);

        if (action.OnStream?.Value != null)
        {
            var maxSSEBuffer = config.Resolve("MaxSSEBufferSize", 10L * 1024 * 1024);
            return await HandleStreamingAsync(
                response, requestMessage, action.OnStream.Value, action.StreamAs?.Value,
                unsigned, app, action.Context, maxSSEBuffer, cts.Token);
        }

        var maxResponseSize = config.Resolve("MaxResponseSize", DefaultMaxResponseSize);

        using (response)
        {
            return await ParseResponseAsync(response, requestMessage, unsigned, app, action.Context, maxResponseSize);
        }
    });

    public Task<data.@this> DownloadAsync(download action) => ExecuteHttpAsync(async () =>
    {
        var app = action.Context.App;
        var config = app.Config.For<Config>(action.Context);

        var unsigned = action.Unsigned.Value || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec.Value > 0 ? action.TimeoutInSec.Value : config.Resolve("TimeoutInSec", 30);
        var urlResult = ResolveUrl(action.Url.Value!, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = MergeHeaders(action.Headers?.Value, config);
        var requestMessage = new HttpRequestMessage(SysHttpMethod.Get, resolvedUrl);
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, action.SignOptions?.Value, null, resolvedUrl, "GET");
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
            responseStream, buffer, totalBytes, maxDownloadSize, action.OnProgress?.Value, app, action.Context, cts.Token);

        return global::app.data.@this.Ok(buffer.ToArray());
    });

    public Task<data.@this> UploadAsync(upload action) => ExecuteHttpAsync(async () =>
    {
        var app = action.Context.App;
        var config = app.Config.For<Config>(action.Context);

        var unsigned = action.Unsigned.Value || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec.Value > 0 ? action.TimeoutInSec.Value : config.Resolve("TimeoutInSec", 30);
        var encoding = action.Encoding.Value ?? config.Resolve("Encoding", "utf-8");

        var urlResult = ResolveUrl(action.Url.Value!, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = MergeHeaders(action.Headers?.Value, config);

        var httpContent = await ResolveUploadContentAsync(action, app, encoding);

        var httpMethod = ToSystemMethod(action.Method.Value);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        string? bodyString = null;
        if (httpContent is StringContent sc)
            bodyString = await sc.ReadAsStringAsync();

        var signResult = await SignRequestAsync(action.Context, unsigned, action.SignOptions?.Value, bodyString, resolvedUrl, httpMethod.Method);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseContentRead, config, cts.Token);

        var maxResponseSize = config.Resolve("MaxResponseSize", DefaultMaxResponseSize);
        return await ParseResponseAsync(response, requestMessage, unsigned, app, action.Context, maxResponseSize);
    });

    public data.@this Configure(configure action)
    {
        // Redirect config can't change after first request (SocketsHttpHandler is immutable)
        if (_client != null && (action.FollowRedirects?.Value != null || action.MaxRedirects?.Value != null))
            return global::app.data.@this.FromError(new ServiceError(
                "Cannot change FollowRedirects/MaxRedirects after first HTTP request",
                "ConfigLocked", 409));

        action.Context.App.Config.Apply<Config>(action, action.Context, action.Default.Value);
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
            or IOException or UnauthorizedAccessException or FormatException
            or InvalidOperationException)
        {
            var (key, statusCode) = ex switch
            {
                InvalidOperationException => ("ResponseTooLarge", 413),
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
    /// Reads HTTP content as string with a byte size limit.
    /// Protects against OOM from unbounded response bodies.
    /// </summary>
    private static async Task<string> ReadLimitedStringAsync(
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
                throw new InvalidOperationException(
                    $"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
            limited.Write(buffer, 0, bytesRead);

            // Slow-loris protection: abort if throughput drops below 1KB/sec for 30s
            throughputBytes += bytesRead;
            var elapsed = (DateTimeOffset.UtcNow - throughputStart).TotalSeconds;
            if (elapsed >= 30)
            {
                if (throughputBytes / elapsed < 1024)
                    throw new InvalidOperationException("Response too slow — possible slow-loris attack");
                throughputStart = DateTimeOffset.UtcNow;
                throughputBytes = 0;
            }
        }

        limited.Position = 0;
        using var reader = new StreamReader(limited, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    /// <summary>
    /// Reads HTTP content as byte array with a size limit.
    /// </summary>
    private static async Task<byte[]> ReadLimitedBytesAsync(
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
                throw new InvalidOperationException(
                    $"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
            limited.Write(buffer, 0, bytesRead);

            throughputBytes += bytesRead;
            var elapsed = (DateTimeOffset.UtcNow - throughputStart).TotalSeconds;
            if (elapsed >= 30)
            {
                if (throughputBytes / elapsed < 1024)
                    throw new InvalidOperationException("Response too slow — possible slow-loris attack");
                throughputStart = DateTimeOffset.UtcNow;
                throughputBytes = 0;
            }
        }

        return limited.ToArray();
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
        string? bodyContent,
        string url,
        string method)
    {
        if (unsigned) return null;

        var httpSign = new signing.sign
        {
            Context = context,
            Data = new data.@this("", bodyContent ?? ""),
            Headers = new data.@this<Dictionary<string, object>>("", new Dictionary<string, object>
            {
                ["url"] = url,
                ["method"] = method
            }),
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

    private static data.@this<string> ResolveUrl(string url, ModuleView<Config> config)
    {
        var baseUrl = config.Resolve<string?>("BaseUrl", null);

        if (url.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(baseUrl))
                return data.@this<string>.FromError(new ServiceError(
                    "Relative URL requires a BaseUrl configuration. Use 'configure http, base url https://...'",
                    "NoBaseUrl", 400));

            baseUrl = baseUrl.TrimEnd('/');
            return data.@this<string>.Ok(baseUrl + url);
        }

        if (!url.Contains("://"))
            url = "https://" + url;

        // Security: only allow http/https schemes (blocks file://, gopher://, etc.)
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme != "http" && uri.Scheme != "https")
                return data.@this<string>.FromError(new ServiceError(
                    $"Only http:// and https:// URLs are allowed, got {uri.Scheme}://",
                    "InvalidUrlScheme", 400));
        }

        return data.@this<string>.Ok(url);
    }

    // --- Response parsing ---

    private async Task<data.@this> ParseResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        bool unsigned,
        AppType app,
        actor.context.@this context,
        long maxResponseSize = DefaultMaxResponseSize)
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

        // application/plang response
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

        // JSON response
        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = await ReadLimitedStringAsync(response.Content, maxResponseSize);
            object? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<object>(json, _caseInsensitiveRead);
            }
            catch (JsonException)
            {
                parsed = json;
            }
            var result = global::app.data.@this.Ok(parsed);
            BuildProperties(result, request, response);
            return result;
        }

        // XML response
        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = await ReadLimitedStringAsync(response.Content, maxResponseSize);
            var result = global::app.data.@this.Ok(xml, data.type.FromMime("application/xml"));
            BuildProperties(result, request, response);
            return result;
        }

        // Text response
        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            var text = await ReadLimitedStringAsync(response.Content, maxResponseSize);
            var result = global::app.data.@this.Ok(text);
            BuildProperties(result, request, response);
            return result;
        }

        // Binary response
        var bytes = await ReadLimitedBytesAsync(response.Content, maxResponseSize);
        var binaryResult = global::app.data.@this.Ok(bytes);
        BuildProperties(binaryResult, request, response);
        return binaryResult;
    }

    /// <summary>
    /// Parses application/plang response: deserialize as data.@this (with Signature via [In]),
    /// verify signature, set %!ServiceIdentity%.
    /// </summary>
    private async Task<data.@this> ParsePlangResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        AppType app,
        actor.context.@this context,
        long maxResponseSize = DefaultMaxResponseSize)
    {
        var body = await ReadLimitedStringAsync(response.Content, maxResponseSize);

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

        context.Variables.Set("!ServiceIdentity", data.Signature?.Identity);

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
                context.Variables.Set("!ServiceIdentity", data.Signature.Identity);
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
            context.Variables.Set("!ServiceIdentity", signedData.Identity);
    }

    /// <summary>
    /// Reads an error HTTP response and builds a Data error with properties.
    /// Returns the error Data and the raw error body (for signed error extraction).
    /// </summary>
    private static async Task<(data.@this Error, string Body)> ReadErrorResponseAsync(
        HttpResponseMessage response, HttpRequestMessage request, CancellationToken ct = default)
    {
        var errorBody = "";
        try { errorBody = await ReadLimitedStringAsync(response.Content, MaxErrorBodySize, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best effort — body too large or read failed, proceed with empty */ }
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

        props.Add(new data.@this("Url", request.RequestUri?.ToString()));
        props.Add(new data.@this("Method", request.Method.Method));

        var reqHeaders = new Dictionary<string, string>();
        foreach (var h in request.Headers)
            reqHeaders[h.Key] = string.Join(", ", h.Value);
        props.Add(new data.@this("RequestHeaders", reqHeaders));

        if (request.Content != null)
        {
            props.Add(new data.@this("ContentType", request.Content.Headers.ContentType?.ToString()));
            props.Add(new data.@this("ContentLength", request.Content.Headers.ContentLength));
        }

        props.Add(new data.@this("StatusCode", (int)response.StatusCode));
        props.Add(new data.@this("Status", response.ReasonPhrase));
        props.Add(new data.@this("IsSuccess", response.IsSuccessStatusCode));

        var respHeaders = new Dictionary<string, string>();
        foreach (var h in response.Headers)
            respHeaders[h.Key] = string.Join(", ", h.Value);
        props.Add(new data.@this("Headers", respHeaders));

        var contentHeaders = new Dictionary<string, string>();
        foreach (var h in response.Content.Headers)
            contentHeaders[h.Key] = string.Join(", ", h.Value);
        props.Add(new data.@this("ContentHeaders", contentHeaders));

        if (response.Content.Headers.ContentType?.CharSet != null)
            props.Add(new data.@this("Charset", response.Content.Headers.ContentType.CharSet));
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
        var call = new GoalCall
        {
            Name = template.Name,
            PrPath = template.PrPath,
            Parameters = new List<data.@this> { new data.@this(paramName, value, type) }
        };
        var result = await app.RunGoalAsync(call, context, ct);
        if (!result.Success)
            await app.System.Channels.WriteTextAsync(AppChannels.Error, result.Error?.Message ?? "");
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

                    await app.System.Channels.WriteAsync(AppChannels.Error,
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
                await app.System.Channels.WriteAsync(AppChannels.Error,
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

            context.Variables.Set("!ServiceIdentity", data.Signature?.Identity);
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

    private static async Task<HttpContent> ResolveUploadContentAsync(
        upload action, global::app.@this app, string encoding)
    {
        var content = action.Content.Value;
        var ctx = action.Context;
        if (action.As?.Value is ContentAs contentAs)
        {
            return contentAs switch
            {
                ContentAs.File => await CreateFileContentAsync(app, ctx, content!.ToString()!),
                ContentAs.Base64 => CreateBase64Content(content!.ToString()!),
                ContentAs.Form => await CreateFormContentAsync(app, ctx, content!),
                ContentAs.Text => new StringContent(
                    content is string s ? s : JsonSerializer.Serialize(content),
                    Encoding.GetEncoding(encoding)),
                _ => new StringContent(content!.ToString()!, Encoding.GetEncoding(encoding))
            };
        }

        // Auto-detect
        if (content is Dictionary<string, object> ||
            content is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            return await CreateFormContentAsync(app, ctx, content);
        }

        if (content is string str)
        {
            // Try as file path — gated through path.ExistsAsync (AuthGate(Read)).
            // Out-of-root probes prompt or deny; in-root fast-passes. Any failure
            // (including denial) falls through to "treat as a string body" —
            // matches the prior "if not a file, send as string" shape.
            var p = global::app.types.path.@this.Resolve(str, ctx);
            var exists = await p.ExistsAsync();
            if (exists.Success && exists.Value == true)
                return await CreateFileContentAsync(app, ctx, str);

            return new StringContent(str, Encoding.GetEncoding(encoding));
        }

        return new StringContent(
            JsonSerializer.Serialize(content),
            Encoding.GetEncoding(encoding),
            "application/json");
    }

    private static async Task<HttpContent> CreateFileContentAsync(global::app.@this app, actor.context.@this context, string path)
    {
        // Gated read via path verb. AuthGate(Read) fires inside ReadBytes;
        // out-of-root paths the actor hasn't granted bubble up as Fail.
        var resolved = global::app.types.path.@this.Resolve(path, context);
        var read = await resolved.ReadBytes();
        if (!read.Success || read.Value == null)
            throw new System.IO.IOException(read.Error?.Message ?? $"Could not read file: {path}");
        var content = new ByteArrayContent(read.Value);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static HttpContent CreateBase64Content(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static async Task<HttpContent> CreateFormContentAsync(global::app.@this app, actor.context.@this context, object content)
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
                var fp = global::app.types.path.@this.Resolve(value[1..], context);
                var read = await fp.ReadBytes();
                if (!read.Success || read.Value == null)
                    throw new System.IO.IOException(read.Error?.Message ?? $"Could not read form file: {value[1..]}");
                var fileContent = new ByteArrayContent(read.Value);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, kvp.Key, fp.FileName);
            }
            else
            {
                form.Add(new StringContent(value), kvp.Key);
            }
        }

        return form;
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
