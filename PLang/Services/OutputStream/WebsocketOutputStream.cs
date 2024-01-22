using PLang.Interfaces;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Net.WebSockets;

namespace PLang.Services.OutputStream
{
	public class WebsocketOutputStream : IOutputStream
	{
		private readonly WebSocket webSocket;
		private readonly IPLangSigningService signingService;

		public WebsocketOutputStream(WebSocket webSocket, IPLangSigningService signingService)
		{			
			this.webSocket = webSocket;
			this.signingService = signingService;
			Stream = new MemoryStream();
			ErrorStream = new MemoryStream();
		}

		public Stream Stream { get; private set; }
		public Stream ErrorStream { get; private set; }

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
			if (obj == null) { return; }

			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(obj.ToString()!);
			await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

		}

		public async Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200)
		{
			await Write(obj, type, statusCode);
		}
	}
}
