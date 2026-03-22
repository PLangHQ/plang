using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;
using System.Text.Json;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Uploads file content — binary file, base64, or multipart form data.
/// The action resolves what Content is based on the As hint or auto-detection.
/// </summary>
[Action("upload", Cacheable = false)]
public partial class upload : IContext
{
    public partial string Url { get; init; }
    public partial object Content { get; init; }

    [Default(HttpMethod.POST)]
    public partial HttpMethod Method { get; init; }

    public partial Dictionary<string, object>? Headers { get; init; }

    [Default("utf-8")]
    public partial string Encoding { get; init; }

    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    [Default(false)]
    public partial bool Unsigned { get; init; }

    public partial sign? SignOptions { get; init; }
    public partial ContentAs? As { get; init; }
    public partial GoalCall? OnProgress { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        var config = engine.Settings.For<Config>(Context);
        var fs = engine.FileSystem;

        // Resolve config
        var resolvedUnsigned = Unsigned || config.Resolve("Unsigned", false);
        var resolvedTimeout = TimeoutInSec > 0 ? TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var resolvedEncoding = Encoding ?? config.Resolve("Encoding", "utf-8");

        // Resolve URL
        var urlResult = HttpHelper.ResolveUrl(Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        // Build headers
        var headers = HttpHelper.MergeHeaders(Headers, config);

        // Resolve content
        HttpContent httpContent;
        try
        {
            httpContent = await ResolveContentAsync(fs, resolvedEncoding);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
        catch (FormatException ex)
        {
            return Data.FromError(new ServiceError(ex.Message, "InvalidContent", 400));
        }

        // Build request
        var httpMethod = HttpHelper.ToSystemMethod(Method);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl)
        {
            Content = httpContent
        };
        HttpHelper.ApplyHeaders(requestMessage, headers);

        try
        {
            // Sign request
            string? bodyString = null;
            if (httpContent is StringContent sc)
                bodyString = await sc.ReadAsStringAsync();

            var signResult = await HttpHelper.SignRequestAsync(
                engine, Context, resolvedUnsigned, SignOptions, bodyString, resolvedUrl, httpMethod.Method);
            if (signResult != null)
            {
                if (!signResult.Success) return signResult;
                HttpHelper.ApplySignature(requestMessage, signResult);
            }

            // Get provider
            var providerResult = engine.Providers.Get<IHttpProvider>();
            if (!providerResult.Success) return providerResult;

            // Send request
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
            cts.CancelAfter(TimeSpan.FromSeconds(resolvedTimeout));

            var response = await providerResult.Value!.SendAsync(
                requestMessage, HttpCompletionOption.ResponseContentRead, cts.Token);

            // Parse response (same as request action)
            return await HttpHelper.ParseResponseAsync(response, requestMessage, resolvedUnsigned, engine, Context);
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

    private async Task<HttpContent> ResolveContentAsync(Interfaces.IPLangFileSystem fs, string encoding)
    {
        var contentAs = As;

        // Explicit content hint
        if (contentAs.HasValue)
        {
            return contentAs.Value switch
            {
                ContentAs.File => await CreateFileContentAsync(fs, Content.ToString()!),
                ContentAs.Base64 => CreateBase64Content(Content.ToString()!),
                ContentAs.Form => await CreateFormContentAsync(fs, Content),
                ContentAs.Text => new StringContent(
                    Content is string s ? s : JsonSerializer.Serialize(Content),
                    System.Text.Encoding.GetEncoding(encoding)),
                _ => new StringContent(Content.ToString()!, System.Text.Encoding.GetEncoding(encoding))
            };
        }

        // Auto-detect
        if (Content is Dictionary<string, object> dict ||
            Content is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            return await CreateFormContentAsync(fs, Content);
        }

        if (Content is string str)
        {
            // Check if it's an existing file path
            var path = fs.ValidatePath(str);
            if (fs.File.Exists(path))
                return await CreateFileContentAsync(fs, path);

            // Treat as text
            return new StringContent(str, System.Text.Encoding.GetEncoding(encoding));
        }

        // Fallback: serialize to JSON string
        return new StringContent(
            JsonSerializer.Serialize(Content),
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

    private async Task<HttpContent> CreateFormContentAsync(Interfaces.IPLangFileSystem fs, object content)
    {
        var form = new MultipartFormDataContent();
        Dictionary<string, object> fields;

        if (content is Dictionary<string, object> dict)
        {
            fields = dict;
        }
        else if (content is JsonElement je)
        {
            fields = new Dictionary<string, object>();
            foreach (var prop in je.EnumerateObject())
                fields[prop.Name] = prop.Value.ToString();
        }
        else
        {
            fields = new Dictionary<string, object> { ["data"] = content };
        }

        foreach (var kvp in fields)
        {
            var value = kvp.Value?.ToString() ?? "";

            // @ prefix = file reference (runtime1 convention)
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
