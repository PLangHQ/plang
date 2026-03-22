using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;
using EngineType = PLang.Runtime2.Engine.@this;
using SysHttpMethod = System.Net.Http.HttpMethod;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Shared HTTP helpers used by request, download, and upload actions.
/// Static methods, no state — just shared pipeline logic.
/// </summary>
internal static class HttpHelper
{
    /// <summary>
    /// Converts PLang HttpMethod enum to System.Net.Http.HttpMethod.
    /// </summary>
    internal static SysHttpMethod ToSystemMethod(HttpMethod method) => method switch
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

    /// <summary>
    /// Resolves URL: auto-prefix https://, combine with BaseUrl for relative URLs.
    /// Returns error if relative URL with no BaseUrl configured.
    /// </summary>
    internal static Data<string> ResolveUrl(string url, ModuleView<Config> config)
    {
        var baseUrl = config.Resolve<string?>("BaseUrl", null);

        // Relative URL check
        if (url.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(baseUrl))
                return Data<string>.FromError(new ServiceError(
                    "Relative URL requires a BaseUrl configuration. Use 'configure http, base url https://...'",
                    "NoBaseUrl", 400));

            baseUrl = baseUrl.TrimEnd('/');
            return Data<string>.Ok(baseUrl + url);
        }

        // Auto-prefix https:// if no protocol
        if (!url.Contains("://"))
            url = "https://" + url;

        return Data<string>.Ok(url);
    }

    /// <summary>
    /// Merges DefaultHeaders from config with per-step headers. Per-step wins on conflict.
    /// </summary>
    internal static Dictionary<string, string> MergeHeaders(
        Dictionary<string, object>? stepHeaders,
        ModuleView<Config> config)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Config defaults first
        var defaults = config.Resolve<Dictionary<string, object>?>("DefaultHeaders", null);
        if (defaults != null)
        {
            foreach (var kvp in defaults)
                merged[kvp.Key] = kvp.Value?.ToString() ?? "";
        }

        // Per-step overrides
        if (stepHeaders != null)
        {
            foreach (var kvp in stepHeaders)
                merged[kvp.Key] = kvp.Value?.ToString() ?? "";
        }

        return merged;
    }

    /// <summary>
    /// Signs a request via engine.RunAction&lt;sign&gt;().
    /// Returns null if unsigned, the sign result Data on success (navigate .Signature for SignedData).
    /// </summary>
    internal static async Task<Data?> SignRequestAsync(
        EngineType engine,
        PLangContext context,
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
            Data = bodyContent ?? "",
            Headers = new Dictionary<string, object>
            {
                ["url"] = url,
                ["method"] = method
            },
            Contracts = signOptions?.Contracts,
            ExpiresInMs = signOptions?.ExpiresInMs,
            Provider = signOptions?.Provider
        };

        // sign returns Data with .Signature = SignedData — relay the full object
        return await engine.RunAction<signing.sign>(httpSign, context);
    }

    /// <summary>
    /// Applies signing result to the request: X-Signature header and Accept: application/plang.
    /// Navigates the sign result Data for the SignedData envelope.
    /// </summary>
    internal static void ApplySignature(HttpRequestMessage request, Data signResult)
    {
        var signatureJson = JsonSerializer.Serialize(signResult.Signature, SignedData.SigningOptions);
        request.Headers.TryAddWithoutValidation("X-Signature", signatureJson);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/plang"));
    }

    /// <summary>
    /// Applies merged headers to the request message.
    /// Splits between content headers and request headers.
    /// </summary>
    internal static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string> headers)
    {
        foreach (var kvp in headers)
        {
            // Content headers must be set on content, not request
            if (IsContentHeader(kvp.Key))
            {
                request.Content?.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }
    }

    private static bool IsContentHeader(string name) =>
        name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Content-Range", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses HTTP response based on content type.
    /// Returns Data with Value set to parsed content and Properties with metadata.
    /// </summary>
    internal static async Task<Data> ParseResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        bool unsigned,
        EngineType engine,
        PLangContext context)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var statusCode = (int)response.StatusCode;

        // Error status codes
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = "";
            try { errorBody = await response.Content.ReadAsStringAsync(); }
            catch { /* best effort */ }

            var errorData = Data.FromError(new ServiceError(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}".Trim(),
                "HttpError", statusCode));

            // Check for signed error response (signature field in error JSON)
            if (!unsigned && !string.IsNullOrEmpty(errorBody))
            {
                try
                {
                    await TryExtractSignedErrorIdentity(errorBody, engine, context);
                }
                catch { /* best effort — don't mask the original error */ }
            }

            BuildProperties(errorData, request, response);
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

            return await ParsePlangResponseAsync(response, request, engine, context);
        }

        // JSON response
        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = await response.Content.ReadAsStringAsync();
            object? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
                parsed = json; // fallback to raw string
            }
            var result = Data.Ok(parsed);
            BuildProperties(result, request, response);
            return result;
        }

        // XML response — store as-is with type marker
        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = await response.Content.ReadAsStringAsync();
            var result = Data.Ok(xml, PLang.Runtime2.Engine.Memory.Type.FromMime("application/xml"));
            BuildProperties(result, request, response);
            return result;
        }

        // Text response
        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            var text = await response.Content.ReadAsStringAsync();
            var result = Data.Ok(text);
            BuildProperties(result, request, response);
            return result;
        }

        // Binary response
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var binaryResult = Data.Ok(bytes);
        BuildProperties(binaryResult, request, response);
        return binaryResult;
    }

    /// <summary>
    /// Parses application/plang response: deserialize as Data with SignedData, verify signature,
    /// set %!ServiceIdentity% scoped variable.
    /// </summary>
    private static async Task<Data> ParsePlangResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        EngineType engine,
        PLangContext context)
    {
        var body = await response.Content.ReadAsStringAsync();

        SignedData? signedData;
        try
        {
            signedData = JsonSerializer.Deserialize<SignedData>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            var err = Data.FromError(new ServiceError(
                $"Failed to deserialize application/plang response: {ex.Message}",
                "PlangDeserializeError", 400));
            BuildProperties(err, request, response);
            return err;
        }

        if (signedData == null)
        {
            var err = Data.FromError(new ServiceError(
                "application/plang response deserialized to null",
                "PlangDeserializeError", 400));
            BuildProperties(err, request, response);
            return err;
        }

        // Verify signature via engine — verify action navigates SignedData
        // Data.Value left null — we don't have the original payload, only the envelope.
        // This skips the re-hash step in VerifyAsync (step 8) and only checks the cryptographic signature.
        var verifyData = new Data("");
        verifyData.Signature = signedData;

        var verifyAction = new signing.verify
        {
            Context = context,
            Data = verifyData
        };

        var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
        if (!verifyResult.Success)
        {
            BuildProperties(verifyResult, request, response);
            return verifyResult;
        }

        // Set %!ServiceIdentity% — navigate the deserialized SignedData
        context.MemoryStack.Set("!ServiceIdentity", signedData.Identity);

        // Build result with Signature attached
        var result = Data.Ok(signedData);
        result.Signature = signedData;
        BuildProperties(result, request, response);
        return result;
    }

    /// <summary>
    /// Tries to extract identity from a signed error response (error JSON with "signature" field).
    /// </summary>
    private static async Task TryExtractSignedErrorIdentity(
        string errorBody, EngineType engine, PLangContext context)
    {
        using var doc = JsonDocument.Parse(errorBody);
        if (!doc.RootElement.TryGetProperty("signature", out var sigElement))
            return;

        var signedData = JsonSerializer.Deserialize<SignedData>(sigElement.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (signedData == null) return;

        var verifyData = new Data("");
        verifyData.Signature = signedData;

        var verifyAction = new signing.verify
        {
            Context = context,
            Data = verifyData
        };

        var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
        if (verifyResult.Success)
            context.MemoryStack.Set("!ServiceIdentity", signedData.Identity);
    }

    /// <summary>
    /// Populates Data.Properties with request and response metadata.
    /// </summary>
    internal static void BuildProperties(Data data, HttpRequestMessage request, HttpResponseMessage response)
    {
        var props = data.Properties;

        // Request metadata
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

        // Response metadata
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

    /// <summary>
    /// Handles streaming response: reads chunks and calls goal per chunk.
    /// </summary>
    internal static async Task<Data> HandleStreamingAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        GoalCall onStream,
        StreamFormat? streamAs,
        bool unsigned,
        EngineType engine,
        PLangContext context,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = "";
            try { errorBody = await response.Content.ReadAsStringAsync(ct); }
            catch { /* best effort */ }
            var err = Data.FromError(new ServiceError(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}".Trim(),
                "HttpError", (int)response.StatusCode));
            BuildProperties(err, request, response);
            return err;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        // Determine stream format
        var format = streamAs ?? DetectStreamFormat(contentType);

        // application/plang always uses NDJSON with signature verification
        var isPlang = contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase);
        if (isPlang && unsigned)
        {
            var err = Data.FromError(new ServiceError(
                "Unsigned request received application/plang streaming response — this is not allowed",
                "UnsignedPlang", 403));
            BuildProperties(err, request, response);
            return err;
        }

        // Determine the variable name for chunk data
        var dataVarName = "!data";
        if (onStream.Parameters != null)
        {
            // Look for a parameter mapping like data=%event% — use the value name
            foreach (var p in onStream.Parameters)
            {
                if (p.Value is string s && s.StartsWith('%') && s.EndsWith('%'))
                {
                    dataVarName = s.Trim('%');
                    break;
                }
            }
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);

        switch (format)
        {
            case StreamFormat.Bytes:
                await StreamBytesAsync(stream, dataVarName, onStream, engine, context, ct);
                break;

            case StreamFormat.SSE:
                await StreamSSEAsync(stream, dataVarName, onStream, engine, context, ct);
                break;

            default: // Line
                if (isPlang)
                    await StreamPlangAsync(stream, dataVarName, onStream, engine, context, ct);
                else
                    await StreamLinesAsync(stream, dataVarName, onStream, engine, context, ct);
                break;
        }

        var result = Data.Ok();
        BuildProperties(result, request, response);
        return result;
    }

    private static StreamFormat DetectStreamFormat(string contentType)
    {
        if (contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
            return StreamFormat.SSE;
        return StreamFormat.Line;
    }

    private static async Task StreamLinesAsync(
        Stream stream, string varName, GoalCall onStream,
        EngineType engine, PLangContext context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            context.MemoryStack.Set(varName, line);
            await engine.RunGoalAsync(onStream, context, ct);
        }
    }

    private static async Task StreamSSEAsync(
        Stream stream, string varName, GoalCall onStream,
        EngineType engine, PLangContext context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                // End of stream — flush any remaining data
                if (dataBuffer.Length > 0)
                {
                    context.MemoryStack.Set(varName, dataBuffer.ToString());
                    await engine.RunGoalAsync(onStream, context, ct);
                }
                break;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var data = line.Length > 5 ? line[5..].TrimStart() : "";
                if (dataBuffer.Length > 0) dataBuffer.Append('\n');
                dataBuffer.Append(data);
            }
            else if (line.Length == 0 && dataBuffer.Length > 0)
            {
                // Empty line = event boundary
                context.MemoryStack.Set(varName, dataBuffer.ToString());
                await engine.RunGoalAsync(onStream, context, ct);
                dataBuffer.Clear();
            }
            // Ignore other SSE fields (event:, id:, retry:)
        }
    }

    private static async Task StreamBytesAsync(
        Stream stream, string varName, GoalCall onStream,
        EngineType engine, PLangContext context, CancellationToken ct)
    {
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            var chunk = new byte[bytesRead];
            System.Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

            context.MemoryStack.Set(varName, chunk);
            await engine.RunGoalAsync(onStream, context, ct);
        }
    }

    /// <summary>
    /// Streams application/plang NDJSON: each line is a Data object with signature verification.
    /// </summary>
    private static async Task StreamPlangAsync(
        Stream stream, string varName, GoalCall onStream,
        EngineType engine, PLangContext context, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;

            var signedData = JsonSerializer.Deserialize<SignedData>(line, jsonOptions);
            if (signedData == null) continue;

            // Verify each chunk's signature via engine
            // No original payload — skip re-hash, verify crypto signature only
            var verifyData = new Data("");
            verifyData.Signature = signedData;

            var verifyAction = new signing.verify
            {
                Context = context,
                Data = verifyData
            };

            var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
            if (!verifyResult.Success)
            {
                // Set error as data and let the goal handle it
                context.MemoryStack.Set(varName, verifyResult);
                await engine.RunGoalAsync(onStream, context, ct);
                continue;
            }

            context.MemoryStack.Set("!ServiceIdentity", signedData.Identity);

            var chunkData = Data.Ok(signedData);
            chunkData.Signature = signedData;
            context.MemoryStack.Set(varName, chunkData);
            await engine.RunGoalAsync(onStream, context, ct);
        }
    }

    /// <summary>
    /// Reports transfer progress by calling a goal every 500ms.
    /// Returns the total bytes transferred.
    /// </summary>
    internal static async Task<long> StreamWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        GoalCall? onProgress,
        EngineType engine,
        PLangContext context,
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
                    context.MemoryStack.Set("!data", progress);
                    await engine.RunGoalAsync(onProgress, context, ct);
                }
            }
        }

        return bytesTransferred;
    }
}
