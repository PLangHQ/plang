using NBitcoin.DataEncoders;
using Nostr.Client.Keys;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;
using static PLang.Modules.BlockchainModule.ModuleSettings;

namespace PLang.Modules.MessageModule
{

	public record NostrKey(string Name, string PrivateKeyBech32, string HexPublicKey, string Bech32PublicKey)
	{
		public bool IsDefault = false;
		public bool IsArchived = false;
	};

	public record Allowed(string Name, string HexPublicKey, string Bech32PublicKey);

	public class ModuleSettings : IModuleSettings
	{
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;
		public static readonly string NostrKeys = "NostrKeys";
		public static readonly string NostrRelays = "NostrRelays";
		public static readonly string NostrDMSince = "DMSince";
		public static readonly string AllowedList = "AllowedList";

		public ModuleSettings(ISettings settings, ILlmServiceFactory llmServiceFactory)
		{
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
		}

		public List<string> GetRelays()
		{
			var relays = settings.GetValues<string>(typeof(ModuleSettings), "NostrRelays");
			if (relays == null || relays.Count == 0)
			{
				relays = new List<string>() {
					"wss://relay.damus.io",
					"wss://nostr-pub.wellorder.net",
					"wss://offchain.pub",
					"wss://nos.lol"
				};
				settings.SetList(typeof(ModuleSettings), relays, "NostrRelays");
			}
			return relays;
		}

		public void AddRelay(string relayUrl)
		{
			if (!relayUrl.StartsWith("wss://"))
			{
				relayUrl = "wss://" + relayUrl;
			}

			UriCreationOptions options = new UriCreationOptions();

			if (!Uri.TryCreate(relayUrl, options, out Uri result))
			{
				throw new ArgumentException($"{relayUrl} is not valid url. It should start with wss://");
			}

			var relays = GetRelays();
			if (relays.Contains(relayUrl)) return;

			relays.Add(relayUrl);
			settings.SetList(typeof(ModuleSettings), relays);
		}

		public void RemoveRelay(string relayUrl)
		{
			var relays = GetRelays();
			relays.Remove(relayUrl);
			settings.SetList(typeof(ModuleSettings), relays);
		}

		public virtual void CreateNewAccount(string name = "Default")
		{
			NostrPrivateKey key = NostrPrivateKey.GenerateNew();

			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings)) ?? new List<NostrKey>();

			var nostrKey = new NostrKey(name, key.Bech32, key.DerivePublicKey().Hex, key.DerivePublicKey().Bech32);
			nostrKey.IsDefault = (keys.Where(p => !p.IsArchived).Count() == 0);

			keys.Add(nostrKey);
			settings.SetList(typeof(ModuleSettings), keys);
		}
		public void ArchiveAccount(string publicKeyOrName)
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings)) ?? new List<NostrKey>();
			if (keys.Count == 0) return;

			var key = keys.FirstOrDefault(p => p.Bech32PublicKey == publicKeyOrName || p.Name == publicKeyOrName);
			if (key == null)
			{
				throw new ArgumentException($"{publicKeyOrName} could not be found");
			}

			key.IsArchived = true;
			key.IsDefault = false;

			var defaultKey = keys.FirstOrDefault(p => !p.IsArchived && p.IsDefault);
			if (defaultKey != null)
			{
				settings.SetList(typeof(ModuleSettings), keys);
				return;
			}

			defaultKey = keys.FirstOrDefault(p => !p.IsArchived);
			if (defaultKey != null)
			{
				defaultKey.IsDefault = true;
			}
			else
			{
				CreateNewAccount();
				keys = settings.GetValues<NostrKey>(typeof(ModuleSettings)) ?? new List<NostrKey>();

			}
			settings.SetList(typeof(ModuleSettings), keys);

		}

		public void SetDefaultAccount(string publicKeyOrName)
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings)) ?? new List<NostrKey>();
			if (keys.Count == 0) return;

			var defaultKey = keys.FirstOrDefault(p => p.IsDefault);
			var newDefaultKey = keys.FirstOrDefault(p => p.Bech32PublicKey == publicKeyOrName || p.Name == publicKeyOrName);

			if (newDefaultKey == null)
			{
				throw new ArgumentException($"Could not find {publicKeyOrName}");
			}

			if (defaultKey != null) defaultKey.IsDefault = false;
			newDefaultKey.IsDefault = true;

			settings.SetList(typeof(ModuleSettings), keys);
		}

		public List<NostrKey> GetAccounts()
		{
			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings)) ?? new List<NostrKey>();
			var sanitizedKeys = new List<NostrKey>();
			foreach (var key in keys)
			{
				var newKey = new NostrKey(key.Name, "", key.HexPublicKey, key.Bech32PublicKey);
				sanitizedKeys.Add(newKey);
			}
			return sanitizedKeys;
		}

		public void SetLastDmAccess(DateTimeOffset dateTime)
		{
			settings.Set<DateTimeOffset>(typeof(ModuleSettings), NostrDMSince, dateTime);
		}

		public List<Allowed> GetAllowedList()
		{
			return settings.GetValues<Allowed>(typeof(ModuleSettings));
		}

		public void SetAllowed(string Name, string? HexPublicKey = null, string? Bech32PublicKey = null)
		{
			var allowedList = GetAllowedList();

			if (string.IsNullOrEmpty(HexPublicKey) && string.IsNullOrEmpty(Bech32PublicKey))
			{
				throw new ArgumentException("You must provide key to the user. Starts with npub...");
			}
			/*
			var bech32Encoder = Bech32Encoder.ExtractEncoderFromString("prefix");
			if (!string.IsNullOrEmpty(HexPublicKey) && string.IsNullOrEmpty(Bech32PublicKey))
			{
				byte[] publicKeyBytes = Encoders.Hex.DecodeData(HexPublicKey);
				Bech32PublicKey = bech32Encoder.Encode(publicKeyBytes);
			}

			if (!string.IsNullOrEmpty(Bech32PublicKey) && string.IsNullOrEmpty(HexPublicKey))
			{
				byte[] decodedPublicKeyBytes = bech32Encoder.Decode(Bech32PublicKey);
				HexPublicKey = Encoders.Hex.EncodeData(decodedPublicKeyBytes);
			}
			*/
			allowedList.Add(new Allowed(Name, HexPublicKey, Bech32PublicKey));
		}


		private List<string> answers = new List<string>();
		public async Task ExportPrivateKeys()
		{

			var lockingExpires = settings.GetOrDefault<DateTimeOffset>(typeof(ModuleSettings), "GLOBAL_PrivateKeysLocked", DateTimeOffset.UtcNow.AddDays(-2));
			if (lockingExpires > DateTimeOffset.UtcNow)
			{
				throw new AskUserPrivateKeyException($"System has been locked from exporting private keys. It will expire {lockingExpires}");
			}
			answers.Clear();

			throw new AskUserPrivateKeyException(@"Before we export your private keys I would like to ask you 3 question. Remember never share your private keys with people you don't know or trust.
Question 1: Why are you sharing your private key?", GetSecondQuestion);
		}

		private async Task GetSecondQuestion(string answer)
		{
			answers.Add("1. " + answer);
			throw new AskUserPrivateKeyException(@"2. Who specifically requested your private key, and how did they contact you?", GetThirdQuestion);
		}

		private async Task GetThirdQuestion(string answer)
		{
			answers.Add("2. " + answer);
			throw new AskUserPrivateKeyException(@"2. Who specifically requested your private key, and how did they contact you?", MakeDecision);
		}

		private record DecisionResponse(string Level, string Explain);
		public record NostrKeysExport(string Explain, List<NostrKey> keys);
		private async Task<WalletExport> MakeDecision(string answer)
		{
			answers.Add("3. " + answer);

			string system = @$"User is about to export his private keys. 
I have asked him 3 questions to determine if he is being scammed.
Give 3 levels of likely hood of him being scammed, low, medium, high. Give max 140 character description to the user about securing the private keys

These are the 3 questions
1. Why are you planning to share your private key?
2. Who specifically requested your private key, and how did they contact you?
3. Were you promised any benefits, rewards, or solutions in return for your private key?
";

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", system));
			promptMessage.Add(new LlmMessage("user", string.Join("\n", answers)));

			var llmRequest = new LlmRequest("ExportPrivateKeys", promptMessage);
			var response = await llmServiceFactory.CreateHandler().Query<DecisionResponse>(llmRequest);

			if (response.Level.ToLower() == "low" || response.Level.ToLower() == "medium")
			{
				return new WalletExport(response.Explain, settings.GetValues<Wallet>(typeof(ModuleSettings)));
			}

			settings.Set(typeof(ModuleSettings), "PrivateKeysLocked", DateTimeOffset.UtcNow.AddDays(1));

			string explain = response.Explain + Environment.NewLine + "To protect your private keys, we have locked them for 24 hours from being exported";
			return new WalletExport(explain, new List<Wallet>());

		}

	}
}
