
/*
 * 
 * This is the way!
 * 

OnAppStart
- connect to llm.plang.is, port: 7526, 
    on message call ShowServerMessage
    write to %llmServer%

Server
- listen on port 5000, 
    on message call HandleMessage

HandleMessage
- call goal %data.goalName%
    allowed goals "user/*", "admin/*"

ReadFile
- read file %file%, stream to output
- execute select %sql%, %parameters%, stream to output

Client
- connect to llm.plang.is, port: 5000, 
    on message call ShowServerMessage
    write to %llmServer%
- call %llmServer%/admin/query, %sql%="select * from users", 
    headers
        "my-header" : 123,
    on message call ShowResult, data is %row%
    write to %users%
- refresh keys for %llmServer%

ShowServerMessage
- write out "Message from server: %data%"

ShowResult
- write out %type% - stream|object = isn't it always stream any point of this?
- write out %headers% and %Identity%
- append %row% to #rows at top
- send %data% to js, appendRow(%data%, 'top')
- set %data.Count% to #rowCount
- %count%


 * 
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using SuperSimpleTcp;

namespace PLang.Modules.TcpModule
{
	public class Program : BaseProgram
	{
		private readonly IAppCache appCache;

		public Program(IAppCache appCache)
		{
			this.appCache = appCache;
			context["!TcpServers"] = new Dictionary<string, Server>();
		}
		[MethodSettings(CanBeCached = false)]
		public async Task<object?> Connect(string ip, int port = 1, string name = "default", GoalToCall? onConnect = null, GoalToCall? onDisconnect = null, GoalToCall? onData = null)
		{
			var servers = context["!TcpServers"] as Dictionary<string, Server> ?? new();
			var server = servers[name];
			if (server == null)
			{
				server = new Server();
				servers.Add(server.Name, server);
			}
			server.Connection(this, ip, port, onConnect, onDisconnect, onData);
			return server;
		}

		public async Task<(IRequestResponse?, IError?)> Send(object? data = null, Dictionary<string, object>? headers = null,
			object? server = null, bool flush = true, string identityName = "default", string encryptionName = "default", DateTime? expires = null)
		{
			if (data == null && headers == null) return (null, null);

			Server? serverInstance = server as Server ?? null;

			if (server != null && server is string str)
			{
				string serverName = "default";
				if (!string.IsNullOrEmpty(str)) serverName = str;
				var servers = context["!TcpServers"] as Dictionary<string, Server> ?? new();
				serverInstance = servers[serverName ?? "default"];
			}
			if (server == null)
			{
				return (null, new ProgramError("You need to connect to a server first.", goalStep, function, FixSuggestion: "Add a step before this where you connect to tpc server, e.g. `- connect to example.org` or `connect to 127.0.0.1:8080"));
			}

			return await serverInstance.Tcp.SendData(data, headers, flush, identityName, encryptionName, expires);
		}



	}

	public interface IRequestResponse
	{
		object Signature { get; }
		object Headers { get; set; }
		object Data { get; set; }
		string Type { get; init; }
	}
	public class Request : IRequestResponse
	{
		public object Signature { get; set; }
		public object Headers { get; set; }
		public object Data { get; set; }
		public string Type { get; init; } = "Request";
	}
	public class Response : IRequestResponse
	{
		public object Signature { get; set; }
		public object Headers { get; set; }
		public object Data { get; set; }
		public string Type { get; init; } = "Request";
	}

	public class Compression
	{
		public Compression()
		{
			Type = "zstd";
			Level = "2";
		}
		public string Type { get; set; }
		public string Level { get; set; }
		public byte[] Package { get; set; }
		public Dictionary<string, object> Signature { get; set; }
	}

	public class Package
	{
		public Package()
		{
			Headers = new();
		}

		public object Data { get; set; }
		public Dictionary<string, object>? Headers { get; set; }
		public string? Type { get; set; }
		public Dictionary<string, object>? Signature { get; set; }

	}
	public interface IEncryption
	{
		public string PublicKey { get; set; }
		public string Type { get; set; }
	}
	public class Encryption : IEncryption
	{
		public string PublicKey { get; set; }
		public string Type { get; set; }
	}

	public class Server : IDisposable
	{
		private bool disposed;

		public Server()
		{
			Tcp = null;
		}

		public string Name { get; set; }
		public string Type { get; set; }
		public Tcp? Tcp { get; set; } // this

		public void Connection(Program program, string ip, int port, GoalToCall? onConnect = null, GoalToCall? onDisconnect = null, GoalToCall? onData = null)
		{
			Tcp = new Tcp(program, ip, port, onConnect, onDisconnect, onData);
		}

		public async Task<(IRequestResponse?, IError?)> Send(object data, Dictionary<string, object> headers, bool flush, string identity, string encryptionProfile, DateTime? expires)
		{
			if (Tcp == null)
			{
				return (null, new ServiceError("Make sure to connect first.", GetType(), "TcpIsNull", 500, FixSuggestion: "Create a step before this one, e.g. `Connect to 127.0.0.1"));
			}

			return await Tcp.SendData(data, headers, flush, identity, encryptionProfile, expires);
		}

		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}
			Tcp.Dispose();
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

	public class Tcp : IDisposable
	{
		private readonly Program program;
		private readonly string ip;
		private readonly int port;
		private readonly GoalToCall? onConnect;
		private readonly GoalToCall? onDisconnect;
		private readonly GoalToCall? onData;
		private readonly GoalToCall? onExpiredIdentity;
		private readonly GoalToCall? onError;
		private readonly Package Package;
		private bool disposed;
		List<Server> Servers { get; set; }
		public List<Encryption> PublicKeys { get; set; }
		private SimpleTcpServer server { get; set; }

		public Tcp(Program program, string ip, int port, GoalToCall? onConnect = null,
						GoalToCall? onDisconnect = null, GoalToCall? onData = null,
						GoalToCall? onExpiredIdentity = null, GoalToCall onError)
		{
			this.program = program;
			this.ip = ip;
			this.port = port;
			this.onConnect = onConnect;
			this.onDisconnect = onDisconnect;
			this.onData = onData;
			this.onExpiredIdentity = onExpiredIdentity;
			this.onError = onError;

			var callGoal = program.GetProgramModule<CallGoalModule.Program>();

			server = new SimpleTcpServer($"{ip}:{port}");

			// set events
			server.Events.ClientConnected += async (sender, e) =>
			{
				if (string.IsNullOrEmpty(onConnect)) return;
				await callGoal.RunGoal(onConnect);
			};
			server.Events.ClientDisconnected += async (sender, e) =>
			{
				if (string.IsNullOrEmpty(onDisconnect)) return;
				await callGoal.RunGoal(onDisconnect);
			};
			server.Events.DataReceived += async (sender, e) =>
			{
				if (string.IsNullOrEmpty(onData)) return;

				//decompress, deserialize and unecrypt
				var parameters = new Dictionary<string, object?> { { "data", e.Data } };
				await callGoal.RunGoal(onData, parameters);
			};

			// let's go!
			server.Start();


		}

		public async Task<(IRequestResponse?, IError?)> SendStream(ReadOnlySpan<byte> data, Dictionary<string, object> headers, bool flush = true,
			string? identityName = null, string? encryptionName = null, DateTime? expires = null)
		{
			var encryptionModule = program.GetProgramModule<CryptographicModule.Program>();
			if (!string.IsNullOrEmpty(encryptionName))
			{
				var result = await encryptionModule.SetCurrentEncryption(encryptionName);
				if (result.error != null) return (null, result);
			}

			var encryptedData = await encryptionModule.Encrypt(data);
			return await SendData(data, headers, flush, identityName, encryptionName, expires);

		}

		public async Task<(IRequestResponse?, IError?)> SendData(object data, Dictionary<string, object> headers,
			bool flush = true, string? identityName = null, string? encryptionName = null, string serializer = "message_pack", 
			DateTime? signatureExpires = null, string? compressionType = null, string? compressionLevel = null)
		{
			// and public key
			var identityModule = program.GetProgramModule<PLang.Modules.IdentityModule.Program>();
			if (identityModule == null)
			{
				return (null, new ServiceError("No identity module found", GetType(), "MissingIdentityModule", 500));
			}

			var encryptionModule = program.GetProgramModule<CryptographicModule.Program>();
			if (encryptionModule == null)
			{
				return (null, new ServiceError("No encryption module found", GetType(), "MissingEncryptionModule", 500));
			}
			var serializeModule = program.GetProgramModule<PLang.Modules.SerializerModule.Program>();
			if (serializeModule == null)
			{
				return (null, new ServiceError("No serialize module found", GetType(), "MissingSerializeModule", 500));
			}

			signatureExpires = signatureExpires ?? DateTime.UtcNow.AddMinutes(5);

			var package = new Package();
			package.Data = data;
			package.Headers = headers;

			var signature = identityModule.Sign(package, expires: signatureExpires, identity: identityName, serializer: serializer);
			var signedPackage = new SignedPackage(package, signature);

			var serializedSignedPackage = await serializeModule.Serialize<SignedPackage>(signedPackage, serializer);

			var compressionModule = program.GetProgramModule<PLang.Modules.CompressionModule.Program>();
			var compressedSignedPackage = await compressionModule.CompressData(serializedPackage, compressionType, compressionLevel);

			signature = identityModule.Sign(compressedSignedPackage, expires: expires);

			var signedCompression = new SignedCompression()
			{
				SignedPackage = compressedSignedPackage,
				Signature = signature,
				Type = compressionType,
				Level = compressionLevel
			};

			var encryptedData = encryptionModule.Encrypt(signedCompression, encryptionProfile: encryptionName);


			var task = await server.SendAsync($"{ip}:{port}", encryptedData);
			try
			{
				await task;
			} catch (Exception e)
			{
				return (null, new ServiceError(e.Message, GetType(), "SendDataError", 500));
			}


			return (new Response() { Package = package, Bytes = bytes }, null);
		}



		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}
			server.Dispose();
			this.disposed = true;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}

		static void ClientConnected(object sender, ConnectionEventArgs e)
		{
			Console.WriteLine($"[{e.IpPort}] client connected");
		}

		static void ClientDisconnected(object sender, ConnectionEventArgs e)
		{
			Console.WriteLine($"[{e.IpPort}] client disconnected: {e.Reason}");
		}

		protected async Task DataReceived(object sender, DataReceivedEventArgs e)
		{

			var serializeModule = context["!SerializerModule"] as PLang.Modules.SerializerModule.Program;
			var data = await serializeModule.Deserialize(e.Data);

			var compressionModule = context["!CompressionModule"] as PLang.Modules.CompressionModule.Program;
			var decompressedData = compressionModule.DecompressData(data);


			var encryptionModule = context["!EncryptionModule"] as PLang.Modules.CryptographicModule.Program;
			var package = await encryptionModule.Decrypt(decompressedData) as Package;


			var callGoalModule = context["!CallGoalModule"] as PLang.Modules.CallGoalModule.Program;
			if (package.Expires[0] < DateTime.UtcNow)
			{
				await CallGoalModule(onExpiredIdentity);
				return;
			}

			var requestData = new Dictionary<string, object?>();
			requestData.Add("request", data);

			var result = await callGoalModule.RunGoal(package.Type, requestData);
			if (result.Item2 != null)
			{
				await CallGoalModule(onError);
				return;
				//package.Type

			}

		}
		public async Task<object?, IError?> CallGoalModule(GoalToCall goal)
		{
			if (string.IsNullOrEmpty(goal))
			{
				var loggerModule = context["!LoggerModule"] as PLang.Modules.LoggerModule.Program;
				await loggerModule.Log("GoalToCall is empty");
				return (null, new ServiceError("GoalToCall is empty", GetType()));
			}

			var callGoalModule = context["!CallGoalModule"] as PLang.Modules.CallGoalModule.Program;
			var result = await callGoalModule.RunGoal(goal);

			if (result.Item1 != null) return result;
			if (result.Item2 == null) return result;


			result = await callGoalModule.RunGoal(goal);
			if (result.Item1 != null) return result;

			if (result.Item2 != null)
			{
				var loggerModule = context["!LoggerModule"] as PLang.Modules.LoggerModule.Program;
				await loggerModule.Log(result.Item2.ToString(), "error");
			}

		}

	}

	
}*/
