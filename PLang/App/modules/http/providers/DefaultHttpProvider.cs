using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using App.Channels.Serializers;
using App.Context;
using App.Errors;
using App.Goals.Goal;
using App.Variables;
using PlangType = App.Variables.Type;
using App.Config;
using App.modules.signing;
using EngineType = App.@this;
using SysHttpMethod = System.Net.Http.HttpMethod;

namespace App.modules.http.providers;

/// <summary>
/// Default HTTP provider. Owns all HTTP behavior — actions delegate to this via `this`.
/// Lazily creates HttpClient on first request. Swappable via engine.Providers.
/// </summary>
public sealed class DefaultHttpProvider : IHttpProvider
{
    public string Name { get; init; } = "default";
    public bool IsDefault { get; set; }

    private readonly HttpMessageHandler? _handler;
    private HttpClient? _client;

    public DefaultHttpProvider() { }

    /// <summary>
    /// Test constructor: injects a custom HttpMessageHandler.
    /// All real provider logic runs — only the HTTP transport is swapped.
    /// </summary>
    public DefaultHttpProvider(HttpMessageHandler handler) => _handler = handler;

    /// <summary>
    /// Transport JSON options: overrides [JsonIgnore] for [In] properties (e.g., Signature).
    /// Used when deserializing application/plang responses — Data arrives with Signature on the wire.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = App.Utility.Json.CaseInsensitiveRead;

    private static readonly JsonSerializerOptions _transportInOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { TransportPropertyFilter.ForInbound }
        }
    };

    // --- IHttpProvider: action-level methods ---

    public Task<Data> SendAsync(request action) => ExecuteHttpAsync(async () =>
    {
        var engine = action.Context.App;
        var config = engine.Config.For<Config>(action.Context);

        var unsigned = action.Unsigned || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec > 0 ? action.TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var contentType = action.ContentType ?? config.Resolve("ContentType", "application/json");
        var encoding = action.Encoding ?? config.Resolve("Encoding", "utf-8");

        var urlResult = ResolveUrl(action.Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = MergeHeaders(action.Headers, config);

        // Build body
        HttpContent? httpContent = null;
        string? bodyString = null;
        if (action.Body != null)
        {
            if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && action.Body is Dictionary<string, object> formDict)
            {
                var formValues = new Dictionary<string, string>();
                foreach (var kvp in formDict)
                    formValues[kvp.Key] = kvp.Value?.ToString() ?? "";
                httpContent = new FormUrlEncodedContent(formValues);
            }
            else
            {
                bodyString = action.Body is string s ? s : JsonSerializer.Serialize(action.Body);
                var enc = Encoding.GetEncoding(encoding);
                httpContent = new StringContent(bodyString, enc, contentType);
            }
        }

        var httpMethod = ToSystemMethod(action.Method);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, action.SignOptions, bodyString, resolvedUrl, httpMethod.Method);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        var completionOption = action.OnStream != null
            ? HttpCompletionOption.ResponseHeadersRead
            : HttpCompletionOption.ResponseContentRead;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(action.Context.App.ShutdownToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        var response = await SendHttpAsync(requestMessage, completionOption, config, cts.Token);

        if (action.OnStream != null)
        {
            var maxSSEBuffer = config.Resolve("MaxSSEBufferSize", 10L * 1024 * 1024);
            return await HandleStreamingAsync(
                response, requestMessage, action.OnStream, action.StreamAs,
                unsigned, engine, action.Context, maxSSEBuffer, cts.Token);
        }

        var maxResponseSize = config.Resolve("MaxResponseSize", DefaultMaxResponseSize);

        using (response)
        {
            return await ParseResponseAsync(response, requestMessage, unsigned, engine, action.Context, maxResponseSize);
        }
    });

    public Task<Data> DownloadAsync(download action) => ExecuteHttpAsync(async () =>
    {
        var engine = action.Context.App;
        var config = engine.Config.For<Config>(action.Context);
        var fs = engine.FileSystem;

        var unsigned = action.Unsigned || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec > 0 ? action.TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var urlResult = ResolveUrl(action.Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var savePath = fs.ValidatePath(action.SaveTo);

        // File existence check
        if (fs.File.Exists(savePath))
        {
            switch (action.IfExists)
            {
                case FileExists.Error:
                    return Data.FromError(new ServiceError(
                        $"File already exists: {action.SaveTo}", "FileExists", 409));
                case FileExists.Skip:
                    return Data.Ok(action.SaveTo);
                case FileExists.Overwrite:
                    break;
            }
        }

        var headers = MergeHeaders(action.Headers, config);
        var requestMessage = new HttpRequestMessage(SysHttpMethod.Get, resolvedUrl);
        ApplyHeaders(requestMessage, headers);

        var signResult = await SignRequestAsync(action.Context, unsigned, action.SignOptions, null, resolvedUrl, "GET");
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, config, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var (err, _) = await ReadErrorResponseAsync(response, requestMessage, ct: cts.Token);
            return err;
        }

        var dir = fs.Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
            fs.Directory.CreateDirectory(dir);

        var totalBytes = response.Content.Headers.ContentLength;
        using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var fileStream = fs.File.Create(savePath);

        await StreamWithProgressAsync(
            responseStream, fileStream, totalBytes, action.OnProgress, engine, action.Context, cts.Token);

        return Data.Ok(action.SaveTo);
    });

    public Task<Data> UploadAsync(upload action) => ExecuteHttpAsync(async () =>
    {
        var engine = action.Context.App;
        var config = engine.Config.For<Config>(action.Context);
        var fs = engine.FileSystem;

        var unsigned = action.Unsigned || config.Resolve("Unsigned", false);
        var timeout = action.TimeoutInSec > 0 ? action.TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var encoding = action.Encoding ?? config.Resolve("Encoding", "utf-8");

        var urlResult = ResolveUrl(action.Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = MergeHeaders(action.Headers, config);

        var httpContent = await ResolveUploadContentAsync(action, fs, encoding);

        var httpMethod = ToSystemMethod(action.Method);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        ApplyHeaders(requestMessage, headers);

        string? bodyString = null;
        if (httpContent is StringContent sc)
            bodyString = await sc.ReadAsStringAsync();

        var signResult = await SignRequestAsync(action.Context, unsigned, action.SignOptions, bodyString, resolvedUrl, httpMethod.Method);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            ApplySignature(requestMessage, signResult);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseContentRead, config, cts.Token);

        var maxResponseSize = config.Resolve("MaxResponseSize", DefaultMaxResponseSize);
        return await ParseResponseAsync(response, requestMessage, unsigned, engine, action.Context, maxResponseSize);
    });

    public Data Configure(configure action)
    {
        // Redirect config can't change after first request (SocketsHttpHandler is immutable)
        if (_client != null && (action.FollowRedirects.HasValue || action.MaxRedirects.HasValue))
            return Data.FromError(new ServiceError(
                "Cannot change FollowRedirects/MaxRedirects after first HTTP request",
                "ConfigLocked", 409));

        action.Context.App.Config.Apply<Config>(action, action.Context, action.Default);
        return Data.Ok();
    }

    // --- Unified error handling ---

    private async Task<Data> ExecuteHttpAsync(Func<Task<Data>> operation)
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
            return Data.FromError(new ServiceError(ex.Message, key, statusCode));
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

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxBytes)
                throw new InvalidOperationException(
                    $"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
            limited.Write(buffer, 0, bytesRead);
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

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxBytes)
                throw new InvalidOperationException(
                    $"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
            limited.Write(buffer, 0, bytesRead);
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
    /// Signs a request via engine.RunAction&lt;sign&gt;().
    /// Returns null if unsigned, the sign result Data on success (navigate .Signature for SignedData).
    /// </summary>
    private static async Task<Data?> SignRequestAsync(
        Context.@this context,
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
            Data = new Data("", bodyContent ?? ""),
            Headers = new Dictionary<string, object>
            {
                ["url"] = url,
                ["method"] = method
            },
            Contracts = signOptions?.Contracts,
            ExpiresInMs = signOptions?.ExpiresInMs
        };

        return await context.App.RunAction<signing.sign>(httpSign, context);
    }

    private static void ApplySignature(HttpRequestMessage request, Data signResult)
    {
        var signatureJson = JsonSerializer.Serialize(signResult.Signature, _jsonOptions);
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
            if (IsContentHeader(kvp.Key))
                request.Content?.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            else
                request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
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

    private static Data<string> ResolveUrl(string url, ModuleView<Config> config)
    {
        var baseUrl = config.Resolve<string?>("BaseUrl", null);

        if (url.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(baseUrl))
                return Data<string>.FromError(new ServiceError(
                    "Relative URL requires a BaseUrl configuration. Use 'configure http, base url https://...'",
                    "NoBaseUrl", 400));

            baseUrl = baseUrl.TrimEnd('/');
            return Data<string>.Ok(baseUrl + url);
        }

        if (!url.Contains("://"))
            url = "https://" + url;

        return Data<string>.Ok(url);
    }

    // --- Response parsing ---

    private static async Task<Data> ParseResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        bool unsigned,
        EngineType engine,
        Context.@this context,
        long maxResponseSize = DefaultMaxResponseSize)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            var (errorData, errorBody) = await ReadErrorResponseAsync(response, request);

            if (!unsigned && !string.IsNullOrEmpty(errorBody))
            {
                try { await TryExtractSignedErrorIdentity(errorBody, engine, context); }
                catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best effort — don't mask the original error */ }
            }

            return errorData;
        }

        // application/plang response
        if (contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase))
        {
            if (unsigned)
            {
                var err = Data.FromError(new ServiceError(
                    "Unsigned request received application/plang response — this is not allowed",
                    "UnsignedPlang", 403));
                BuildProperties(err, request, response);
                return err;
            }

            return await ParsePlangResponseAsync(response, request, engine, context, maxResponseSize);
        }

        // JSON response
        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = await ReadLimitedStringAsync(response.Content, maxResponseSize);
            object? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<object>(json, _jsonOptions);
            }
            catch (JsonException)
            {
                parsed = json;
            }
            var result = Data.Ok(parsed);
            BuildProperties(result, request, response);
            return result;
        }

        // XML response
        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = await ReadLimitedStringAsync(response.Content, maxResponseSize);
            var result = Data.Ok(xml, Variables.Type.FromMime("application/xml"));
            BuildProperties(result, request, response);
            return result;
        }

        // Text response
        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            var text = await ReadLimitedStringAsync(response.Content, maxResponseSize);
            var result = Data.Ok(text);
            BuildProperties(result, request, response);
            return result;
        }

        // Binary response
        var bytes = await ReadLimitedBytesAsync(response.Content, maxResponseSize);
        var binaryResult = Data.Ok(bytes);
        BuildProperties(binaryResult, request, response);
        return binaryResult;
    }

    /// <summary>
    /// Parses application/plang response: deserialize as Data (with Signature via [In]),
    /// verify signature, set %!ServiceIdentity%.
    /// </summary>
    private static async Task<Data> ParsePlangResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        EngineType engine,
        Context.@this context,
        long maxResponseSize = DefaultMaxResponseSize)
    {
        var body = await ReadLimitedStringAsync(response.Content, maxResponseSize);

        Data? data;
        try
        {
            data = JsonSerializer.Deserialize<Data>(body, _transportInOptions);
        }
        catch (JsonException ex)
        {
            var err = Data.FromError(new ServiceError(
                $"Failed to deserialize application/plang response: {ex.Message}",
                "PlangDeserializeError", 400));
            BuildProperties(err, request, response);
            return err;
        }

        if (data == null)
        {
            var err = Data.FromError(new ServiceError(
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

        var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
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
    private static async Task TryExtractSignedErrorIdentity(
        string errorBody, EngineType engine, Context.@this context)
    {
        // Try deserializing as Data with transport options (may have Signature via [In])
        Data? data = null;
        try { data = JsonSerializer.Deserialize<Data>(errorBody, _transportInOptions); }
        catch (JsonException) { /* not valid Data JSON — try legacy format below */ }

        if (data?.Signature != null)
        {
            var verifyAction = new signing.verify { Context = context, Data = data };
            var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
            if (verifyResult.Success)
                context.Variables.Set("!ServiceIdentity", data.Signature.Identity);
            return;
        }

        // Legacy: look for a "signature" field in arbitrary JSON
        using var doc = JsonDocument.Parse(errorBody);
        if (!doc.RootElement.TryGetProperty("signature", out var sigElement))
            return;

        var signedData = JsonSerializer.Deserialize<SignedData>(sigElement.GetRawText(),
            App.Utility.Json.CaseInsensitiveRead);
        if (signedData == null) return;

        var legacyData = new Data("");
        legacyData.Signature = signedData;

        var legacyVerify = new signing.verify { Context = context, Data = legacyData };
        var legacyResult = await engine.RunAction<signing.verify>(legacyVerify, context);
        if (legacyResult.Success)
            context.Variables.Set("!ServiceIdentity", signedData.Identity);
    }

    /// <summary>
    /// Reads an error HTTP response and builds a Data error with properties.
    /// Returns the error Data and the raw error body (for signed error extraction).
    /// </summary>
    private static async Task<(Data Error, string Body)> ReadErrorResponseAsync(
        HttpResponseMessage response, HttpRequestMessage request, CancellationToken ct = default)
    {
        var errorBody = "";
        try { errorBody = await ReadLimitedStringAsync(response.Content, MaxErrorBodySize, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best effort — body too large or read failed, proceed with empty */ }
        var err = Data.FromError(new ServiceError(
            $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}".Trim(),
            "HttpError", (int)response.StatusCode));
        BuildProperties(err, request, response);
        return (err, errorBody);
    }

    // --- Response metadata ---

    private static void BuildProperties(Data data, HttpRequestMessage request, HttpResponseMessage response)
    {
        var props = data.Properties;

        props.Add(new Data("Url", request.RequestUri?.ToString()));
        props.Add(new Data("Method", request.Method.Method));

        var reqHeaders = new Dictionary<string, string>();
        foreach (var h in request.Headers)
            reqHeaders[h.Key] = string.Join(", ", h.Value);
        props.Add(new Data("RequestHeaders", reqHeaders));

        if (request.Content != null)
        {
            props.Add(new Data("ContentType", request.Content.Headers.ContentType?.ToString()));
            props.Add(new Data("ContentLength", request.Content.Headers.ContentLength));
        }

        props.Add(new Data("StatusCode", (int)response.StatusCode));
        props.Add(new Data("Status", response.ReasonPhrase));
        props.Add(new Data("IsSuccess", response.IsSuccessStatusCode));

        var respHeaders = new Dictionary<string, string>();
        foreach (var h in response.Headers)
            respHeaders[h.Key] = string.Join(", ", h.Value);
        props.Add(new Data("Headers", respHeaders));

        var contentHeaders = new Dictionary<string, string>();
        foreach (var h in response.Content.Headers)
            contentHeaders[h.Key] = string.Join(", ", h.Value);
        props.Add(new Data("ContentHeaders", contentHeaders));

        if (response.Content.Headers.ContentType?.CharSet != null)
            props.Add(new Data("Charset", response.Content.Headers.ContentType.CharSet));
    }

    // --- Streaming ---

    private static async Task<Data> HandleStreamingAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        GoalCall onStream,
        StreamFormat? streamAs,
        bool unsigned,
        EngineType engine,
        Context.@this context,
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
                var err = Data.FromError(new ServiceError(
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
                    await StreamBytesAsync(stream, onStream, engine, context, ct);
                    break;

                case StreamFormat.SSE:
                    await StreamSSEAsync(stream, onStream, engine, context, maxSSEBufferSize, ct);
                    break;

                default:
                    if (isPlang)
                        await StreamPlangAsync(stream, onStream, engine, context, ct);
                    else
                        await StreamLinesAsync(stream, onStream, engine, context, ct);
                    break;
            }

            var result = Data.Ok();
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
        EngineType engine, Context.@this context, CancellationToken ct)
    {
        var paramName = template.Parameters?.Count > 0 ? template.Parameters[0].Name : defaultName;
        var call = new GoalCall
        {
            Name = template.Name,
            PrPath = template.PrPath,
            Parameters = new List<Data> { new Data(paramName, value, type) }
        };
        var result = await engine.RunGoalAsync(call, context, ct);
        if (!result.Success)
            await engine.Channels.WriteAsync(EngineChannels.StdErr, result);
    }

    private static StreamFormat DetectStreamFormat(string contentType)
    {
        if (contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
            return StreamFormat.SSE;
        return StreamFormat.Line;
    }

    private static async Task StreamLinesAsync(
        Stream stream, GoalCall onStream,
        EngineType engine, Context.@this context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            await RunCallbackAsync(onStream, line, PlangType.String, "chunk", engine, context, ct);
        }
    }

    private static async Task StreamSSEAsync(
        Stream stream, GoalCall onStream,
        EngineType engine, Context.@this context, long maxBufferSize, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                if (dataBuffer.Length > 0)
                    await RunCallbackAsync(onStream, dataBuffer.ToString(), PlangType.String, "chunk", engine, context, ct);
                break;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var data = line.Length > 5 ? line[5..].TrimStart() : "";

                // Guard against unbounded SSE messages (no blank-line boundary)
                if (dataBuffer.Length + data.Length + 1 > maxBufferSize)
                {
                    await engine.Channels.WriteAsync(EngineChannels.StdErr,
                        Data.FromError(new ServiceError(
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
                await RunCallbackAsync(onStream, dataBuffer.ToString(), PlangType.String, "chunk", engine, context, ct);
                dataBuffer.Clear();
            }
        }
    }

    private static async Task StreamBytesAsync(
        Stream stream, GoalCall onStream,
        EngineType engine, Context.@this context, CancellationToken ct)
    {
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            var chunk = new byte[bytesRead];
            System.Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

            await RunCallbackAsync(onStream, chunk, null, "chunk", engine, context, ct);
        }
    }

    private static async Task StreamPlangAsync(
        Stream stream, GoalCall onStream,
        EngineType engine, Context.@this context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            // Each NDJSON line is a Data object with Signature populated via [In]
            Data? data;
            try
            {
                data = JsonSerializer.Deserialize<Data>(line, _transportInOptions);
            }
            catch (JsonException)
            {
                await engine.Channels.WriteAsync(EngineChannels.StdErr,
                    Data.FromError(new ServiceError("Malformed NDJSON line in application/plang stream", "PlangStreamError", 400)));
                continue;
            }
            if (data == null) continue;

            // Verify signature — pass Data straight to verify
            var verifyAction = new signing.verify
            {
                Context = context,
                Data = data
            };

            var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
            if (!verifyResult.Success)
            {
                await RunCallbackAsync(onStream, verifyResult, null, "chunk", engine, context, ct);
                continue;
            }

            context.Variables.Set("!ServiceIdentity", data.Signature?.Identity);
            await RunCallbackAsync(onStream, data, null, "chunk", engine, context, ct);
        }
    }

    // --- Progress reporting ---

    private static async Task<long> StreamWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        GoalCall? onProgress,
        EngineType engine,
        Context.@this context,
        CancellationToken ct)
    {
        var buffer = new byte[8192];
        long bytesTransferred = 0;
        var lastReport = DateTimeOffset.UtcNow;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, ct);
            bytesTransferred += bytesRead;

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
                    await RunCallbackAsync(onProgress, progress, null, "progress", engine, context, ct);
                }
            }
        }

        return bytesTransferred;
    }

    // --- Upload content resolution ---

    private static async Task<HttpContent> ResolveUploadContentAsync(
        upload action, App.SafeFileSystem.IPLangFileSystem fs, string encoding)
    {
        if (action.As.HasValue)
        {
            return action.As.Value switch
            {
                ContentAs.File => await CreateFileContentAsync(fs, action.Content.ToString()!),
                ContentAs.Base64 => CreateBase64Content(action.Content.ToString()!),
                ContentAs.Form => await CreateFormContentAsync(fs, action.Content),
                ContentAs.Text => new StringContent(
                    action.Content is string s ? s : JsonSerializer.Serialize(action.Content),
                    Encoding.GetEncoding(encoding)),
                _ => new StringContent(action.Content.ToString()!, Encoding.GetEncoding(encoding))
            };
        }

        // Auto-detect
        if (action.Content is Dictionary<string, object> ||
            action.Content is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            return await CreateFormContentAsync(fs, action.Content);
        }

        if (action.Content is string str)
        {
            var path = fs.ValidatePath(str);
            if (fs.File.Exists(path))
                return await CreateFileContentAsync(fs, path);

            return new StringContent(str, Encoding.GetEncoding(encoding));
        }

        return new StringContent(
            JsonSerializer.Serialize(action.Content),
            Encoding.GetEncoding(encoding),
            "application/json");
    }

    private static async Task<HttpContent> CreateFileContentAsync(App.SafeFileSystem.IPLangFileSystem fs, string path)
    {
        var validPath = fs.ValidatePath(path);
        var bytes = await fs.File.ReadAllBytesAsync(validPath);
        var content = new ByteArrayContent(bytes);
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

    private static async Task<HttpContent> CreateFormContentAsync(App.SafeFileSystem.IPLangFileSystem fs, object content)
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
                var filePath = fs.ValidatePath(value[1..]);
                var bytes = await fs.File.ReadAllBytesAsync(filePath);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var fileName = fs.Path.GetFileName(filePath);
                form.Add(fileContent, kvp.Key, fileName);
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
