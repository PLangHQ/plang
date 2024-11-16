using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LightInject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Modules.WebserverModule;

public class RequestHandler
{
    private readonly IServiceContainer container;
    private readonly GoalStep goalStep;
    private readonly HttpListenerContext httpContext;
    private readonly string plangVersion;
    private readonly WebserverInfo webserverInfo;

    public RequestHandler(HttpListenerContext httpContext, IServiceContainer container, WebserverInfo webserverInfo,
        string plangVersion, GoalStep goalStep)
    {
        this.httpContext = httpContext;
        this.container = container;
        this.webserverInfo = webserverInfo;
        this.plangVersion = plangVersion;
        this.goalStep = goalStep;
    }

    public IError? HandleRequest()
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        response.Headers.Add("Server", "plang v" + plangVersion);

        if (webserverInfo.SignedRequestRequired && string.IsNullOrEmpty(request.Headers.Get("X-Signature")))
            return new Error(
                "You must sign your request to user this web service. Using plang, you simply say. '- GET http://... ");

        var routing = GetRouting(request.Url?.LocalPath);
        if (routing == null) return ProcessStaticFileRequest(httpContext);

        var acceptTypes = request.AcceptTypes ?? ["text/html"];
        if (routing.ContentType != null && request.AcceptTypes != null)
            acceptTypes = request.AcceptTypes.Prepend(routing.ContentType).ToArray();
        var formatter = container.GetInstance<IOutputStreamFactory>().CreateHandler(acceptTypes);


        Goal? goal = null;
        string? goalPath = null;
        string? requestedFile = null;


        goal = prParser.GetGoal(Path.Combine(goalPath, ISettings.GoalFileName));
        if (goal == null)
        {
            await WriteNotfound(resp, "Goal could not be loaded");
            continue;
        }

        if (httpContext.Request.QueryString.GetValues("__signature__") != null)
        {
            httpContext.Response.AddHeader("X-Goal-Hash", goal.Hash);
            httpContext.Response.AddHeader("X-Goal-Signature", goal.Signature);
            httpContext.Response.StatusCode = 200;
            httpContext.Response.Close();
            continue;
        }

        long maxContentLength =
            goal.GoalInfo?.GoalApiInfo != null && goal.GoalInfo?.GoalApiInfo?.MaxContentLengthInBytes != 0
                ? goal.GoalInfo?.GoalApiInfo.MaxContentLengthInBytes ?? maxContentLengthInBytes
                : maxContentLengthInBytes;
        if (httpContext.Request.ContentLength64 > maxContentLength)
        {
            httpContext.Response.StatusCode = 413;
            using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
            {
                await writer.WriteAsync($"Content sent to server is to big. Max {maxContentLength} bytes");
                await writer.FlushAsync();
            }

            httpContext.Response.Close();
            continue;
        }

        if (httpContext.Request.IsWebSocketRequest)
        {
            ProcessWebsocketRequest(httpContext);
            continue;
        }

        if (goal.GoalInfo?.GoalApiInfo == null || string.IsNullOrEmpty(goal.GoalInfo.GoalApiInfo?.Method))
        {
            await WriteError(resp, "METHOD is not defined on goal");
            continue;
        }

        httpContext.Response.ContentEncoding = Encoding.GetEncoding(defaultResponseContentEncoding);
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.SendChunked = true;
        httpContext.Response.AddHeader("X-Goal-Hash", goal.Hash);
        httpContext.Response.AddHeader("X-Goal-Signature", goal.Signature);
        if (goal.GoalInfo.GoalApiInfo != null)
        {
            if (goal.GoalInfo.GoalApiInfo.ContentEncoding != null)
                httpContext.Response.ContentEncoding = Encoding.GetEncoding(defaultResponseContentEncoding);
            if (goal.GoalInfo.GoalApiInfo.ContentType != null)
                httpContext.Response.ContentType = goal.GoalInfo.GoalApiInfo.ContentType;

            if (goal.GoalInfo.GoalApiInfo.NoCacheOrNoStore != null)
            {
                httpContext.Response.Headers["Cache-Control"] = goal.GoalInfo.GoalApiInfo.NoCacheOrNoStore;
            }
            else if (goal.GoalInfo.GoalApiInfo.CacheControlPrivateOrPublic != null ||
                     goal.GoalInfo.GoalApiInfo.CacheControlMaxAge != null)
            {
                var publicOrPrivate = goal.GoalInfo.GoalApiInfo.CacheControlPrivateOrPublic;
                if (publicOrPrivate == null) publicOrPrivate = "public";


                httpContext.Response.Headers["Cache-Control"] =
                    $"{publicOrPrivate}, {goal.GoalInfo.GoalApiInfo.CacheControlMaxAge}";
            }
        }

        logger.LogDebug(
            $"Register container for webserver - AbsoluteAppStartupFolderPath:{goal.AbsoluteAppStartupFolderPath}");

        container.RegisterForPLangWebserver(goal.AbsoluteAppStartupFolderPath, Path.DirectorySeparatorChar.ToString(),
            httpContext);
        var context = container.GetInstance<PLangAppContext>();
        context.Add(ReservedKeywords.IsHttpRequest, true);

        var engine = container.GetInstance<IEngine>();
        engine.Init(container);
        engine.HttpContext = httpContext;

        var requestMemoryStack = engine.GetMemoryStack();
        var identityService = container.GetInstance<IPLangIdentityService>();
        var error = await ParseRequest(httpContext, identityService, goal.GoalInfo.GoalApiInfo!.Method,
            requestMemoryStack);

        if (error != null)
        {
            await ShowError(container, error);

            continue;
        }

        error = await engine.RunGoal(goal);
        if (error != null && error is not IErrorHandled)
        {
            await ShowError(container, error);
            continue;
        }

        var streamFactory = container.GetInstance<IOutputStreamFactory>();
        var stream = streamFactory.CreateHandler().Stream;

        stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(resp.OutputStream);


        return null;
    }


    public IRouting? GetRouting(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        foreach (var route in webserverInfo.Routings)
            if (Regex.IsMatch(path, route.Path))
                return route;

        return null;
    }

    private IError? ProcessStaticFileRequest(HttpListenerContext httpContext)
    {
        var requestedFile = httpContext.Request.Url?.LocalPath.AdjustPathToOs();
        if (string.IsNullOrEmpty(requestedFile)) return new Error("Path empty", StatusCode: 404);

        ((ServiceContainer)container).RegisterForPLangWebserver(goalStep.Goal.AbsoluteAppStartupFolderPath,
            goalStep.Goal.RelativeGoalFolderPath, httpContext);
        var fileSystem = container.GetInstance<IPLangFileSystem>();


        var filePath = Path.Join(fileSystem.GoalsPath!, requestedFile);
        var fileExtension = Path.GetExtension(filePath);
        var mimeType = MimeTypeHelper.GetWebMimeType(fileExtension);

        if (mimeType != null && fileSystem.File.Exists(filePath))
        {
            var buffer = fileSystem.File.ReadAllBytes(filePath);
            httpContext.Response.ContentLength64 = buffer.Length;

            httpContext.Response.ContentType = mimeType;
            httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        else
        {
            return new Error("File not found", StatusCode: 404);
        }


        return null;
    }


    private async Task<IError?> ParseRequest(HttpListenerContext? context, IPLangIdentityService identityService,
        string? method, MemoryStack memoryStack)
    {
        if (context == null) return new Error("context is empty");

        var request = context.Request;
        var contentType = request.ContentType ?? "application/json";
        if (string.IsNullOrWhiteSpace(contentType)) throw new HttpRequestException("ContentType is missing");
        if (method == null) return new Error("Could not map request to api");

        if (request.HttpMethod != method)
            return new Error($"Only {method} is supported. You sent {request.HttpMethod}");
        var body = "";
        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
        {
            body = await reader.ReadToEndAsync();
        }

        await VerifySignature(request, body, memoryStack);

        var nvc = request.QueryString;
        if (contentType.StartsWith("application/json") && !string.IsNullOrEmpty(body))
        {
            var obj = JsonConvert.DeserializeObject(body) as JObject;

            if (nvc.AllKeys.Length > 0)
            {
                if (obj == null) obj = new JObject();
                foreach (var key in nvc.AllKeys)
                {
                    if (key == null) continue;
                    obj.Add(key, nvc[key]);
                }
            }

            memoryStack.Put("request", obj);


            return null;
        }

        if (contentType.StartsWith("application/x-www-form-urlencoded") && !string.IsNullOrEmpty(body))
        {
            var formData = HttpUtility.ParseQueryString(body);
            if (nvc.AllKeys.Length > 0)
            {
                if (formData == null)
                    formData = nvc;
                else
                    foreach (var key in nvc.AllKeys)
                    {
                        if (key == null) continue;
                        formData.Add(key, nvc[key]);
                    }
            }

            memoryStack.Put("request", formData);
            return null;
        }


        memoryStack.Put("request", nvc);
        return null;

        /*
         * @ingig - Not really sure what is happening here, so decide to remove it for now.
        if (request.HttpMethod == method && contentType.StartsWith("multipart/form-data"))
        {
            var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType), 70);
            var multipart = new MultipartReader(boundary, request.InputStream);

            while (true)
            {
                var section = await multipart.ReadNextSectionAsync();
                if (section == null) break;

                var formData = section.AsFormDataSection();
                memoryStack.Put(formData.Name, await formData.GetValueAsync());
            }
        }
        */
    }

    public async Task VerifySignature(HttpListenerRequest request, string body, MemoryStack memoryStack)
    {
        if (request.Headers.Get("X-Signature") == null ||
            request.Headers.Get("X-Signature-Created") == null ||
            request.Headers.Get("X-Signature-Nonce") == null ||
            request.Headers.Get("X-Signature-Public-Key") == null ||
            request.Headers.Get("X-Signature-Contract") == null
           ) return;

        var validationHeaders = new Dictionary<string, object>();
        validationHeaders.Add("X-Signature", request.Headers.Get("X-Signature")!);
        validationHeaders.Add("X-Signature-Created", request.Headers.Get("X-Signature-Created")!);
        validationHeaders.Add("X-Signature-Nonce", request.Headers.Get("X-Signature-Nonce")!);
        validationHeaders.Add("X-Signature-Public-Key", request.Headers.Get("X-Signature-Public-Key")!);
        validationHeaders.Add("X-Signature-Contract", request.Headers.Get("X-Signature-Contract") ?? "C0");

        var url = request.Url?.PathAndQuery ?? "";

        var identies = await signingService.VerifySignature(body, request.HttpMethod, url, validationHeaders);
        if (identies == null) return;
        foreach (var identity in identies) memoryStack.Put(identity.Key, identity.Value);
    }
}