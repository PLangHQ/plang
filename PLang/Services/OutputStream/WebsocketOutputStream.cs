using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SigningService;
using PLang.Utils;
using System;
using System.Net.WebSockets;
using System.Text;
using static PLang.Modules.OutputModule.Program;
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
		
		}

		public Stream Stream { get; private set; }
		public Stream ErrorStream { get; private set; }
		public GoalStep Step { get; set; }

		public string Output => "text";
		public bool IsStateful => false;

		public bool IsFlushed { get; set; }
		public IEngine Engine { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			throw new NotImplementedException("WebsocketOutputStream.Ask");

			using var ms = new MemoryStream();
			var jsonOutputStream = new JsonOutputStream(ms, Encoding.UTF8, IsStateful);
			(_, error) = await jsonOutputStream.Ask(askOptions, callback, error);
			if (error != null) return (null, error);

			await webSocket.SendAsync(ms.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
			IsFlushed = true;
			return (null, null);
		}

		public string Read()
		{
			throw new NotImplementedException();
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			if (obj == null) { return; }

			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(obj.ToString()!);
			await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
			IsFlushed = true;
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
