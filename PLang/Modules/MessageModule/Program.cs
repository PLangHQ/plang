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
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.SigningService;
using PLang.Utils;
using System.ComponentModel;
using System.Configuration;
using System.Reactive.Linq;
using System.Security.Cryptography.X509Certificates;
using Websocket.Client;


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
		private readonly INostrClient client;
		private readonly IPLangSigningService signingService;
		private ModuleSettings moduleSettings;

		public static readonly string CurrentAccountIdx = "PLang.Modules.MessageModule.CurrentAccountIdx";
		public static readonly string NosrtEventKey = "__NosrtEventKey__";

		public Program(ISettings settings, ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine, ILlmServiceFactory llmServiceFactory, INostrClient client, IPLangSigningService signingService) : base()
		{
			this.settings = settings;
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.client = client;
			this.signingService = signingService;
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

		[Description("goalName should be prefixed by ! and be whole word with possible slash(/)")]
		public async Task Listen(string goalName, [HandlesVariable] string contentVariableName = "content", [HandlesVariable] string senderVariableName = "sender", [HandlesVariable] string eventVariableName = "__NosrtEventKey__", DateTimeOffset? listenFromDateTime = null, string[]? onlyMessageFromSenders = null)
		{
			var client2 = (NostrMultiWebsocketClient)client;

			for (int i = 0; onlyMessageFromSenders != null && i < onlyMessageFromSenders.Length; i++)
			{
				onlyMessageFromSenders[i] = NostrConverter.ToHex(onlyMessageFromSenders[i], out string? hrp);
			}

			foreach (var c in client2.Clients)
			{
				c.Communicator.DisconnectionHappened.Subscribe(happened =>
				{
					Send(c, listenFromDateTime);
				});
				c.Communicator.MessageReceived.Subscribe(async (ResponseMessage message) =>
				{
					if (message.MessageType != System.Net.WebSockets.WebSocketMessageType.Text) return;
					if (!message.Text.StartsWith("[\"EVENT\"")) return;

					var eve = JsonConvert.DeserializeObject<NostrEventResponse>(message.Text);
					if (eve == null || eve.Event == null) return;

					if (onlyMessageFromSenders != null && onlyMessageFromSenders.Length > 0)
					{
						string? fromAddress = onlyMessageFromSenders.FirstOrDefault(p => p == eve.Event.Pubkey);
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
				settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, until);
			}

		}

		private async Task ProcessEvent(IEngine engine, NostrEventResponse response, string contentVariableName, string eventVariableName, string senderVariableName, string publicKey, string goalName)
		{
			var ev = response.Event as NostrEncryptedEvent;
			if (ev == null || ev.Content == null) return;

			var memoryCache = System.Runtime.Caching.MemoryCache.Default;
			lock (_lock)
			{

				var content = ev.DecryptContent(GetPrivateKey(ev.RecipientPubkey));
				var hash = ev.CreatedAt.ToString().ComputeHash() + content.ComputeHash() + ev.Pubkey.ComputeHash();

				//For preventing multiple calls on same message. I don't think this is the correct way, but only way I saw.
				if (memoryCache.Contains("NostrId_" + hash)) return;
				memoryCache.Add("NostrId_" + hash, true, DateTimeOffset.UtcNow.AddMinutes(5));

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
						return;
					}

					settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, new DateTimeOffset(ev.CreatedAt.Value));
				}
				Dictionary<string, object> validationKeyValues = new Dictionary<string, object>();
				foreach (var tag in tags)
				{
					if (tag.AdditionalData.Length == 1)
					{
						validationKeyValues.Add(tag.TagIdentifier, tag.AdditionalData[0]);
					}
				}

				var identites = signingService.VerifySignature(settings.GetSalt(), content, "EncryptedDm", ev.Pubkey, validationKeyValues).Result;
				parameters.AddOrReplace(identites);

				var task = pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName, parameters, goal);
				if (task != null)
				{
					task.Wait();

					if (task.Exception != null) throw task.Exception;

				}


			}
		}

		public async Task SendPrivateMessageToMyself(string content)
		{
			await SendPrivateMessage(content, GetCurrentKey().Bech32PublicKey);
		}

		public async Task SendPrivateMessage(string content, string npubReceiverPublicKey)
		{
			if (string.IsNullOrEmpty(content.Trim()))
			{
				logger.LogWarning("Message content empty. Nothing sent");
				return;
			}

			if (string.IsNullOrEmpty(npubReceiverPublicKey))
			{
				logger.LogWarning("Address missing. Nothing sent");
				return;
			}

			var currentKey = GetCurrentKey();
			var sender = GetPrivateKey(currentKey.Bech32PublicKey);
			var receiver = NostrPublicKey.FromBech32(npubReceiverPublicKey);

			content = variableHelper.LoadVariables(content).ToString();

			var signedContent = signingService.SignWithTimeout(content, "EncryptedDm", currentKey.HexPublicKey, DateTimeOffset.UtcNow.AddYears(100));
			List<NostrEventTag> tags = new List<NostrEventTag>();
			foreach (var item in signedContent)
			{
				tags.Add(new NostrEventTag(item.Key, item.Value.ToString()));
			}

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

		private NostrPrivateKey GetPrivateKey(string publicKey)
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings));
			if (keys.Count == 0)
			{
				throw new KeyNotFoundException($"No keys exist in system");
			}

			var key = keys.FirstOrDefault(p => p.Bech32PublicKey == publicKey || p.HexPublicKey == publicKey);
			if (key == null)
			{
				throw new KeyNotFoundException($"Could not find {publicKey} key");
			}
			return NostrPrivateKey.FromBech32(key.PrivateKeyBech32);

		}
	}
}

