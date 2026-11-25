using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;

namespace PLang.Modules.IrohModule
{
	[Description("Talk to other plang apps over iroh, peer to peer by key and not by ip. Create and print the endpoint key (`create iroh key`), listen for signed commands (`start listening`), connect to a peer by its key (`connect to`) and tell it to run a goal (`tell %peer% to Update`). Listen and connect can bind a fixed UDP port (`on udp port 7000`)")]
	public class Program(IPseudoRuntime pseudoRuntime, IPLangSigningService signingService) : BaseProgram
	{
		public const string Alpn = "plang/tell/1";
		private const string SignatureFileExtension = ".signature";
		private const int MaxSignatureFileBytes = 16 * 1024;
		private const string SecretKeyPrefix = "IrohSecretKey_";
		private const int MaxHeaderBytes = 1024 * 1024;
		private const int MaxAnswerBytes = 16 * 1024 * 1024;
		private const int ChunkTimeoutMilliseconds = 30000;
		private const long DefaultMaxFileBytes = 512L * 1024 * 1024;
		private const string MaxFileBytesSetting = "IrohMaxFileBytes";
		private const string IncomingFolder = "/.tmp/iroh";

		private static readonly ConcurrentDictionary<string, IrohEndpoint> Endpoints = new();
		internal static int EndpointCount => Endpoints.Count;

		public record IrohKey(string Name, string EndpointId);
		public record IrohListener(string Name, string EndpointId, string Ticket);
		/// <summary>
		/// One open connection to a peer, made with connect or accepted by listen. Id names the connection while it is open,
		/// so an app can keep it (e.g. in a table) and tell the peer in a later run: `tell %peerId% to Ingest`.
		/// </summary>
		public record IrohPeer(string Target, string? EndpointId, [property: JsonIgnore] IrohConnection Connection) : IHoldsDisposable
		{
			public string Id => Connection.Id;

			[JsonIgnore]
			public IDisposable Disposable => Connection;
		}

		[Description("Create the iroh endpoint key with a name, saved in settings, and return its endpoint id to print or share. When the key exists it is returned unchanged, unless replace is true. The secret is never returned")]
		[Example("create iroh key, write to %key%", "name=\"default\"")]
		[Example("create iroh key 'kiosk', replace, write to %key%", "name=\"kiosk\", replace=true")]
		public async Task<IrohKey> CreateKey(string name = "default", bool replace = false)
		{
			string settingKey = SecretKeyPrefix + name;
			string? secret = settings.GetOrDefault<string?>(typeof(Program), settingKey, null);
			if (replace || string.IsNullOrEmpty(secret))
			{
				secret = IrohKeys.Generate().Secret;
				settings.Set(typeof(Program), settingKey, secret);
			}
			return new IrohKey(name, IrohKeys.IdFromSecret(secret));
		}

		[Description("Get the endpoint id of a named iroh key. The key is created when it does not exist")]
		[Example("get iroh key, write to %key%", "name=\"default\"")]
		public async Task<IrohKey> GetKey(string name = "default")
		{
			return await CreateKey(name, false);
		}

		[Description("Start listening for iroh peers. Every incoming command must be signed, an unsigned or badly signed one is declined. The goal in onIncoming is called first with %Identity% (the signer), %goalName%, %parameters%, %EndpointId% and %peer% (the connection the command came on) (and %fileName% and %fileSize% when a file comes with the command, the file is not received until this goal has not failed), and it decides if the identity is allowed by throwing an error to decline. When it does not fail the requested goal runs, also with %peer% and %Identity%, and what it returns goes back to the caller. %peer% can be told back on the same connection while it is open (`tell %peer% to Ingest`); keep %peer.Id% to tell it in a later run. onClosed is called with %peer% when a connection of a peer closes. onIncoming is required")]
		[Example("start listening, on incoming call Allow, write to %listener%", "onIncoming=Allow")]
		[Example("listen on iroh 'kiosk', on incoming call Allow", "keyName=\"kiosk\", onIncoming=Allow")]
		[Example("start listening on udp port 7000, on incoming call Allow", "onIncoming=Allow, port=7000")]
		[Example("start listening on udp port 47000, on incoming call Allow, on closed call PeerGone, write to %listener%", "onIncoming=Allow, onClosed=PeerGone, port=47000")]
		public async Task<(IrohListener?, IError?)> Listen(GoalToCallInfo? onIncoming = null, string keyName = "default", bool useRelay = true,
			[Description(PortDescription)] int port = 0, GoalToCallInfo? onClosed = null)
		{
			if (onIncoming == null || string.IsNullOrEmpty(onIncoming.Name))
			{
				return (null, new ProgramError("A goal that validates who may tell this app what to do is required", goalStep, function,
					FixSuggestion: "Add 'on incoming call Allow' to the step, e.g. `- start listening, on incoming call Allow`. The goal gets %Identity% and throws an error to decline"));
			}

			var (endpoint, error) = GetEndpoint(keyName, useRelay, port);
			if (error != null) return (null, error);

			var listener = new IrohListener(keyName, endpoint!.Id, endpoint.Ticket());
			RemoveOldReceivedFiles(fileSystem.RootDirectory);

			var engineRef = engine;
			var accessorRef = contextAccessor;
			var rootPath = fileSystem.RootDirectory;
			Action<IrohConnection>? connectionClosed = onClosed == null ? null : connection =>
				RunClosed(onClosed, new IrohPeer(connection.Id, null, connection), engineRef, accessorRef, rootPath).GetAwaiter().GetResult();
			_ = Task.Factory.StartNew(() => endpoint.Serve(
				(stream, connection) => HandleStream(stream, connection, endpoint, onIncoming, engineRef, accessorRef, rootPath),
				ex => logger?.LogWarning("iroh: {Message}", ex.Message), connectionClosed), TaskCreationOptions.LongRunning);

			KeepAlive(endpoint, "iroh-listen-" + keyName);
			return (listener, null);
		}

		[Description("Connect to a peer by its endpoint id (or a full ticket). Returns the peer to use in tell. Without onClosed and onIncoming the connection closes when this goal ends, or when the calling goal ends if this goal returns it. With onClosed or onIncoming it stays open until the peer or the network closes it, and onClosed is called then, use it to connect again. With onIncoming the peer can also tell this app on the same connection: every command must be signed, onIncoming is called first and declines by throwing an error, exactly as in `start listening`, then the requested goal runs")]
		[Example("connect to %kioskKey%, on closed call Redial, write to %agent%", "target=%kioskKey%, onClosed=Redial")]
		[Example("connect to iroh peer %id%, key 'admin', write to %agent%", "target=%id%, keyName=\"admin\"")]
		[Example("connect to %agentEndpoint%, on udp port 47001, write to %agent%", "target=%agentEndpoint%, port=47001")]
		[Example("connect to %agentEndpoint%, on incoming call Allow, on closed call Reconnect, write to %agent%", "target=%agentEndpoint%, onIncoming=Allow, onClosed=Reconnect")]
		public async Task<(IrohPeer?, IError?)> Connect(string target, GoalToCallInfo? onClosed = null, string keyName = "default", bool useRelay = true,
			[Description(PortDescription)] int port = 0, GoalToCallInfo? onIncoming = null)
		{
			var (endpoint, error) = GetEndpoint(keyName, useRelay, port);
			if (error != null) return (null, error);

			IrohConnection connection;
			try
			{
				connection = await Task.Run(() => endpoint!.Connect(target));
			}
			catch (Exception ex)
			{
				return (null, new ProgramError(ex.Message, goalStep, function));
			}

			target = target.Trim().Trim('﻿').Trim();
			string? endpointId = IrohEndpoint.IsTicket(target) ? null : target;

			var peer = new IrohPeer(target, endpointId, connection);
			if (onClosed == null && onIncoming == null)
			{
				// The connection closes when this goal ends, or, when the goal returns the peer, when the calling goal ends;
				// the listening side then frees what it holds for it.
				contextAccessor.Current.CallStack.AddDisposable(connection);
				return (peer, null);
			}

			var engineRef = engine;
			var accessorRef = contextAccessor;
			var rootPath = fileSystem.RootDirectory;
			if (onIncoming != null)
			{
				// The peer opens streams on this connection as on a listening endpoint. A thread waits for them until the
				// connection closes; while it waits, a local disconnect takes effect when the peer or the network closes it.
				// The app stays alive while it can be told something, as with `start listening`.
				string keepAliveKind = "iroh-listen-connection-" + connection.Id;
				KeepAlive(connection, keepAliveKind, "iroh connection to " + target);
				_ = Task.Factory.StartNew(async () =>
				{
					connection.ServeStreams(
						stream => HandleStream(stream, connection, endpoint!, onIncoming, engineRef, accessorRef, rootPath),
						ex => logger?.LogWarning("iroh: {Message}", ex.Message));
					// onClosed runs before the app may end, so a goal that connects again keeps it alive
					try
					{
						if (onClosed != null) await RunClosed(onClosed, peer, engineRef, accessorRef, rootPath);
					}
					finally
					{
						RemoveKeepAlive(connection, keepAliveKind);
					}
				}, TaskCreationOptions.LongRunning);
			}
			else if (onClosed != null)
			{
				_ = Task.Factory.StartNew(async () =>
				{
					connection.WaitUntilClosed();
					connection.Dispose();
					await RunClosed(onClosed, peer, engineRef, accessorRef, rootPath);
				}, TaskCreationOptions.LongRunning);
			}

			return (peer, null);
		}

		private async Task RunClosed(GoalToCallInfo onClosed, IrohPeer peer, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			var parameters = new Dictionary<string, object?>(onClosed.Parameters) { ["peer"] = peer };
			// The peer or the network closed it: a run from outside, with nobody's identity.
			using var origin = RunOrigin.External(null);
			await pseudoRuntime.RunGoal(engineRef, accessorRef, rootPath, new GoalToCallInfo(onClosed.Name, parameters));
		}

		/// <summary>
		/// The peer a step names: a peer from connect or from an incoming command (%peer%), or the id of an open connection
		/// (%peer.Id%, kept from an earlier run). Null when the connection is not open (any more).
		/// </summary>
		private static IrohPeer? ResolvePeer(object? value)
		{
			switch (value)
			{
				case IrohPeer peer:
					return peer;
				case ObjectValue ov:
					return ResolvePeer(ov.Value);
				case JObject json:
					return ResolvePeer(json["Id"]?.ToString() ?? json["id"]?.ToString());
				case string id when !string.IsNullOrWhiteSpace(id):
					id = id.Trim();
					foreach (var endpoint in Endpoints.Values)
					{
						var connection = endpoint.FindConnection(id);
						if (connection != null) return new IrohPeer(id, null, connection);
					}
					return null;
				default:
					return null;
			}
		}

		[Description("Close the connection to a peer (%peer% from connect or of an incoming command). A connection without onClosed and onIncoming also closes by itself when the goal that holds it ends")]
		[Example("disconnect from %agent%", "peer=%agent%")]
		[Example("close connection %agent%", "peer=%agent%")]
		public async Task Disconnect(IrohPeer peer)
		{
			peer?.Connection?.Dispose();
		}

		[Description("Close the connection with this id (%peer.Id%, kept from an earlier run)")]
		[Example("disconnect peer id %peerId%", "peerId=%peerId%")]
		public async Task DisconnectById(string peerId)
		{
			ResolvePeer(peerId)?.Connection?.Dispose();
		}

		[Description("Tell a connected peer to run a goal in its app. The peer is one from connect, or %peer% of an incoming command (tells it back on the same connection). The command is signed with your identity, the peer decides if it will run it. Returns what the remote goal returns")]
		[Example("tell %agent% to Update, version: \"1\", write to %result%", "goalName=\"Update\", parameters={\"version\":\"1\"}")]
		[Example("tell %agent% to RestartKiosk, timeout 30 seconds, write to %result%", "goalName=\"RestartKiosk\", timeoutInSeconds=30")]
		[Example("tell %agent% to Update, file: %package%, write to %result%", "goalName=\"Update\", file=%package%")]
		[Example("tell %peer% to Ingest, seq: %seq%, write to %result%", "goalName=\"Ingest\", parameters={\"seq\":\"%seq%\"}")]
		public async Task<(object?, IError?)> Tell(IrohPeer peer, string goalName, Dictionary<string, object?>? parameters = null,
			[Description("Path of a file to send with the command. Its size and sha256 are in the signed command, the peer checks them and gives the goal the saved path as %file%")] string? file = null,
			int timeoutInSeconds = 60, int expiresInSeconds = 120)
		{
			return await TellConnected(peer, goalName, parameters, file, timeoutInSeconds, expiresInSeconds);
		}

		[Description("Tell the peer of an open connection by its id (%peer.Id% of an incoming command or of connect, kept from an earlier run, e.g. in a table) to run a goal in its app. Same as tell otherwise. When the connection has closed the error key is PeerNotConnected")]
		[Example("tell peer id %peerId% to Ingest, seq: %seq%, write to %result%", "peerId=%peerId%, goalName=\"Ingest\", parameters={\"seq\":\"%seq%\"}")]
		[Example("tell connection %watcher.peerId% to Ingest, write to %result%", "peerId=%watcher.peerId%, goalName=\"Ingest\"")]
		public async Task<(object?, IError?)> TellById(string peerId, string goalName, Dictionary<string, object?>? parameters = null,
			[Description("Path of a file to send with the command. Its size and sha256 are in the signed command, the peer checks them and gives the goal the saved path as %file%")] string? file = null,
			int timeoutInSeconds = 60, int expiresInSeconds = 120)
		{
			return await TellConnected(ResolvePeer(peerId), goalName, parameters, file, timeoutInSeconds, expiresInSeconds);
		}

		private async Task<(object?, IError?)> TellConnected(IrohPeer? connected, string goalName, Dictionary<string, object?>? parameters, string? file,
			int timeoutInSeconds, int expiresInSeconds)
		{
			if (connected?.Connection == null || connected.Connection.IsDisposed)
			{
				return (null, new ProgramError("The peer is not connected", goalStep, function, Key: "PeerNotConnected",
					FixSuggestion: "Connect first, e.g. `- connect to %key%, write to %agent%`. A peer that connected to this app is told on its connection while it is open; when it has closed, the peer must connect again"));
			}
			if (string.IsNullOrWhiteSpace(goalName))
			{
				return (null, new ProgramError("goalName is empty, say which goal the peer should run", goalStep, function));
			}

			var payload = new JObject
			{
				["goal"] = goalName,
				["parameters"] = parameters == null ? new JObject() : JObject.FromObject(parameters),
				["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			};
			if (connected.EndpointId != null) payload["to"] = connected.EndpointId;

			string? filePath = null;
			if (!string.IsNullOrWhiteSpace(file))
			{
				filePath = GetPath(file);
				if (!File.Exists(filePath))
				{
					return (null, new ProgramError($"The file to send does not exist: {file}", goalStep, function));
				}
				var info = new FileInfo(filePath);
				payload["file"] = new JObject
				{
					["name"] = info.Name,
					["size"] = info.Length,
					["sha256"] = await Task.Run(() => HashFile(filePath))
				};

				// A file that has a signature file (<file>.signature, see `write to file ..., identity: default`) sends it along inside
				// the signed command; the receiver saves it next to the file so `validate file signature` works there.
				string sidecar = filePath + SignatureFileExtension;
				if (File.Exists(sidecar) && new FileInfo(sidecar).Length <= MaxSignatureFileBytes)
				{
					payload["fileSignature"] = await File.ReadAllTextAsync(sidecar);
				}
			}

			string payloadJson = payload.ToString(Formatting.None);
			var signature = await signingService.Sign(payloadJson, null, expiresInSeconds);
			string request = JsonConvert.SerializeObject(new TellRequest(JsonConvert.SerializeObject(signature), payloadJson));

			byte[] answerBytes;
			try
			{
				answerBytes = await Task.Run(() =>
				{
					using Stream? fileStream = filePath == null ? null : new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					return connected.Connection.Request(Encoding.UTF8.GetBytes(request), fileStream, ChunkTimeoutMilliseconds, timeoutInSeconds * 1000, MaxAnswerBytes);
				});
			}
			catch (Exception ex)
			{
				return (null, new ProgramError(ex.Message, goalStep, function,
					FixSuggestion: "The connection may be closed. Connect again and retry the tell"));
			}

			var answer = JsonConvert.DeserializeObject<TellAnswer>(Encoding.UTF8.GetString(answerBytes));
			if (answer == null) return (null, new ProgramError("The peer sent an answer that could not be read", goalStep, function));
			if (!answer.Ok) return (null, new ProgramError(answer.Error ?? "The peer declined", goalStep, function, StatusCode: answer.StatusCode));

			return (answer.Result, null);
		}

		[Description("Stop listening and close the endpoint of a named key")]
		[Example("stop iroh, key 'kiosk'", "keyName=\"kiosk\"")]
		public async Task Close(string keyName = "default")
		{
			if (Endpoints.TryRemove(keyName, out var endpoint))
			{
				RemoveKeepAlive(endpoint, "iroh-listen-" + keyName);
				endpoint.Dispose();
			}
		}

		/// <summary>
		/// Used by `drain app`: closes every connection of every endpoint, then the endpoints, so the peers see the
		/// connection drop and dial again (the next process listens on the same key). Returns how many of each were closed.
		/// </summary>
		internal static (int Endpoints, int Connections) CloseAll()
		{
			int endpoints = 0, connections = 0;
			foreach (var keyName in Endpoints.Keys.ToList())
			{
				if (!Endpoints.TryRemove(keyName, out var endpoint)) continue;
				connections += endpoint.CloseConnections();
				endpoint.Dispose();
				endpoints++;
			}
			return (endpoints, connections);
		}

		private const string PortDescription = "UDP port the endpoint binds, on IPv4 and IPv6, for a firewall rule or a NAT port forward. 0 (the default) takes a free port. The first listen or connect of a key binds it; a later one uses that endpoint as it is";

		private (IrohEndpoint?, IError?) GetEndpoint(string keyName, bool useRelay, int port)
		{
			if (port < 0 || port > 65535)
			{
				return (null, new ProgramError($"The port must be from 0 to 65535, got {port}", goalStep, function));
			}
			try
			{
				var endpoint = Endpoints.GetOrAdd(keyName, _ =>
				{
					string settingKey = SecretKeyPrefix + keyName;
					string? secret = settings.GetOrDefault<string?>(typeof(Program), settingKey, null);
					if (string.IsNullOrEmpty(secret))
					{
						secret = IrohKeys.Generate().Secret;
						settings.Set(typeof(Program), settingKey, secret);
					}
					return IrohEndpoint.Bind(secret, Alpn, useRelay, port);
				});
				return (endpoint, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError("Could not start iroh: " + ex.Message, goalStep, function));
			}
		}

		private async Task HandleStream(IrohStream stream, IrohConnection connection, IrohEndpoint endpoint, GoalToCallInfo onIncoming, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			TellAnswer answer;
			string? receivedFolder = null;
			try
			{
				var peer = new IrohPeer(connection.Id, null, connection);
				(answer, receivedFolder) = await Process(stream, peer, endpoint, onIncoming, engineRef, accessorRef, rootPath);
			}
			catch (Exception ex)
			{
				logger?.LogWarning(ex, "iroh: a command could not be processed");
				answer = new TellAnswer(false, null, "Declined: the command could not be read", 400);
			}

			if (!answer.Ok && receivedFolder != null)
			{
				try { Directory.Delete(receivedFolder, true); } catch { }
			}

			try { stream.WriteAndFinish(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(answer))); } catch { }
		}

		private async Task<(TellAnswer Answer, string? ReceivedFolder)> Process(IrohStream stream, IrohPeer peer, IrohEndpoint endpoint, GoalToCallInfo onIncoming, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			byte[] lengthBytes = new byte[4];
			if (!stream.ReadExactly(lengthBytes, 4, ChunkTimeoutMilliseconds))
			{
				return (new TellAnswer(false, null, "Declined: the command is not signed", 401), null);
			}
			int headerLength = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
			if (headerLength <= 0 || headerLength > MaxHeaderBytes)
			{
				return (new TellAnswer(false, null, "Declined: the command header is not valid", 400), null);
			}
			byte[] headerBytes = new byte[headerLength];
			if (!stream.ReadExactly(headerBytes, headerLength, ChunkTimeoutMilliseconds))
			{
				return (new TellAnswer(false, null, "Declined: the command header is not valid", 400), null);
			}

			return await Process(stream, headerBytes, peer, endpoint, onIncoming, engineRef, accessorRef, rootPath);
		}

		private async Task<(TellAnswer Answer, string? ReceivedFolder)> Process(IrohStream stream, byte[] bytes, IrohPeer peer, IrohEndpoint endpoint, GoalToCallInfo onIncoming, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			var request = JsonConvert.DeserializeObject<TellRequest>(Encoding.UTF8.GetString(bytes));
			if (request == null || string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.Payload))
			{
				return (new TellAnswer(false, null, "Declined: the command is not signed", 401), null);
			}

			SignedMessage? signed;
			try { signed = JsonConvert.DeserializeObject<SignedMessage>(request.Signature); }
			catch { signed = null; }
			if (signed == null) return (new TellAnswer(false, null, "Declined: the signature could not be read", 401), null);

			var (verified, error) = await signingService.VerifySignature(signed, null, request.Payload);
			if (error != null || verified == null)
			{
				return (new TellAnswer(false, null, "Declined: " + (error?.Message ?? "the signature is not valid"), 401), null);
			}

			var payload = JObject.Parse(request.Payload);
			string? to = payload["to"]?.ToString();
			if (!string.IsNullOrEmpty(to) && to != endpoint.Id)
			{
				return (new TellAnswer(false, null, "Declined: the command was signed for another endpoint", 403), null);
			}

			string goalName = payload["goal"]?.ToString() ?? "";
			if (string.IsNullOrWhiteSpace(goalName)) return (new TellAnswer(false, null, "Declined: no goal in the command", 400), null);

			var parameters = (payload["parameters"] as JObject)?.ToObject<Dictionary<string, object?>>() ?? new Dictionary<string, object?>();

			string? fileName = null;
			long fileSize = 0;
			string? fileSha256 = null;
			if (payload["file"] is JObject fileInfo)
			{
				fileName = Path.GetFileName(fileInfo["name"]?.ToString() ?? "");
				fileSize = fileInfo["size"]?.Value<long>() ?? -1;
				fileSha256 = fileInfo["sha256"]?.ToString()?.ToLowerInvariant();
				long maxFileBytes = settings.GetOrDefault(typeof(Program), MaxFileBytesSetting, DefaultMaxFileBytes);
				if (string.IsNullOrEmpty(fileName) || fileSize < 0 || string.IsNullOrEmpty(fileSha256) || fileSha256.Length != 64)
				{
					return (new TellAnswer(false, null, "Declined: the file in the command is not described correctly", 400), null);
				}
				if (fileSize > maxFileBytes)
				{
					return (new TellAnswer(false, null, $"Declined: the file is {fileSize} bytes, this app takes at most {maxFileBytes}", 413), null);
				}
			}

			var gateParameters = new Dictionary<string, object?>(onIncoming.Parameters)
			{
				[ReservedKeywords.Identity] = verified.Identity,
				["goalName"] = goalName,
				["parameters"] = parameters,
				["EndpointId"] = endpoint.Id,
				["peer"] = peer
			};
			if (fileName != null)
			{
				gateParameters["fileName"] = fileName;
				gateParameters["fileSize"] = fileSize;
			}
			// A command comes from outside: %Identity% is the identity that signed it, in the gate and in the goal it runs.
			using var origin = RunOrigin.External(verified.Identity?.ToString());
			var allowed = await pseudoRuntime.RunGoal(engineRef, accessorRef, rootPath, new GoalToCallInfo(onIncoming.Name, gateParameters));
			if (allowed.Error != null && allowed.Error is not Return)
			{
				return (new TellAnswer(false, null, "Declined: " + allowed.Error.Message, 403), null);
			}

			string? receivedFolder = null;
			if (fileName != null)
			{
				string folderName = Guid.NewGuid().ToString("N");
				receivedFolder = Path.Join(rootPath, IncomingFolder, folderName);
				Directory.CreateDirectory(receivedFolder);
				string savedPath = Path.Join(receivedFolder, fileName);

				string? receiveError = await Task.Run(() => ReceiveFile(stream, savedPath, fileSize, fileSha256!));
				if (receiveError != null) return (new TellAnswer(false, null, "Declined: " + receiveError, 400), receivedFolder);

				string? fileSignature = payload["fileSignature"]?.ToString();
				if (!string.IsNullOrEmpty(fileSignature) && fileSignature.Length <= MaxSignatureFileBytes)
				{
					await File.WriteAllTextAsync(savedPath + SignatureFileExtension, fileSignature);
				}

				parameters["file"] = IncomingFolder + "/" + folderName + "/" + fileName;
			}

			parameters[ReservedKeywords.Identity] = verified.Identity;
			parameters["peer"] = peer;
			var run = await pseudoRuntime.RunGoal(engineRef, accessorRef, rootPath, new GoalToCallInfo(goalName, parameters));

			if (run.Error is Return ret)
			{
				return (new TellAnswer(true, ReturnValue(ret), null, 200), receivedFolder);
			}
			if (run.Error != null) return (new TellAnswer(false, null, run.Error.Message, run.Error.StatusCode), receivedFolder);

			return (new TellAnswer(true, null, null, 200), receivedFolder);
		}

		private static string? ReceiveFile(IrohStream stream, string savedPath, long size, string expectedSha256)
		{
			using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
			using (var output = new FileStream(savedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				byte[] buffer = new byte[IrohConnection.ChunkBytes];
				long remaining = size;
				while (remaining > 0)
				{
					int read = stream.Read(buffer, (int)Math.Min(buffer.Length, remaining), ChunkTimeoutMilliseconds);
					if (read == 0) return "the file ended before all of it arrived";
					output.Write(buffer, 0, read);
					hash.AppendData(buffer, 0, read);
					remaining -= read;
				}
			}

			string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
			return actual == expectedSha256 ? null : "the file does not match the sha256 in the signed command";
		}

		private static void RemoveOldReceivedFiles(string rootPath)
		{
			try
			{
				string folder = Path.Join(rootPath, IncomingFolder);
				if (!Directory.Exists(folder)) return;
				foreach (var old in Directory.GetDirectories(folder).Where(p => Directory.GetCreationTimeUtc(p) < DateTime.UtcNow.AddDays(-1)))
				{
					Directory.Delete(old, true);
				}
			}
			catch { }
		}

		private static string HashFile(string path)
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
		}

		private static object? ReturnValue(Return ret)
		{
			var values = ret.ReturnVariables;
			if (values == null || values.Count == 0) return null;
			if (values.Count == 1) return values[0].Value;
			return values.ToDictionary(p => p.Name, p => p.Value);
		}

		public record TellRequest(string Signature, string Payload);
		public record TellAnswer(bool Ok, object? Result, string? Error, int StatusCode);
	}
}
