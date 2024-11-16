using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Utils;

namespace PLang.Services.OutputStream;

public class JsonOutputStream : IOutputStream, IDisposable
{
    private readonly HttpListenerContext httpContext;
    private readonly MemoryStream memoryStream;

    public JsonOutputStream(HttpListenerContext httpContext)
    {
        this.httpContext = httpContext;
        memoryStream = new MemoryStream();
        httpContext.Response.ContentType = "application/json";
    }

    public void Dispose()
    {
        httpContext.Response.OutputStream.Close();
        memoryStream.Dispose();
    }

    public Stream Stream => memoryStream;
    public Stream ErrorStream => memoryStream;

    public string ContentType => "application/json";

    public async Task<string> Ask(string text, string type, int statusCode = 400)
    {
        httpContext.Response.SendChunked = true;
        httpContext.Response.StatusCode = 400;

        using (var writer = new StreamWriter(httpContext.Response.OutputStream,
                   httpContext.Response.ContentEncoding ?? Encoding.UTF8))
        {
            if (text != null)
            {
                var content = text;
                if (!JsonHelper.IsJson(content)) content = JsonConvert.SerializeObject(content);

                await writer.WriteAsync(content);
            }

            await writer.FlushAsync();
        }

        return "";
    }

    public string Read()
    {
        return "";
    }

    public async Task Write(object? obj, string type, int httpStatusCode = 200)
    {
        httpContext.Response.StatusCode = httpStatusCode;
        httpContext.Response.StatusDescription = type;

        var content = GetAsString(obj);
        if (content == null) return;

        var buffer = Encoding.UTF8.GetBytes(content);
        memoryStream.Write(buffer, 0, buffer.Length);
        //httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    public async Task WriteToBuffer(object? obj, string type, int httpStatusCode = 200)
    {
        httpContext.Response.StatusCode = httpStatusCode;
        httpContext.Response.StatusDescription = type;
        httpContext.Response.SendChunked = true;

        var content = GetAsString(obj);
        if (content == null) return;

        var buffer = Encoding.UTF8.GetBytes(content);
        httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    private string? GetAsString(object? obj)
    {
        if (obj == null) return null;

        if (obj is JValue || obj is JObject || obj is JArray) return obj.ToString();
        if (obj is IError) return ((IError)obj).ToFormat("json").ToString();

        var content = obj.ToString()!;
        if (!JsonHelper.IsJson(content)) content = JsonConvert.SerializeObject(obj);

        return content;
    }
}