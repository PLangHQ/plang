using System.Net.Http;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Downloads a file from a URL. Three-state file handling: error (default), overwrite, or skip.
/// </summary>
[Action("download", Cacheable = false)]
public partial class download : IContext
{
    public partial string Url { get; init; }
    public partial string SaveTo { get; init; }

    [Default(FileExists.Error)]
    public partial FileExists IfExists { get; init; }

    public partial Dictionary<string, object>? Headers { get; init; }

    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    [Default(false)]
    public partial bool Unsigned { get; init; }

    public partial sign? SignOptions { get; init; }
    public partial GoalCall? OnProgress { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        var config = engine.Settings.For<Config>(Context);
        var fs = engine.FileSystem;

        // Resolve URL
        var resolvedUnsigned = Unsigned || config.Resolve("Unsigned", false);
        var resolvedTimeout = TimeoutInSec > 0 ? TimeoutInSec : config.Resolve("TimeoutInSec", 30);
        var urlResult = HttpHelper.ResolveUrl(Url, config);
        if (!urlResult.Success) return urlResult;
        var resolvedUrl = urlResult.Value!;

        // Resolve save path
        var savePath = fs.ValidatePath(SaveTo);

        // Check file existence
        if (fs.File.Exists(savePath))
        {
            switch (IfExists)
            {
                case FileExists.Error:
                    return Data.FromError(new ServiceError(
                        $"File already exists: {SaveTo}", "FileExists", 409));
                case FileExists.Skip:
                    return Data.Ok(SaveTo);
                case FileExists.Overwrite:
                    break; // continue with download
            }
        }

        // Build headers
        var headers = HttpHelper.MergeHeaders(Headers, config);

        // Build request
        var requestMessage = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, resolvedUrl);
        HttpHelper.ApplyHeaders(requestMessage, headers);

        try
        {
            // Sign request
            var signResult = await HttpHelper.SignRequestAsync(
                engine, Context, resolvedUnsigned, SignOptions, null, resolvedUrl, "GET");
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
                requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            // Check status
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

            // Create parent directories
            var dir = fs.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
                fs.Directory.CreateDirectory(dir);

            // Stream response to file
            var totalBytes = response.Content.Headers.ContentLength;
            using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var fileStream = fs.File.Create(savePath);

            await HttpHelper.StreamWithProgressAsync(
                responseStream, fileStream, totalBytes, OnProgress, engine, Context, cts.Token);

            return Data.Ok(SaveTo);
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
}
