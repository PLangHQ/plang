using PLang.Interfaces;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Net.WebSockets;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class WebsocketOutputStream : IOutputStream, IDisposable
	{
		private readonly WebSocket webSocket;
		private readonly IPLangSigningService signingService;
		private bool disposed;

		public WebsocketOutputStream(WebSocket webSocket, IPLangSigningService signingService)
		{			
			this.webSocket = webSocket;
			this.signingService = signingService;
			Stream = new MemoryStream();
			ErrorStream = new MemoryStream();
		}

		public Stream Stream { get; private set; }
		public Stream ErrorStream { get; private set; }

		public string Output => "text";

		public async Task<string> Ask(string text, string type = "text", int statusCode = 200, Dictionary<string, object>? parameters = null, Callback? callback = null)
		{
			throw new NotImplementedException();
		}

		public string Read()
		{
			throw new NotImplementedException();
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

		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}
			Stream.Dispose();
			ErrorStream.Dispose();

			this.disposed = true;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}
	}
}
