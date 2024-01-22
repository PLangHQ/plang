using NBitcoin;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Model;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Text;
using System.Xml.Linq;

namespace PLang.Services.IdentityService
{
	public class IdentityException : Exception
	{
		public IdentityException(string message) : base(message) { }
	}
	public class SignatureExpiredException : Exception
	{
		public SignatureExpiredException(string message) : base(message) { }
	}
	public class SignatureException : Exception
	{
		public SignatureException(string message) : base(message) { }
	}

	public class PLangIdentityService(ISettingsRepository settingsRepository, IAppCache appCache, IPublicPrivateKeyCreator publicPrivateKeyCreator, PLangAppContext context) : IPLangIdentityService
	{
		public static readonly string SettingKey = "Identities";

		public Identity CreateIdentity(string name = "MyIdentity", bool setAsDefault = false)
		{
			var setting = settingsRepository.GetSettings().FirstOrDefault(p => p.ClassOwnerFullName == this.GetType().FullName && p.Key == SettingKey);
			if (setting == null) return CreatePrivatePublicKeyIdentity(new(), name, true);

			var identites = JsonConvert.DeserializeObject<List<Identity>>(setting.Value);
			if (identites == null) throw new RuntimeException("Could load Identites. Backup your system.sqlite database to save your Identies, you might have to delete system.sqlite");

			var identity = identites.FirstOrDefault(p => p.Name == name);
			if (identity != null)
			{
				throw new IdentityException($"Identity named '{name}' already exists");
			}

			if (identites.Count == 0)
			{
				setAsDefault = true;
			}
			else if (setAsDefault)
			{
				identites = identites.Select((id) =>
				{
					if (id.IsDefault) id.IsDefault = false;
					return id;
				}).ToList();
			}
			
			return CreatePrivatePublicKeyIdentity(identites, name, setAsDefault);
		}

		private Identity CreatePrivatePublicKeyIdentity(List<Identity> identites, string name, bool setAsDefault)
		{
			using (var keyCreator = publicPrivateKeyCreator.Create())
			{
				var identity = new Identity(name, keyCreator.GetPublicKey(), keyCreator.GetPrivateKey(), setAsDefault);
				identites.Add(identity);

				StoreIdentities(identites);

				return GetIdentityInstance(identity);
			}
		}

		private void StoreIdentities(IEnumerable<Identity> identities)
		{
			var jsonIdentities = JsonConvert.SerializeObject(identities);
			Dictionary<string, object> signatureData;

			var signingService = new SigningService.PLangSigningService(appCache, this);
			if (identities.Count() == 1)
			{
				var seed = Encoding.UTF8.GetBytes(identities.FirstOrDefault().Value.ToString()!);
				signatureData = signingService.SignWithTimeout(seed, jsonIdentities, SettingKey, GetType().FullName, DateTimeOffset.UtcNow.AddYears(500), "CreateIdentity");
			}
			else
			{
				var seed = Encoding.UTF8.GetBytes(identities.FirstOrDefault(p => p.IsDefault).Value.ToString()!);
				signatureData = signingService.SignWithTimeout(seed, jsonIdentities, SettingKey, GetType().FullName, DateTimeOffset.UtcNow.AddYears(500), "CreateIdentity");
			}

			Setting setting = new Setting("1", GetType().FullName, identities.GetType().FullName, SettingKey, jsonIdentities, signatureData);

			settingsRepository.Set(setting);
		}

		public Identity GetIdentity(string nameOrIdentitfier = "MyIdentity")
		{
			var identity = GetIdentities().FirstOrDefault(p => p.Name == nameOrIdentitfier || p.Identifier == nameOrIdentitfier);
			if (identity != null) return identity;

			throw new IdentityException("Identity not found");
		}

		public Identity SetIdentity(string nameOrIdentitfier = "MyIdentity")
		{
			var identity = GetIdentities().FirstOrDefault(p => p.Name == nameOrIdentitfier || p.Identifier == nameOrIdentitfier);
			if (identity == null) throw new IdentityException("Identity not found");

			context.AddOrReplace(ReservedKeywords.MyIdentity, identity);

			return identity;
		}

		public Identity GetCurrentIdentityWithPrivateKey()
		{
			Identity identity = GetCurrentIdentity();
			identity = GetIdentitiesWithPrivateKey().Where(p => !p.IsArchived && p.Identifier == identity.Identifier).FirstOrDefault()!;
			return identity;
		}
		public Identity GetCurrentIdentity()
		{			
			if (context.TryGetValue(ReservedKeywords.MyIdentity, out object? value) && value != null)
			{
				return (Identity)value;
			}

			Identity? identity;
			var identities = GetIdentities();

			identity = identities.FirstOrDefault(p => p.IsDefault);
			if (identity != null) return GetIdentityInstance(identity);
			

			identity = identities.FirstOrDefault(p => !p.IsArchived);
			if (identity != null) return GetIdentityInstance(identity);

			identity = CreateIdentity("MyIdentity", true);
			
			var currentIdentity = GetIdentityInstance(identity);

			context.AddOrReplace(ReservedKeywords.MyIdentity, currentIdentity);
			
			return currentIdentity;
		}

		private Identity GetIdentityInstance(Identity identity)
		{
			return new Identity(identity.Name, identity.Identifier, null)
			{
				Created = identity.Created,
				IsArchived = identity.IsArchived,
				IsDefault = identity.IsDefault,
			};
		}


		public Identity? ArchiveIdentity(string nameOrIdentitfier)
		{
			Identity? identity = null;
			var identities = GetIdentitiesWithPrivateKey();
			identities = identities.Select((id) =>
			{
				if (id.Name == nameOrIdentitfier || id.Identifier == nameOrIdentitfier)
				{
					if (id.IsDefault)
					{
						throw new IdentityException("The identity is the default identity. Change default identity before archiving it.");
					}
					identity = id;
					id.IsArchived = true;
				}
				return id;
			}).ToList();

			StoreIdentities(identities);
			return identity;
		}

		private IEnumerable<Identity> GetIdentitiesWithPrivateKey()
		{
			var setting = settingsRepository.GetSettings().FirstOrDefault(p => p.ClassOwnerFullName == this.GetType().FullName && p.Key == SettingKey);
			if (setting == null) return new List<Identity>();

			var identities = JsonConvert.DeserializeObject<List<Identity>>(setting.Value);
			if (identities == null) return new List<Identity>();

			return identities;
		}

		public IEnumerable<Identity> GetAllIdentities()
		{
			return GetIdentitiesWithPrivateKey().Select((identity) => { identity.ClearValue(); return identity; });
		}
		public IEnumerable<Identity> GetIdentities()
		{
			return GetAllIdentities().Where(p => !p.IsArchived);
		}




		public async Task<bool> Authenticate(Dictionary<string, string> keyValues)
		{
			return true;
		}
	}
}
