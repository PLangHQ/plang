using LightInject;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using Nostr.Client.Client;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Direct;
using Nostr.Client.Requests;
using Nostr.Client.Responses;
using Nostr.Client.Utils;
using Org.BouncyCastle.Asn1.X9;
using PLang.Attributes;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Services.SigningService;
using PLang.Utils;
using System.ComponentModel;
using System.Configuration;
using System.Reactive.Linq;
using System.Security.Cryptography.X509Certificates;
using Websocket.Client;
using static PLang.Errors.AskUser.AskUserPrivateKeyExport;


namespace PLang.Modules.MessageModule
{
	//todo: should cleanup any usage of keys, set them to null after usage
	// this is so they dont stay in memory until garbage collection
	// this needs to happen down the call stack and figure out how settings is handled

	[Description("Send and recieve private messages. Get account(public key), set current account for messaging")]
	public class Program : BaseProgram, IDisposable
	{
		private static readonly object _lock = new object();
		private readonly ISettings settings;
		private readonly ILogger logger;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly INostrClient client;
		private readonly IPLangSigningService signingService;
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
		private readonly IErrorHandlerFactory errorHandlerFactory;
		private readonly IErrorSystemHandlerFactory errorSystemHandlerFactory;
		private readonly IAskUserHandlerFactory askUserHandlerFactory;
		private readonly IPLangFileSystem fileSystem;
		private ModuleSettings moduleSettings;

		public static readonly string CurrentAccountIdx = "PLang.Modules.MessageModule.CurrentAccountIdx";
		public static readonly string NosrtEventKey = "__NosrtEventKey__";

		public Program(ISettings settings, ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine,
			ILlmServiceFactory llmServiceFactory, INostrClient client, IPLangSigningService signingService,
			IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory,
			IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory,
			IAskUserHandlerFactory askUserHandlerFactory, IPLangFileSystem fileSystem
			) : base()
		{
			this.settings = settings;
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.llmServiceFactory = llmServiceFactory;
			this.client = client;
			this.signingService = signingService;
			this.outputStreamFactory = outputStreamFactory;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
			this.errorHandlerFactory = errorHandlerFactory;
			this.errorSystemHandlerFactory = errorSystemHandlerFactory;
			this.askUserHandlerFactory = askUserHandlerFactory;
			this.fileSystem = fileSystem;
			this.moduleSettings = new ModuleSettings(settings, llmServiceFactory);
		}


		public async Task<string> GetPublicKey()
		{
			return GetCurrentKey().Bech32PublicKey;
		}

		public async Task<List<string>> GetRelays()
		{
			return moduleSettings.GetRelays();
		}

		public async Task SetCurrentAccount(string publicKeyOrName)
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings));
			var idx = keys.FindIndex(p => (p.Bech32PublicKey == publicKeyOrName || p.Name == publicKeyOrName) && !p.IsArchived);
			if (idx == -1)
			{
				throw new ArgumentException($"{publicKeyOrName} could not be found in set of keys");
			}
			context.AddOrReplace(CurrentAccountIdx, idx);
		}

		IDisposable? disconnectDisposable = null;
		IDisposable? messageReceivedDisposable = null;
		[Description("goalName should be prefixed by ! and be whole word with possible slash(/)")]
		public async Task Listen(GoalToCall goalName, [HandlesVariable] string contentVariableName = "content",
				[HandlesVariable] string senderVariableName = "sender",
				[HandlesVariable] string eventVariableName = "__NosrtEventKey__", DateTimeOffset? listenFromDateTime = null,
				string[]? onlyMessageFromSenders = null)
		{
			var client2 = (NostrMultiWebsocketClient)client;

			for (int i = 0; onlyMessageFromSenders != null && i < onlyMessageFromSenders.Length; i++)
			{
				if (onlyMessageFromSenders[i].ToString().StartsWith("npub"))
				{
					onlyMessageFromSenders[i] = NostrConverter.ToHex(onlyMessageFromSenders[i].ToString(), out string? hrp);
				}

				if (!NostrConverter.IsHex(onlyMessageFromSenders[i]))
				{
					logger.LogError($"{onlyMessageFromSenders[i]} is not valid address to listen to");
				}
			}

			foreach (var c in client2.Clients)
			{
				disconnectDisposable?.Dispose();
				disconnectDisposable = c.Communicator.DisconnectionHappened.Subscribe(happened =>
				{
					Send(c, listenFromDateTime);
				});

				messageReceivedDisposable?.Dispose();
				messageReceivedDisposable = c.Communicator.MessageReceived.Subscribe(async (ResponseMessage message) =>
				{
					if (message.MessageType != System.Net.WebSockets.WebSocketMessageType.Text) return;
					if (!message.Text.StartsWith("[\"EVENT\"")) return;

					var eve = JsonConvert.DeserializeObject<NostrEventResponse>(message.Text);
					if (eve == null || eve.Event == null) return;


					if (onlyMessageFromSenders != null && onlyMessageFromSenders.Length > 0)
					{
						string? fromAddress = onlyMessageFromSenders.FirstOrDefault(p => p.ToString().Equals(eve.Event.Pubkey))?.ToString();
						if (fromAddress == null)
						{
							return;
						}
					}

					var key = GetCurrentKey();
					var publicKey = key.Bech32PublicKey;

					await ProcessEvent(engine, eve, contentVariableName, eventVariableName, senderVariableName, publicKey, goalName);
				});
			}

			logger.LogInformation("Starting message listener");
			var key = GetCurrentKey();
			Console.WriteLine($" - Message listener:{key.Bech32PublicKey}");
			Send(client2, listenFromDateTime);

			KeepAlive(this, "ListenToMessages");


		}


		private void Send(INostrClient client, DateTimeOffset? listenFromDateTime)
		{
			var key = GetCurrentKey();
			var publicKey = key.Bech32PublicKey;

			DateTimeOffset since;
			DateTimeOffset until = SystemTime.OffsetUtcNow();
			if (listenFromDateTime != null)
			{
				since = listenFromDateTime.Value;
			}
			else
			{
				since = settings.GetOrDefault(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, SystemTime.OffsetUtcNow());
				// Since Nostr uses unix timestamp, second is the most granual timestamp. Get only message created 1 second after our last message recieved
				// not sure this is good or correct.
				since = since.AddSeconds(1);
			}

			client.Send(
				new NostrRequest("timeline:pubkey:follows",
				new NostrFilter
				{
					P = [key.HexPublicKey],
					Kinds = [NostrKind.EncryptedDm],
					Since = since.DateTime
				}));


			if (listenFromDateTime == null)
			{
				//settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, until);
			}

		}

		private async Task<IError?> ProcessEvent(IEngine engine, NostrEventResponse response, string contentVariableName, string eventVariableName, string senderVariableName, string publicKey, GoalToCall goalName)
		{
			var ev = response.Event as NostrEncryptedEvent;
			if (ev == null || ev.Content == null) return null;


			var privateKey = GetPrivateKey(ev.RecipientPubkey);
			if (privateKey == null) return null;

			using (var container = new ServiceContainer())
			{
				container.RegisterForPLang(fileSystem.RootDirectory, fileSystem.RelativeAppPath, askUserHandlerFactory, outputStreamFactory, outputSystemStreamFactory, errorHandlerFactory, errorSystemHandlerFactory);

				var content = ev.DecryptContent(privateKey);
				var hash = ev.CreatedAt.ToString().ComputeHash().Hash + content.ComputeHash().Hash + ev.Pubkey.ComputeHash().Hash;

				var settings = container.GetInstance<ISettings>();
				lock (_lock)
				{
					//For preventing multiple calls on same message. I don't think this is the correct way, but only way I saw.
					var hashOfLastMessage = settings.GetOrDefault(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_hash_" + publicKey, "");
					if (hashOfLastMessage == hash)
					{
						return null;
					}

					var memoryCache = System.Runtime.Caching.MemoryCache.Default;
					if (memoryCache.Contains("NostrId_" + hash)) return null;
					memoryCache.Add("NostrId_" + hash, true, DateTimeOffset.UtcNow.AddMinutes(5));
				}

				var parameters = new Dictionary<string, object?>();
				if (JsonHelper.IsJson(content, out object? parsedObject))
				{
					parameters.Add(contentVariableName.Replace("%", ""), parsedObject);
				}
				else
				{
					parameters.Add(contentVariableName.Replace("%", ""), content);
				}
				parameters.Add(eventVariableName, ev);
				parameters.Add(senderVariableName, ev.Pubkey);

				var tags = ev.Tags;

				if (ev.CreatedAt != null)
				{
					var dt = settings.GetOrDefault(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, DateTimeOffset.MinValue);
					if (ev.CreatedAt.Value < dt)
					{
						return null;
					}

					settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, new DateTimeOffset(ev.CreatedAt.Value));
					settings.Set(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_hash_" + publicKey, hash);
				}
				Dictionary<string, object> validationKeyValues = new Dictionary<string, object>();
				Signature? signature = null;
				if (tags != null)
				{
					var signatureData = tags.FirstOrDefault(p => p.TagIdentifier.Equals("X-Signature"))?.AdditionalData.FirstOrDefault(p => p != null);
					if (signatureData != null)
					{
						signature = JsonConvert.DeserializeObject<Signature>(signatureData.ToString());
					}
				}

				if (signature != null)
				{
					var identites = signingService.VerifySignature(signature).Result;
					parameters.AddOrReplace(ReservedKeywords.Signature, identites.Signature);

				}
				engine = container.GetInstance<IEngine>();
				engine.Init(container);

				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				var task = pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName, parameters, goal);
				if (task == null) return null;


				try
				{
					await task;
				}
				catch { }

				var error = TaskHasError(task);

				if (error != null)
				{
					var handler = errorHandlerFactory.CreateHandler();
					(var isHandled, var handlerError) = await handler.Handle(error);
					if (!isHandled)
					{
						await handler.ShowError(error, goalStep);
					}
					error = ErrorHelper.GetMultipleError(error, handlerError);
				}

				var os = outputStreamFactory.CreateHandler();
				if (os is UIOutputStream)
				{
					((UIOutputStream)os).Flush();
				}
				return error;

			}


		}



		public async Task SendPrivateMessageToMyself(string content)
		{
			await SendPrivateMessage(content, GetCurrentKey().Bech32PublicKey);
		}

		public async Task SendPrivateMessage(string content, string receiverPublicKey)
		{
			if (string.IsNullOrEmpty(content.Trim()))
			{
				logger.LogWarning("Message content empty. Nothing sent");
				return;
			}

			if (string.IsNullOrEmpty(receiverPublicKey))
			{
				logger.LogWarning("Address missing. Nothing sent");
				return;
			}

			var currentKey = GetCurrentKey();
			var sender = GetPrivateKey(currentKey.Bech32PublicKey);
			NostrPublicKey receiver;
			if (!receiverPublicKey.ToLower().StartsWith("npub"))
			{
				receiver = NostrPublicKey.FromHex(receiverPublicKey);
			}
			else
			{
				receiver = NostrPublicKey.FromBech32(receiverPublicKey);

			}

			content = variableHelper.LoadVariables(content).ToString();

			var headers = new Dictionary<string, object>();
			headers.Add("hex-public-key", currentKey.HexPublicKey);
			var signedContent = await signingService.Sign(content, headers: headers);
			List<NostrEventTag> tags = new List<NostrEventTag>();
			
			string signatureAsJson = JsonConvert.SerializeObject(signedContent);
			tags.Add(new NostrEventTag("X-Signature", signatureAsJson));


			// Todo: Just noticed that tags are not ecrypted. Signature is therefor visible and gives information that shouldn't be public. 
			// Need to find a way to encrypt the tags. 
			NostrEventTags eventTags = new NostrEventTags(tags);

			var ev = new NostrEvent
			{
				Kind = NostrKind.EncryptedDm,
				CreatedAt = SystemTime.UtcNow(),
				Content = content,
				Tags = eventTags
			};


			var encrypted = ev.EncryptDirect(sender, receiver);
			var signed = encrypted.Sign(sender);

			var eventRequest = new NostrEventRequest(signed);

			client.Send(eventRequest);
		}

		public void Dispose()
		{
			context.Remove(CurrentAccountIdx);

			disconnectDisposable?.Dispose();
			messageReceivedDisposable?.Dispose();
		}

		private NostrKey GetCurrentKey()
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings));
			if (keys.Count == 0)
			{
				moduleSettings.CreateNewAccount();
				keys = settings.GetValues<NostrKey>(typeof(ModuleSettings));
			}

			NostrKey? key = null;
			if (context.ContainsKey(CurrentAccountIdx))
			{
				int idx = (int)context[CurrentAccountIdx];
				if (idx < keys.Count)
				{
					key = keys[idx];
				}
			}

			if (key == null)
			{
				key = keys.FirstOrDefault(p => p.IsDefault);
				if (key == null)
				{
					key = keys[0];
				}
			}

			return key;
		}

		private NostrPrivateKey? GetPrivateKey(string? publicKey)
		{
			if (publicKey == null) return null;

			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings));
			if (keys.Count == 0)
			{
				throw new KeyNotFoundException($"No keys exist in system");
			}

			var key = keys.FirstOrDefault(p => p.Bech32PublicKey == publicKey || p.HexPublicKey == publicKey);
			if (key == null)
			{
				logger.LogError($"Could not find {publicKey} key");
				return null;
			}
			return NostrPrivateKey.FromBech32(key.PrivateKeyBech32);

		}



		public async Task<(string?, IError?)> GetPrivateKey()
		{
			// This should be handled by the AskUserPrivateKeyExport, this Program.cs should not know about it.
			var lockTimeout = settings.GetOrDefault(typeof(AskUserPrivateKeyExport), LockedKey, DateTime.MinValue);
			if (lockTimeout != DateTime.MinValue && lockTimeout > SystemTime.UtcNow().AddDays(-1))
			{
				return (null, new StepError($"System has been locked from exporting private keys. You will be able to export after {lockTimeout}", goalStep));
			}

			var response = settings.GetOrDefault<DecisionResponse>(typeof(AskUserPrivateKeyExport), this.GetType().Name, new DecisionResponse("none", "", DateTime.MinValue));
			if (response == null || response.Level == "none" || response.Expires < SystemTime.UtcNow())
			{
				var error = new AskUserPrivateKeyExport(llmServiceFactory, settings, this.GetType().Name);
				return (null, error);
			}

			if (response.Level.ToLower() == "low" || response.Level.ToLower() == "medium")
			{
				var wallet = GetCurrentKey();
				return (wallet.PrivateKeyBech32, null);
			}

			return (null, new StepError(response.Explain, goalStep, "PrivateKeyLocked"));

		}
	}
}

