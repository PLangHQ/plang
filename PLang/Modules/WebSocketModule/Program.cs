using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Utils;
using System;
using System.Net.WebSockets;
using System.Text;

namespace PLang.Modules.WebSocketModule
{
	public class Program : BaseProgram, IDisposable
	{
		private readonly ProgramFactory programFactory;
		private bool disposed;

		private static readonly string WebsocketClient = "WebsocketClient";

		public Program(ProgramFactory programFactory)
		{
			this.programFactory = programFactory;
		}

		public record WebsocketConnection(string name, ClientWebSocket connection);

		public async Task<(object?, IError?)> Connect(string url, string? name = null, Dictionary<string, object>? headers = null,
				GoalToCallInfo? onMessage = null, GoalToCallInfo? onConnected = null, GoalToCallInfo? onClose = null, GoalToCallInfo? onError = null, int bufferSize = 8192)
		{
			ExceptionHelper.NotImplemented();
			if (string.IsNullOrEmpty(onMessage))
			{
				return (null, new ProgramError("You must defined a goal to call on new message", goalStep, function,
					FixSuggestion: @"Add 'on message call ProcessMessage' to you step, e.g. connect to websocket %url%, on message call ProcessMessage"));
			}
			if (bufferSize <= 0) bufferSize = 8192;

			if (string.IsNullOrEmpty(name))
			{
				name = url;
			}

			var caller = programFactory.GetProgram<CallGoalModule.Program>(goalStep);

			ClientWebSocket _socket = new();
			await _socket.ConnectAsync(new Uri(url), CancellationToken.None);
			if (onConnected != null)
			{
				await caller.RunGoal(onConnected);

			}
#pragma warning disable CS4014
			Task.Run(async () =>
			{
				var buffer = new byte[8192];
				while (_socket.State == WebSocketState.Open)
				{
					var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

					onMessage.Parameters.Add("data", json);

					var (returns, error) = await caller.RunGoal(onMessage);
					if (error != null)
					{
						await caller.RunGoal(onError);
					}

				}

				if (onClose != null && _socket.State == WebSocketState.Closed)
				{
					await caller.RunGoal(onClose);
				}
				if (onError != null && _socket.State == WebSocketState.Aborted)
				{
					await caller.RunGoal(onError);
				}
			});
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

			List<WebsocketConnection>? connections = new();
			if (appContext.ContainsKey(WebsocketClient))
			{
				connections = appContext[WebsocketClient] as List<WebsocketConnection>;
				if (connections == null) connections = new();
			}
			connections.Add(new WebsocketConnection(name, _socket));

			appContext.AddOrReplace(WebsocketClient, connections);
			return (_socket, null);
		}

		public async Task<IError?> Send(object message, string? name = null)
		{
			if (!appContext.ContainsKey(WebsocketClient)) 
			{
				return new ProgramError("Not connection available to a websocket server", goalStep, function);
			}
			ExceptionHelper.NotImplemented();

			return null;
		}

		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}
			if (appContext.ContainsKey(WebsocketClient))
			{
				var connections = appContext[WebsocketClient] as List<WebsocketConnection>;
				if (connections != null)
				{
					foreach (var connection in connections)
					{
						connection.connection.Dispose();
					}
				}
			}

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
