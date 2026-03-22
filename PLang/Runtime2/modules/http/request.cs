using System.Net.Http;
using System.Text;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Core HTTP action. Handles all HTTP methods, response parsing, signing, and streaming.
/// </summary>
[Action("request")]
public partial class request : IContext
{
    public partial string Url { get; init; }

    [Default(HttpMethod.GET)]
    public partial HttpMethod Method { get; init; }

    public partial object? Body { get; init; }
    public partial Dictionary<string, object>? Headers { get; init; }

    [Default("application/json")]
    public partial string ContentType { get; init; }

    [Default("utf-8")]
    public partial string Encoding { get; init; }

    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    [Default(false)]
    public partial bool Unsigned { get; init; }

    public partial sign? SignOptions { get; init; }
    public partial GoalCall? OnStream { get; init; }
    public partial StreamFormat? StreamAs { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        var config = engine.Settings.For<Config>(Context);

        // Resolve configuration with per-step overrides
        var resolvedUnsigned = Unsigned || config.Resolve("Unsigned", false);
        var resolvedTimeout = TimeoutInSec > 0 ? TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var resolvedContentType = ContentType ?? config.Resolve("ContentType", "application/json");
        var resolvedEncoding = Encoding ?? config.Resolve("Encoding", "utf-8");

        // Resolve URL
        var urlResult = HttpHelper.ResolveUrl(Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        // Build headers
        var headers = HttpHelper.MergeHeaders(Headers, config);

        // Build body content
        HttpContent? httpContent = null;
        string? bodyString = null;
        if (Body != null)
        {
            if (resolvedContentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && Body is Dictionary<string, object> formDict)
            {
                var formValues = new Dictionary<string, string>();
                foreach (var kvp in formDict)
                    formValues[kvp.Key] = kvp.Value?.ToString() ?? "";
                httpContent = new FormUrlEncodedContent(formValues);
            }
            else
            {
                bodyString = Body is string s ? s : System.Text.Json.JsonSerializer.Serialize(Body);
                var encoding = System.Text.Encoding.GetEncoding(resolvedEncoding);
                httpContent = new StringContent(bodyString, encoding, resolvedContentType);
            }
        }

        // Build request message
        var httpMethod = HttpHelper.ToSystemMethod(Method);
        var requestMessage = new HttpRequestMessage(httpMethod, resolvedUrl)
        {
            Content = httpContent
        };

        // Apply headers
        HttpHelper.ApplyHeaders(requestMessage, headers);

        // Sign request
        try
        {
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

            // Determine completion option
            var completionOption = OnStream != null
                ? HttpCompletionOption.ResponseHeadersRead
                : HttpCompletionOption.ResponseContentRead;

            // Send request with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
            cts.CancelAfter(TimeSpan.FromSeconds(resolvedTimeout));

            var response = await providerResult.Value!.SendAsync(requestMessage, completionOption, cts.Token);

            // Handle streaming
            if (OnStream != null)
            {
                return await HttpHelper.HandleStreamingAsync(
                    response, requestMessage, OnStream, StreamAs,
                    resolvedUnsigned, engine, Context, cts.Token);
            }

            // Parse response
            return await HttpHelper.ParseResponseAsync(response, requestMessage, resolvedUnsigned, engine, Context);
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
}
