using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Settings;
using EngineType = PLang.Runtime2.Engine.@this;
using SysHttpMethod = System.Net.Http.HttpMethod;

namespace PLang.Runtime2.modules.http.providers;

/// <summary>
/// Default HTTP provider. Owns all HTTP behavior — actions delegate to this via `this`.
/// Lazily creates HttpClient on first request. Uses HttpHelper public utilities for shared logic.
/// Swappable: custom providers can reimplement from scratch or reuse HttpHelper.
/// </summary>
public sealed class DefaultHttpProvider : IHttpProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private HttpClient? _client;
    private bool _followRedirects = true;
    private int _maxRedirects = 10;

    // --- IHttpProvider: action-level methods ---

    public async Task<Data> SendAsync(request action)
    {
        var engine = action.Context.Engine;
        var config = engine.Settings.For<Config>(action.Context);

        var resolvedUnsigned = action.Unsigned || config.Resolve("Unsigned", false);
        var resolvedTimeout = action.TimeoutInSec > 0 ? action.TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var resolvedContentType = action.ContentType ?? config.Resolve("ContentType", "application/json");
        var resolvedEncoding = action.Encoding ?? config.Resolve("Encoding", "utf-8");

        var urlResult = HttpHelper.ResolveUrl(action.Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = HttpHelper.MergeHeaders(action.Headers, config);

        // Build body
        HttpContent? httpContent = null;
        string? bodyString = null;
        if (action.Body != null)
        {
            if (resolvedContentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
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
                var encoding = System.Text.Encoding.GetEncoding(resolvedEncoding);
                httpContent = new StringContent(bodyString, encoding, resolvedContentType);
            }
        }

        var httpMethod = HttpHelper.ToSystemMethod(action.Method);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        HttpHelper.ApplyHeaders(requestMessage, headers);

        try
        {
            var signResult = await HttpHelper.SignRequestAsync(
                engine, action.Context, resolvedUnsigned, action.SignOptions, bodyString, resolvedUrl, httpMethod.Method);
            if (signResult != null)
            {
                if (!signResult.Success) return signResult;
                HttpHelper.ApplySignature(requestMessage, signResult);
            }

            var completionOption = action.OnStream != null
                ? HttpCompletionOption.ResponseHeadersRead
                : HttpCompletionOption.ResponseContentRead;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
            cts.CancelAfter(TimeSpan.FromSeconds(resolvedTimeout));

            var response = await SendHttpAsync(requestMessage, completionOption, cts.Token);

            if (action.OnStream != null)
            {
                return await HttpHelper.HandleStreamingAsync(
                    response, requestMessage, action.OnStream, action.StreamAs,
                    resolvedUnsigned, engine, action.Context, cts.Token);
            }

            return await HttpHelper.ParseResponseAsync(response, requestMessage, resolvedUnsigned, engine, action.Context);
        }
        catch (TaskCanceledException)
        {
            return Data.FromError(new ServiceError("Request timed out", "Timeout", 408));
        }
        catch (HttpRequestException ex)
        {
            return Data.FromError(new ServiceError(ex.Message, "HttpError", (int)(ex.StatusCode ?? 0)));
        }
    }

    public async Task<Data> DownloadAsync(download action)
    {
        var engine = action.Context.Engine;
        var config = engine.Settings.For<Config>(action.Context);
        var fs = engine.FileSystem;

        var resolvedUnsigned = action.Unsigned || config.Resolve("Unsigned", false);
        var resolvedTimeout = action.TimeoutInSec > 0 ? action.TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var urlResult = HttpHelper.ResolveUrl(action.Url, config);
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

        var headers = HttpHelper.MergeHeaders(action.Headers, config);
        var requestMessage = new HttpRequestMessage(SysHttpMethod.Get, resolvedUrl);
        HttpHelper.ApplyHeaders(requestMessage, headers);

        try
        {
            var signResult = await HttpHelper.SignRequestAsync(
                engine, action.Context, resolvedUnsigned, action.SignOptions, null, resolvedUrl, "GET");
            if (signResult != null)
            {
                if (!signResult.Success) return signResult;
                HttpHelper.ApplySignature(requestMessage, signResult);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
            cts.CancelAfter(TimeSpan.FromSeconds(resolvedTimeout));

            var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = "";
                try { errorBody = await response.Content.ReadAsStringAsync(cts.Token); }
                catch { /* best effort */ }
                var err = Data.FromError(new ServiceError(
                    $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}".Trim(),
                    "HttpError", (int)response.StatusCode));
                HttpHelper.BuildProperties(err, requestMessage, response);
                return err;
            }

            var dir = fs.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
                fs.Directory.CreateDirectory(dir);

            var totalBytes = response.Content.Headers.ContentLength;
            using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var fileStream = fs.File.Create(savePath);

            await HttpHelper.StreamWithProgressAsync(
                responseStream, fileStream, totalBytes, action.OnProgress, engine, action.Context, cts.Token);

            return Data.Ok(action.SaveTo);
        }
        catch (TaskCanceledException)
        {
            return Data.FromError(new ServiceError("Download timed out", "Timeout", 408));
        }
        catch (HttpRequestException ex)
        {
            return Data.FromError(new ServiceError(ex.Message, "HttpError", (int)(ex.StatusCode ?? 0)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public async Task<Data> UploadAsync(upload action)
    {
        var engine = action.Context.Engine;
        var config = engine.Settings.For<Config>(action.Context);
        var fs = engine.FileSystem;

        var resolvedUnsigned = action.Unsigned || config.Resolve("Unsigned", false);
        var resolvedTimeout = action.TimeoutInSec > 0 ? action.TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var resolvedEncoding = action.Encoding ?? config.Resolve("Encoding", "utf-8");

        var urlResult = HttpHelper.ResolveUrl(action.Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        var headers = HttpHelper.MergeHeaders(action.Headers, config);

        HttpContent httpContent;
        try
        {
            httpContent = await ResolveUploadContentAsync(action, fs, resolvedEncoding);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
        catch (FormatException ex)
        {
            return Data.FromError(new ServiceError(ex.Message, "InvalidContent", 400));
        }

        var httpMethod = HttpHelper.ToSystemMethod(action.Method);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl) { Content = httpContent };
        HttpHelper.ApplyHeaders(requestMessage, headers);

        try
        {
            string? bodyString = null;
            if (httpContent is StringContent sc)
                bodyString = await sc.ReadAsStringAsync();

            var signResult = await HttpHelper.SignRequestAsync(
                engine, action.Context, resolvedUnsigned, action.SignOptions, bodyString, resolvedUrl, httpMethod.Method);
            if (signResult != null)
            {
                if (!signResult.Success) return signResult;
                HttpHelper.ApplySignature(requestMessage, signResult);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
            cts.CancelAfter(TimeSpan.FromSeconds(resolvedTimeout));

            var response = await SendHttpAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cts.Token);

            return await HttpHelper.ParseResponseAsync(response, requestMessage, resolvedUnsigned, engine, action.Context);
        }
        catch (TaskCanceledException)
        {
            return Data.FromError(new ServiceError("Upload timed out", "Timeout", 408));
        }
        catch (HttpRequestException ex)
        {
            return Data.FromError(new ServiceError(ex.Message, "HttpError", (int)(ex.StatusCode ?? 0)));
        }
    }

    public Data Configure(configure action)
    {
        var engine = action.Context.Engine;
        var isDefault = action.Default;

        if (action.TimeoutInSec.HasValue)
            engine.Settings.Set("http.TimeoutInSec", action.TimeoutInSec.Value, action.Context, isDefault);
        if (action.BaseUrl != null)
            engine.Settings.Set("http.BaseUrl", action.BaseUrl, action.Context, isDefault);
        if (action.DefaultHeaders != null)
            engine.Settings.Set("http.DefaultHeaders", action.DefaultHeaders, action.Context, isDefault);
        if (action.ContentType != null)
            engine.Settings.Set("http.ContentType", action.ContentType, action.Context, isDefault);
        if (action.Encoding != null)
            engine.Settings.Set("http.Encoding", action.Encoding, action.Context, isDefault);
        if (action.Unsigned.HasValue)
            engine.Settings.Set("http.Unsigned", action.Unsigned.Value, action.Context, isDefault);
        if (action.FollowRedirects.HasValue)
            engine.Settings.Set("http.FollowRedirects", action.FollowRedirects.Value, action.Context, isDefault);
        if (action.MaxRedirects.HasValue)
            engine.Settings.Set("http.MaxRedirects", action.MaxRedirects.Value, action.Context, isDefault);

        // Apply handler-level settings
        if (action.FollowRedirects.HasValue || action.MaxRedirects.HasValue)
        {
            var config = new Config
            {
                FollowRedirects = action.FollowRedirects ?? _followRedirects,
                MaxRedirects = action.MaxRedirects ?? _maxRedirects
            };

            if (_client != null && (config.FollowRedirects != _followRedirects || config.MaxRedirects != _maxRedirects))
                return Data.FromError(new ServiceError(
                    "Cannot change FollowRedirects/MaxRedirects after first HTTP request",
                    "ConfigLocked", 409));

            _followRedirects = config.FollowRedirects;
            _maxRedirects = config.MaxRedirects;
        }

        return Data.Ok();
    }

    // --- Internal HTTP transport ---

    private Task<HttpResponseMessage> SendHttpAsync(
        HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken ct)
    {
        _client ??= CreateClient();
        return _client.SendAsync(request, completionOption, ct);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    private HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = _followRedirects,
        MaxAutomaticRedirections = _maxRedirects
    });

    // --- Upload content resolution ---

    private static async Task<HttpContent> ResolveUploadContentAsync(
        upload action, Interfaces.IPLangFileSystem fs, string encoding)
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
                    System.Text.Encoding.GetEncoding(encoding)),
                _ => new StringContent(action.Content.ToString()!, System.Text.Encoding.GetEncoding(encoding))
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

            return new StringContent(str, System.Text.Encoding.GetEncoding(encoding));
        }

        return new StringContent(
            JsonSerializer.Serialize(action.Content),
            System.Text.Encoding.GetEncoding(encoding),
            "application/json");
    }

    private static async Task<HttpContent> CreateFileContentAsync(Interfaces.IPLangFileSystem fs, string path)
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

    private static async Task<HttpContent> CreateFormContentAsync(Interfaces.IPLangFileSystem fs, object content)
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
}
