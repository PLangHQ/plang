using NBitcoin.Secp256k1;
using NNostr.Client;
using NNostr.Client.Protocols;
using PLang.Building.Model;
using System.Security.Cryptography;
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
			var relays = settings.GetValues<string>(typeof(ModuleSettings), NostrRelays);
			if (relays == null || relays.Count == 0)
			{
				relays = new List<string>() {
					"wss://relay.damus.io",
					"wss://relay.primal.net",
					"wss://nos.lol",
					"wss://nostr-pub.wellorder.net",
					"wss://offchain.pub",
					"wss://relay.nostr.band"
				};
				settings.SetList(typeof(ModuleSettings), relays, NostrRelays);
			}
			return relays;
		}

		public List<string> AddRelay(string relayUrl)
		{
			relayUrl = NostrRelayPool.Normalize(relayUrl);
			if (!Uri.TryCreate(relayUrl, UriKind.Absolute, out _))
			{
				throw new ArgumentException($"{relayUrl} is not a valid relay url. It should look like wss://relay.example.com");
			}

			var relays = GetRelays();
			if (relays.Any(r => NostrRelayPool.Normalize(r).Equals(relayUrl, StringComparison.OrdinalIgnoreCase))) return relays;

			relays.Add(relayUrl);
			settings.SetList(typeof(ModuleSettings), relays, NostrRelays);
			return relays;
		}

		public List<string> RemoveRelay(string relayUrl)
		{
			relayUrl = NostrRelayPool.Normalize(relayUrl);
			var relays = GetRelays();
			relays.RemoveAll(r => NostrRelayPool.Normalize(r).Equals(relayUrl, StringComparison.OrdinalIgnoreCase));
			settings.SetList(typeof(ModuleSettings), relays, NostrRelays);
			return relays;
		}

		public virtual void CreateNewAccount(string name = "Default")
		{
			var key = ECPrivKey.Create(RandomNumberGenerator.GetBytes(32));
			var publicKey = key.CreateXOnlyPubKey();

			var keys = settings.GetValues<NostrKey>(typeof(ModuleSettings)) ?? new List<NostrKey>();

			var nostrKey = new NostrKey(name, key.ToNIP19(), publicKey.ToHex(), publicKey.ToNIP19());
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


	}
}
