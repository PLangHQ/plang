using NBitcoin.Secp256k1;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;
using PLang.Services.OutputStream.Transformers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text;

namespace PLang.Modules.CommunicationModule;

/// <summary>
/// Module for bidirectional communication via named pipes, TCP, and other transports.
/// Supports channel registration for unified I/O routing.
/// </summary>
public class Program : BaseProgram, IDisposable
{
	private readonly ConcurrentDictionary<string, PipeConnection> _connections = new();
	private readonly ConcurrentDictionary<string, CancellationTokenSource> _serverCancellations = new();
	private bool _disposed;

	/// <summary>
	/// Listen for incoming pipe connections. When a client connects, registers the connection
	/// as a channel and starts listening for messages. Supports multiple simultaneous connections.
	/// </summary>
	/// <param name="pipeName">Name of the pipe to listen on</param>
	/// <param name="channelName">Name to register the connection as in the output channel system</param>
	/// <param name="onMessageGoal">Goal to call when a message is received. Receives 'data' and 'connection' parameters</param>
	/// <param name="onConnectGoal">Optional goal to call when a client connects. Receives 'connection' parameter</param>
	/// <param name="onDisconnectGoal">Optional goal to call when client disconnects. Receives 'connection' parameter</param>
	/// <param name="delimiter">Message delimiter, defaults to newline</param>
	[Description("Listen for connection on pipe, register as channel, handle messages")]
	public async Task ListenForPipeConnection(
		string pipeName,
		string channelName,
		GoalToCallInfo onMessageGoal,
		GoalToCallInfo? onConnectGoal = null,
		GoalToCallInfo? onDisconnectGoal = null,
		string delimiter = "\n")
	{
		var cts = new CancellationTokenSource();
		_serverCancellations[pipeName] = cts;

		_ = Task.Run(async () =>
		{
			while (!cts.Token.IsCancellationRequested && !_disposed)
			{
				try
				{
					var server = new NamedPipeServerStream(
						pipeName,
						PipeDirection.InOut,
						NamedPipeServerStream.MaxAllowedServerInstances,
						PipeTransmissionMode.Byte,
						PipeOptions.Asynchronous);

					await server.WaitForConnectionAsync(cts.Token);

					var connectionId = Guid.NewGuid().ToString();
					var connection = new PipeConnection(connectionId, server, pipeName, isServer: true);
					_connections[connectionId] = connection;

					// Register as channel for output routing
					var sink = new PipeOutputSink(connection, delimiter, channelName);
					context.Output.User.RegisterChannel(channelName, sink);

					// Call onConnect goal if provided
					if (onConnectGoal != null)
					{
						var connectParams = new Dictionary<string, object?>
						{
							{ "connection", connection }
						};
						var connectGoal = new GoalToCallInfo(onConnectGoal.Name, connectParams)
						{
							Path = onConnectGoal.Path
						};
						await engine.RunGoal(connectGoal, goal, context);
					}

					// Start listening for messages (don't await - let it run independently)
					_ = ListenForMessages(connection, delimiter, onMessageGoal, onDisconnectGoal, channelName);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex) when (!_disposed)
				{
					logger.LogError(ex, "Error in pipe server for {PipeName}", pipeName);
					await Task.Delay(1000, cts.Token); // Brief delay before retry
				}
			}
		});

		KeepAlive(cts, $"PipeServer_{pipeName}");

	}

	/// <summary>
	/// Connect to an existing pipe server. Registers the connection as a channel
	/// and starts listening for messages.
	/// </summary>
	/// <param name="pipeName">Name of the pipe to connect to</param>
	/// <param name="channelName">Name to register the connection as in the output channel system</param>
	/// <param name="onMessageGoal">Goal to call when a message is received. Receives 'data' and 'connection' parameters</param>
	/// <param name="onConnectGoal">Optional goal to call when connection is established. Receives 'connection' parameter</param>
	/// <param name="onDisconnectGoal">Optional goal to call when disconnected. Receives 'connection' parameter</param>
	/// <param name="delimiter">Message delimiter, defaults to newline</param>
	/// <param name="timeoutMs">Connection timeout in milliseconds, defaults to 5000</param>
	[Description("Connect to pipe, register as channel, handle messages")]
	public async Task<PipeConnection> ConnectToPipe(
		string pipeName,
		string channelName,
		GoalToCallInfo onMessageGoal,
		GoalToCallInfo? onConnectGoal = null,
		GoalToCallInfo? onDisconnectGoal = null,
		string delimiter = "\n",
		int timeoutMs = 5000, string serverName = ".")
	{
		var client = new NamedPipeClientStream(
			serverName,
			pipeName,
			PipeDirection.InOut,
			PipeOptions.Asynchronous);

		await client.ConnectAsync(timeoutMs);

		var connectionId = Guid.NewGuid().ToString();
		var connection = new PipeConnection(connectionId, client, pipeName, isServer: false);
		_connections[connectionId] = connection;

		// Register as channel for output routing
		var sink = new PipeOutputSink(connection, delimiter, channelName);
		context.Output.Service.RegisterChannel(channelName, sink);

		// Call onConnect goal if provided
		if (onConnectGoal != null)
		{
			var connectParams = new Dictionary<string, object?>
			{
				{ "connection", connection }
			};
			var connectGoal = new GoalToCallInfo(onConnectGoal.Name, connectParams)
			{
				Path = onConnectGoal.Path
			};
			await engine.RunGoal(connectGoal, goal, context);
		}

		// Start listening for messages
		_ = Task.Run(async () =>
		{
			try
			{
				await ListenForMessages(connection, delimiter, onMessageGoal, onDisconnectGoal, channelName);
			}
			catch (Exception ex) when (!_disposed)
			{
				logger.LogError(ex, "Error in pipe client for {PipeName}", pipeName);
			}
		});

		KeepAlive(client, $"PipeClient_{pipeName}");

		return connection;
	}

	/// <summary>
	/// Close a connection.
	/// </summary>
	/// <param name="connection">The connection to close</param>
	[Description("Close connection")]
	public async Task CloseConnection(PipeConnection connection)
	{
		if (_connections.TryRemove(connection.Id, out _))
		{
			await connection.DisposeAsync();
		}
	}

	private async Task ListenForMessages(
		PipeConnection connection,
		string delimiter,
		GoalToCallInfo onMessageGoal,
		GoalToCallInfo? onDisconnectGoal,
		string channelName)
	{
		var buffer = new StringBuilder();
		var byteBuffer = new byte[4096];

		try
		{
			while (connection.IsConnected && !_disposed)
			{
				var bytesRead = await connection.ReadAsync(byteBuffer);
				if (bytesRead == 0)
				{
					// Connection closed
					break;
				}

				var text = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
				buffer.Append(text);

				// Process complete messages
				var content = buffer.ToString();
				int delimiterIndex;
				while ((delimiterIndex = content.IndexOf(delimiter, StringComparison.Ordinal)) >= 0)
				{
					var message = content[..delimiterIndex];
					content = content[(delimiterIndex + delimiter.Length)..];
					buffer.Clear();
					buffer.Append(content);

					if (!string.IsNullOrWhiteSpace(message))
					{
						var messageParams = new Dictionary<string, object?>
						{
							{ "message", message },
							{ "connection", connection }
						};
						var messageGoal = new GoalToCallInfo(onMessageGoal.Name, messageParams) {  Path = onMessageGoal.Path };

						// Fire and don't block reading
						_ = Task.Run(async () =>
						{
							try
							{
								await engine.RunGoal(messageGoal, goal, context);
							}
							catch (Exception ex)
							{
								logger.LogError(ex, "Error handling message in goal {GoalName}", onMessageGoal.Name);
							}
						});
					}
				}
			}
		}
		catch (IOException)
		{
			// Pipe disconnected
		}
		catch (ObjectDisposedException)
		{
			// Already disposed
		}
		finally
		{
			// Unregister channel
			context.Output.GetActor(connection.Actor).UnregisterChannel(channelName);

			// Call onDisconnect goal if provided
			if (onDisconnectGoal != null)
			{
				try
				{
					var disconnectParams = new Dictionary<string, object?>
					{
						{ "connection", connection }
					};
					var disconnectGoal = new GoalToCallInfo(onDisconnectGoal.Name, disconnectParams)
					{
						Path = onDisconnectGoal.Path
					};
					await engine.RunGoal(disconnectGoal, goal, context);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error in disconnect handler");
				}
			}

			_connections.TryRemove(connection.Id, out _);
		}
	}


	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		foreach (var cts in _serverCancellations.Values)
		{
			cts.Cancel();
			cts.Dispose();
		}
		_serverCancellations.Clear();

		foreach (var connection in _connections.Values)
		{
			connection.DisposeAsync().AsTask().Wait(1000);
		}
		_connections.Clear();
	}
}

/// <summary>
/// Represents a pipe connection (either server or client side)
/// </summary>
public class PipeConnection : IAsyncDisposable
{
	private readonly Stream _pipeStream;
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private bool _disposed;

	public string Id { get; }
	public string PipeName { get; }
	public string Actor { get; }
	public bool IsServer { get; }
	public bool IsConnected => !_disposed && (_pipeStream is NamedPipeServerStream server ? server.IsConnected :
											   _pipeStream is NamedPipeClientStream client && client.IsConnected);

	public PipeConnection(string id, NamedPipeServerStream pipe, string pipeName, bool isServer)
	{
		Id = id;
		_pipeStream = pipe;
		PipeName = pipeName;
		IsServer = isServer;
		Actor = (isServer) ? "user" : "service";
	}

	public PipeConnection(string id, NamedPipeClientStream pipe, string pipeName, bool isServer)
	{
		Id = id;
		_pipeStream = pipe;
		PipeName = pipeName;
		IsServer = isServer;
		Actor = (isServer) ? "user" : "service";
	}

	public async Task<int> ReadAsync(byte[] buffer)
	{
		if (_disposed) return 0;
		return await _pipeStream.ReadAsync(buffer);
	}

	public async Task SendAsync(object data, string delimiter)
	{
		if (_disposed) throw new ObjectDisposedException(nameof(PipeConnection));


		var message = data is string str ? str : System.Text.Json.JsonSerializer.Serialize(data);
		var bytes = Encoding.UTF8.GetBytes(message + delimiter);

		await _writeLock.WaitAsync();
		try
		{
			await _pipeStream.WriteAsync(bytes);
			await _pipeStream.FlushAsync();
		}
		finally
		{
			_writeLock.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;
		_disposed = true;

		try
		{
			await _pipeStream.DisposeAsync();
		}
		catch { }

		_writeLock.Dispose();
	}
}

/// <summary>
/// IOutputSink implementation for pipe connections
/// </summary>
public class PipeOutputSink : IOutputSink
{
	private readonly PipeConnection _connection;
	private readonly string _delimiter;

	public string Id { get; }
	public bool IsStateful => true;

	public PipeOutputSink(PipeConnection connection, string delimiter, string channelName)
	{
		_connection = connection;
		_delimiter = delimiter;
		Id = channelName;
	}

	public async Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
	{
		try
		{
			if (!_connection.IsConnected)
			{
				return new Error("Pipe connection is not connected", "PipeDisconnected");
			}

			await _connection.SendAsync(message, _delimiter);
			return null;
		}
		catch (Exception ex)
		{
			return new Error(ex.Message, "PipeSendError", Exception: ex);
		}
	}

	public async Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default)
	{
		// For now, asking through pipe sends the question and waits for response
		// This would need more complex request/response correlation for real use
		try
		{
			if (!_connection.IsConnected)
			{
				return (null, new Error("Pipe connection is not connected", "PipeDisconnected"));
			}

			await _connection.SendAsync(message, _delimiter);

			// TODO: Implement response waiting with correlation ID
			// For debugger use case, the response comes back through onMessageGoal
			return (null, null);
		}
		catch (Exception ex)
		{
			return (null, new Error(ex.Message, "PipeAskError", Exception: ex));
		}
	}
}