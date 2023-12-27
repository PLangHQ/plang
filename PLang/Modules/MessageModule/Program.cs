using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Direct;
using Nostr.Client.Requests;
using Nostr.Client.Responses;
using PLang.Attributes;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Reactive.Linq;


namespace PLang.Modules.MessageModule
{
	[Description("Send and recieve private messages using Nostr protocol")]
	public class Program : BaseProgram, IDisposable
	{
		private static readonly object _lock = new object();
		private readonly ISettings settings;
		private readonly ILogger logger;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly INostrClient client;
		private ModuleSettings moduleSettings;

		public static readonly string CurrentAccountIdx = "PLang.Modules.MessageModule.CurrentAccountIdx";
		public static readonly string NosrtEventKey = "__NosrtEventKey__";

		public Program(ISettings settings, ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine, ILlmService llmService, INostrClient client) : base()
		{
			this.settings = settings;
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.client = client;
			this.moduleSettings = new ModuleSettings(settings, llmService);
		}


		public async Task<string> GetPublicKey()
		{
			return GetCurrentKey().Bech32PublicKey;
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

		[Description("goalName should be prefixed by ! and be whole word with possible dot(.)")]
		public async Task Listen(string goalName, [HandlesVariable] string variableName = "content", DateTimeOffset? listenFromDateTime = null)
		{

			logger.LogInformation("Starting listener");
			var key = GetCurrentKey();
			var publicKey = key.Bech32PublicKey;

			DateTimeOffset since;
			if (listenFromDateTime != null)
			{
				since = listenFromDateTime.Value;
			}
			else
			{
				since = DateTimeOffset.UtcNow;
			}

			client.Send(
				new NostrRequest("timeline:pubkey:follows",
				new NostrFilter
				{
					Authors = new[]
						{
							key.HexPublicKey
						},
					Kinds = new[]
					{
						NostrKind.EncryptedDm
					},
					Since = since.DateTime
				}));

			if (listenFromDateTime == null)
			{
				settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince, DateTimeOffset.UtcNow);
			}


			client.Streams.EventStream.Subscribe(async (NostrEventResponse response) =>
			{
				var ev = response.Event as NostrEncryptedEvent;
				if (ev == null || ev.Content == null) return;

				var memoryCache = System.Runtime.Caching.MemoryCache.Default;
				lock (_lock)
				{

					var content = ev.DecryptContent(GetPrivateKey(ev.RecipientPubkey));
					var hash = content.ComputeHash();

					//For preventing multiple calls on same message. I don't think this is the correct way, but only way I saw.
					if (memoryCache.Contains("NostrId_" + hash)) return;
					memoryCache.Add("NostrId_" + hash, true, DateTimeOffset.UtcNow.AddMinutes(5));

					var parameters = new Dictionary<string, object>();
					parameters.Add(variableName.Replace("%", ""), content);
					parameters.Add(NosrtEventKey, ev);
					parameters.Add("sender", ev.Pubkey);
					
					pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName, parameters, goal).Wait();


				}
			});

			KeepAlive(this, "ListenToMessages");


		}


		public async Task SendPrivateMessageToMyself(string content)
		{
			SendPrivateMessage(content, GetCurrentKey().Bech32PublicKey);
		}
		public async Task SendPrivateMessage(string content, string npubReceiverPublicKey)
		{
			var currentKey = GetCurrentKey();
			var sender = GetPrivateKey(currentKey.Bech32PublicKey);
			var receiver = NostrPublicKey.FromBech32(npubReceiverPublicKey);

			content = variableHelper.LoadVariables(content).ToString();
			var ev = new NostrEvent
			{
				Kind = NostrKind.EncryptedDm,
				CreatedAt = DateTime.UtcNow,
				Content = content
			};

			var encrypted = ev.EncryptDirect(sender, receiver);
			var signed = encrypted.Sign(sender);

			client.Send(new NostrEventRequest(signed));
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

