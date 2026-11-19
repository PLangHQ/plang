using LightInject;
using Microsoft.Extensions.Logging;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using NNostr.Client;
using NNostr.Client.Protocols;
using PLang.Attributes;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Collections.Concurrent;
using System.ComponentModel;
using static PLang.Errors.AskUser.AskUserPrivateKeyExport;



namespace PLang.Modules.MessageModule
{
	//todo: should cleanup any usage of keys, set them to null after usage
	// this is so they dont stay in memory until garbage collection
	// this needs to happen down the call stack and figure out how settings is handled

	[Description("Send and recieve private messages over the network (nostr) to a public key. Get account(public key), set current account for messaging. Get, add or remove the relays messages go through. Not for a database table that happens to be called messages: a step that inserts, selects or updates rows is DbModule, whatever the table is named")]
	[Effect(Effects.External)]
	public class Program : BaseProgram, IDisposable
	{
		private static readonly object _lock = new object();
		private readonly ISettings settings;
		private readonly ILogger logger;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IPLangSigningService signingService;

		private readonly IErrorHandlerFactory errorHandlerFactory;
		private readonly IErrorSystemHandlerFactory errorSystemHandlerFactory;
		private readonly IPLangFileSystem fileSystem;
		private ModuleSettings moduleSettings;

		public static readonly string CurrentAccountIdx = "PLang.Modules.MessageModule.CurrentAccountIdx";
		public static readonly string NosrtEventKey = "__NosrtEventKey__";

		// Private messages: NIP-17 (kind 1059 gift wrap around a kind 13 seal around a kind 14 message, NIP-44 encrypted) is what is
		// sent; NIP-04 (kind 4) is still received. Kind 10050 lists the relays a key reads its private messages from.
		private const int KindGiftWrap = 1059;
		private const int KindPrivateMessage = 14;
		private const int KindNip04Message = 4;
		private const int KindInboxRelays = 10050;

		// A gift wrap has a random created_at up to two days in the past, so a listener asks for two days more than it needs.
		private static readonly TimeSpan GiftWrapDateSpread = TimeSpan.FromDays(2);
		// Messages that come in out of order within this window are still received; the ids already processed keep them from running twice.
		private static readonly TimeSpan ReorderWindow = TimeSpan.FromMinutes(10);
		private const int ProcessedIdsToKeep = 500;
		private static readonly TimeSpan InboxRelayCacheTime = TimeSpan.FromMinutes(10);
		private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, List<string> Relays)> inboxRelayCache = new();

		public Program(ISettings settings, ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine,
			ILlmServiceFactory llmServiceFactory, IPLangSigningService signingService,
			IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory, IPLangFileSystem fileSystem
			) : base()
		{
			this.settings = settings;
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.llmServiceFactory = llmServiceFactory;
			this.signingService = signingService;
			this.errorHandlerFactory = errorHandlerFactory;
			this.errorSystemHandlerFactory = errorSystemHandlerFactory;
			this.fileSystem = fileSystem;
			this.moduleSettings = new ModuleSettings(settings, llmServiceFactory);
		}

		private async Task<NostrRelayPool> GetPool()
		{
			var pool = NostrRelayPool.Shared;
			pool.Logger ??= logger;
			await pool.SetRelays(moduleSettings.GetRelays());
			return pool;
		}

		public async Task<string> GetPublicKey()
		{
			return GetCurrentKey().Bech32PublicKey;
		}

		public async Task<List<string>> GetRelays()
		{
			return moduleSettings.GetRelays();
		}

		[Description("Add a relay that messages are sent through and listened on, e.g. relay.primal.net or wss://relay.primal.net. Returns the list of relays")]
		public async Task<List<string>> AddRelay(string relayUrl)
		{
			var relays = moduleSettings.AddRelay(relayUrl);
			await NostrRelayPool.Shared.SetRelays(relays);
			return relays;
		}

		[Description("Remove a relay from the relays messages are sent through and listened on. Returns the list of relays")]
		public async Task<List<string>> RemoveRelay(string relayUrl)
		{
			var relays = moduleSettings.RemoveRelay(relayUrl);
			await NostrRelayPool.Shared.SetRelays(relays);
			return relays;
		}

		public async Task SetCurrentAccount(string publicKeyOrName)
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings));
			var idx = keys.FindIndex(p => (p.Bech32PublicKey == publicKeyOrName || p.Name == publicKeyOrName) && !p.IsArchived);
			if (idx == -1)
			{
				throw new ArgumentException($"{publicKeyOrName} could not be found in set of keys");
			}
			appContext.AddOrReplace(CurrentAccountIdx, idx);
		}

		// A listener lives as long as the app, not as long as the step: the module instance is disposed when the goal that started
		// listening ends. A new listen on the same key replaces its listener.
		private static readonly ConcurrentDictionary<string, Action<string, NostrEvent>> listeners = new();

		[Description("goalName should be prefixed by ! and be whole word with possible slash(/). The sender of a message is its public key in hex")]
		public async Task Listen(GoalToCallInfo goalName, [HandlesVariable] string contentVariableName = "content",
				[HandlesVariable] string senderVariableName = "sender",
				[HandlesVariable] string eventVariableName = "__NosrtEventKey__", DateTimeOffset? listenFromDateTime = null,
				string[]? onlyMessageFromSenders = null)
		{
			var allowedSenders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var sender in onlyMessageFromSenders ?? [])
			{
				var hex = ToHexPublicKey(sender);
				if (hex == null)
				{
					logger.LogError($"{sender} is not valid address to listen to");
					continue;
				}
				allowedSenders.Add(hex);
			}

			var key = GetCurrentKey();
			var privateKey = ToPrivateKey(key);
			var cutoff = listenFromDateTime ?? GetWatermark(key.HexPublicKey);
			var subscriptionId = "plang-dm-" + key.HexPublicKey.Substring(0, 16);

			var pool = await GetPool();

			Action<string, NostrEvent> eventHandler = (id, nostrEvent) =>
			{
				if (id != subscriptionId) return;
				_ = Task.Run(async () =>
				{
					try
					{
						await ReceiveEvent(nostrEvent, key, privateKey, cutoff, listenFromDateTime != null, allowedSenders,
							contentVariableName, eventVariableName, senderVariableName, goalName);
					}
					catch (Exception ex)
					{
						logger.LogWarning($"Could not read message {nostrEvent.Id}: {ex.Message}");
					}
				});
			};
			if (listeners.TryRemove(subscriptionId, out var previous)) pool.EventReceived -= previous;
			listeners[subscriptionId] = eventHandler;
			pool.EventReceived += eventHandler;

			await pool.Subscribe(subscriptionId, [
				new NostrSubscriptionFilter { Kinds = [KindGiftWrap], ReferencedPublicKeys = [key.HexPublicKey], Since = cutoff - GiftWrapDateSpread },
				new NostrSubscriptionFilter { Kinds = [KindNip04Message], ReferencedPublicKeys = [key.HexPublicKey], Since = cutoff }
			]);

			await PublishInboxRelays(pool, privateKey, moduleSettings.GetRelays());

			logger.LogInformation("Starting message listener");
			Console.WriteLine($" - Message listener:{key.Bech32PublicKey}");

			KeepAlive(this, "ListenToMessages");
		}

		// Tells other clients (kind 10050) which relays this key reads its private messages from, so they send there.
		private async Task PublishInboxRelays(NostrRelayPool pool, ECPrivKey privateKey, List<string> relays)
		{
			try
			{
				var inbox = await new NostrEvent
				{
					Kind = KindInboxRelays,
					Content = "",
					Tags = relays.Select(r => new NostrEventTag { TagIdentifier = "relay", Data = [r] }).ToList()
				}.ComputeIdAndSignAsync(privateKey);
				await pool.Publish(inbox);
			}
			catch (Exception ex)
			{
				logger.LogWarning($"Could not publish the inbox relays: {ex.Message}");
			}
		}

		private async Task ReceiveEvent(NostrEvent nostrEvent, NostrKey key, ECPrivKey privateKey, DateTimeOffset cutoff, bool fixedCutoff,
			HashSet<string> allowedSenders, string contentVariableName, string eventVariableName, string senderVariableName, GoalToCallInfo goalName)
		{
			if (!MarkProcessing(key.HexPublicKey, nostrEvent.Id)) return;

			NostrEvent message;
			string content;
			if (nostrEvent.Kind == KindGiftWrap)
			{
				message = await NIP17.Open(nostrEvent, privateKey);
				content = message.Content ?? "";
			}
			else if (nostrEvent.Kind == KindNip04Message)
			{
				message = nostrEvent;
				content = await nostrEvent.DecryptNip04EventAsync(privateKey);
			}
			else
			{
				return;
			}

			var sentAt = message.CreatedAt ?? DateTimeOffset.UtcNow;
			if (sentAt < cutoff) return;
			if (allowedSenders.Count > 0 && !allowedSenders.Contains(message.PublicKey)) return;

			SaveProcessed(key.HexPublicKey, nostrEvent.Id, sentAt, fixedCutoff);

			await RunGoal(message, content, contentVariableName, eventVariableName, senderVariableName, goalName);
		}

		private async Task<IError?> RunGoal(NostrEvent message, string content, string contentVariableName, string eventVariableName, string senderVariableName, GoalToCallInfo goalName)
		{
			using (var container = new ServiceContainer())
			{
				container.RegisterForPLang(fileSystem.RootDirectory, fileSystem.RelativeAppPath, errorHandlerFactory, errorSystemHandlerFactory, parentEngine: engine);

				var parameters = new Dictionary<string, object?>();
				if (JsonHelper.IsJson(content, out object? parsedObject))
				{
					parameters.Add(contentVariableName.Replace("%", ""), parsedObject);
				}
				else
				{
					parameters.Add(contentVariableName.Replace("%", ""), content);
				}
				parameters.Add(eventVariableName, message);
				parameters.Add(senderVariableName, message.PublicKey);

				var signatureData = message.Tags?.FirstOrDefault(p => p.TagIdentifier == "X-Signature")?.Data.FirstOrDefault(p => p != null);
				if (signatureData != null)
				{
					var signature = JsonConvert.DeserializeObject<SignedMessage>(signatureData);
					if (signature != null)
					{
						var identites = await signingService.VerifySignature(signature);
						parameters.AddOrReplace(ReservedKeywords.Signature, identites.Signature);
					}
				}

				var goalEngine = container.GetInstance<IEngine>();
				goalEngine.Init(container);

				goalName.Parameters.AddOrReplaceDict(parameters);

				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				var task = pseudoRuntime.RunGoal(goalEngine, contextAccessor, Goal.RelativeAppStartupFolderPath, goalName, goal);
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

				return error;
			}
		}

		// The same message comes from every relay that has it, and again after a restart. It runs once.
		private bool MarkProcessing(string publicKey, string eventId)
		{
			lock (_lock)
			{
				var memoryCache = System.Runtime.Caching.MemoryCache.Default;
				if (memoryCache.Contains("NostrId_" + eventId)) return false;
				if (GetProcessedIds(publicKey).Contains(eventId)) return false;
				memoryCache.Add("NostrId_" + eventId, true, DateTimeOffset.UtcNow.AddDays(3));
				return true;
			}
		}

		private List<string> GetProcessedIds(string publicKey)
		{
			return settings.GetValues<string>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_processed_" + publicKey) ?? new();
		}

		private void SaveProcessed(string publicKey, string eventId, DateTimeOffset sentAt, bool fixedCutoff)
		{
			lock (_lock)
			{
				var ids = GetProcessedIds(publicKey);
				ids.Add(eventId);
				if (ids.Count > ProcessedIdsToKeep) ids = ids.Skip(ids.Count - ProcessedIdsToKeep).ToList();
				settings.SetList(typeof(ModuleSettings), ids, ModuleSettings.NostrDMSince + "_processed_" + publicKey);

				if (fixedCutoff) return;
				var last = settings.GetOrDefault(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, DateTimeOffset.MinValue);
				if (sentAt > last)
				{
					settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, sentAt);
				}
			}
		}

		// Messages sent before the listener first started are not received. After that, a message sent shortly before the last one
		// received is still received (relays deliver out of order); the processed ids keep a message from running twice.
		private DateTimeOffset GetWatermark(string publicKey)
		{
			var last = settings.GetOrDefault(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, DateTimeOffset.MinValue);
			if (last == DateTimeOffset.MinValue)
			{
				var now = SystemTime.OffsetUtcNow();
				settings.Set<DateTimeOffset>(typeof(ModuleSettings), ModuleSettings.NostrDMSince + "_" + publicKey, now);
				return now;
			}
			return last - ReorderWindow;
		}

		public async Task<IError?> SendPrivateMessageToMyself(string content)
		{
			return await SendPrivateMessage(content, GetCurrentKey().Bech32PublicKey);
		}

		[Description("Send a private message to a public key (npub... or hex)")]
		public async Task<IError?> SendPrivateMessage(string content, string receiverPublicKey)
		{
			if (string.IsNullOrEmpty(content?.Trim()))
			{
				logger.LogWarning("Message content empty. Nothing sent");
				return null;
			}

			if (string.IsNullOrEmpty(receiverPublicKey))
			{
				logger.LogWarning("Address missing. Nothing sent");
				return null;
			}

			var receiverHex = ToHexPublicKey(receiverPublicKey);
			if (receiverHex == null)
			{
				return new ProgramError($"{receiverPublicKey} is not a valid public key. It should start with npub or be 64 hex characters", goalStep, function, Key: "InvalidPublicKey");
			}
			var receiver = NostrExtensions.ParsePubKey(receiverHex);

			var currentKey = GetCurrentKey();
			var sender = ToPrivateKey(currentKey);

			content = memoryStack.LoadVariables(content)?.ToString() ?? "";

			// The plang signature goes inside the encrypted message, so only the receiver sees it.
			var headers = new Dictionary<string, object>();
			headers.Add("hex-public-key", currentKey.HexPublicKey);
			var signedContent = await signingService.Sign(content, headers: headers);

			var privateMessage = new NostrEvent
			{
				Kind = KindPrivateMessage,
				PublicKey = currentKey.HexPublicKey,
				CreatedAt = SystemTime.OffsetUtcNow(),
				Content = content,
				Tags = [
					new NostrEventTag { TagIdentifier = "p", Data = [receiverHex] },
					new NostrEventTag { TagIdentifier = "X-Signature", Data = [JsonConvert.SerializeObject(signedContent)] }
				]
			};

			var giftWrap = await NIP17.Create(privateMessage, sender, receiver, []);

			var pool = await GetPool();
			var inboxRelays = await GetInboxRelays(pool, receiverHex);
			var accepted = await pool.Publish(giftWrap, inboxRelays);
			if (accepted.Count == 0)
			{
				return new ProgramError($"No relay accepted the message to {receiverPublicKey}. Relays: {string.Join(", ", pool.Relays.Concat(inboxRelays))}", goalStep, function, Key: "MessageNotSent");
			}
			logger.LogDebug($"Message to {receiverPublicKey} accepted by {string.Join(", ", accepted)}");
			return null;
		}

		// The relays the receiver reads private messages from (its kind 10050), so the message is sent where the receiver looks.
		private async Task<List<string>> GetInboxRelays(NostrRelayPool pool, string receiverHex)
		{
			if (inboxRelayCache.TryGetValue(receiverHex, out var cached) && DateTime.UtcNow - cached.FetchedAt < InboxRelayCacheTime)
			{
				return cached.Relays;
			}

			var relays = new List<string>();
			try
			{
				var events = await pool.Fetch([new NostrSubscriptionFilter { Kinds = [KindInboxRelays], Authors = [receiverHex], Limit = 1 }], TimeSpan.FromSeconds(4));
				var latest = events.OrderByDescending(e => e.CreatedAt).FirstOrDefault();
				if (latest != null)
				{
					relays = latest.Tags.Where(t => t.TagIdentifier == "relay").SelectMany(t => t.Data.Take(1)).Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
				}
			}
			catch (Exception ex)
			{
				logger.LogDebug($"Could not get the inbox relays of {receiverHex}: {ex.Message}");
			}

			inboxRelayCache[receiverHex] = (DateTime.UtcNow, relays);
			return relays;
		}

		public void Dispose()
		{
			appContext.Remove(CurrentAccountIdx);
		}

		private static string? ToHexPublicKey(string? publicKey)
		{
			if (string.IsNullOrWhiteSpace(publicKey)) return null;
			publicKey = publicKey.Trim();
			try
			{
				if (publicKey.StartsWith("npub", StringComparison.OrdinalIgnoreCase)) return publicKey.FromNIP19Npub().ToHex();
				if (publicKey.Length == 64) return NostrExtensions.ParsePubKey(publicKey.ToLowerInvariant()).ToHex();
			}
			catch { }
			return null;
		}

		private static ECPrivKey ToPrivateKey(NostrKey key)
		{
			return key.PrivateKeyBech32.FromNIP19Nsec();
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
			if (appContext.ContainsKey(CurrentAccountIdx))
			{
				int idx = (int)appContext[CurrentAccountIdx];
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

		public record EmailMessage(List<string> ToEmails, string Subject, string Body, string TextBody,
			bool IsHtml = true, List<string>? Cc = null, List<string>? Bcc = null, string? ReplyTo = null,
			Dictionary<string, string>? Headers = null, List<object>? Attachements = null, string Service = "email.plang.io", string? FromEmail = null);

		public async Task<(object?, IError?)> SendEmail(EmailMessage emailMessage)
		{
			if (emailMessage.ToEmails.Count == 0) return (null, new ProgramError("To email address cannot be empty"));
			if (string.IsNullOrEmpty(emailMessage.Body) && string.IsNullOrEmpty(emailMessage.TextBody)) return (null, new ProgramError("The body of the email cannot be empty"));

			GroupedErrors groupedErrors = new();
			foreach (var toEmail in emailMessage.ToEmails)
			{
				if (!toEmail.Contains("@")) groupedErrors.Add(new ProgramError("Email must contain @ sign", FixSuggestion: $"The email {toEmail} didn't contain @ sign"));
			}
			if (groupedErrors.Count > 0) return (null, groupedErrors);

			Dictionary<string, object?> parameters = new();
			parameters.Add("EmailMessage", emailMessage);
			var caller = GetProgramModule<AppModule.Program>();
			var result = await caller.RunApp(new Models.AppToCallInfo("Email", "SendEmail", parameters));

			return result;

		}
	}
}
