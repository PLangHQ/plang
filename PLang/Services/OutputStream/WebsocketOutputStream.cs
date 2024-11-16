using System.Net.WebSockets;
using System.Text;
using PLang.Services.SigningService;

namespace PLang.Services.OutputStream;

public class WebsocketOutputStream : IOutputStream
{
    private readonly IPLangSigningService signingService;
    private readonly WebSocket webSocket;

    public WebsocketOutputStream(WebSocket webSocket, IPLangSigningService signingService)
    {
        this.webSocket = webSocket;
        this.signingService = signingService;
        Stream = new MemoryStream();
        ErrorStream = new MemoryStream();
    }

    public Stream Stream { get; }
    public Stream ErrorStream { get; }

    public string ContentType => "text/plain";

    public async Task<string> Ask(string text, string type = "text", int statusCode = 200)
    {
        return "";
    }

    public string Read()
    {
        return "";
    }

    public async Task Write(object? obj, string type = "text", int statusCode = 200)
    {
        if (obj == null) return;

        var buffer = Encoding.UTF8.GetBytes(obj.ToString()!);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    public async Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200)
    {
        await Write(obj, type, statusCode);
    }
}