using Microsoft.Extensions.Logging;
using NBitcoin;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Model;
using PLang.Models;
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
	

	public class PLangIdentityService(ISettingsRepository settingsRepository, IAppCache appCache, IPublicPrivateKeyCreator publicPrivateKeyCreator, ILogger logger, PLangAppContext context) : IPLangIdentityService
	{
		public static readonly string SettingKey = "Identities";
		private string? sharedIdentity = null;
		public void UseSharedIdentity(string? sharedIdentity = null)
		{
            if (sharedIdentity == null) 
            {
				this.sharedIdentity = null;
				settingsRepository.UseSharedDataSource(false);
            } else
			{
				this.sharedIdentity = sharedIdentity;
				settingsRepository.UseSharedDataSource(true);
			}
            
		}

		private Setting? GetSetting()
		{
			var setting = settingsRepository.GetSettings().FirstOrDefault(p => p.ClassOwnerFullName == this.GetType().FullName && p.Key == SettingKey);
			if (setting != null && PLangSigningService.VerifySignature(appCache, context, setting.Value, "Identity", GetType().FullName, setting.SignatureData).Result == null)
			{
				logger.LogWarning($"Signature for {setting.Key} is not valid. It has been modified outside of plang language.");
			}
			return setting;
		}

		public Identity CreateIdentity(string name = "MyIdentity", bool setAsDefault = false)
		{
			var setting = GetSetting();
			if (setting == null) return CreatePrivatePublicKeyIdentity(new(), name, true);


			var identites = GetIdentitiesWithPrivateKey().ToList();
			var identity = identites.FirstOrDefault(p => p.Name == name && !p.IsArchived && p.SharedIdentity == sharedIdentity);
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
				var identity = new Identity(name, keyCreator.GetPublicKey(), keyCreator.GetPrivateKey(), setAsDefault, sharedIdentity);
				identites.Add(identity);

				StoreIdentities(identites);

				return GetIdentityInstance(identity);
			}
		}

		private void StoreIdentities(IEnumerable<Identity> identities)
		{
			var jsonIdentities = JsonConvert.SerializeObject(identities);

			var signingService = new PLangSigningService(appCache, this, context);
			var seed = Encoding.UTF8.GetBytes(identities.FirstOrDefault().Value!.ToString()!);
			var signatureData = signingService.SignWithTimeout(seed, jsonIdentities, SettingKey, GetType().FullName, DateTimeOffset.UtcNow.AddYears(500), "C0");

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
			identity = GetIdentitiesWithPrivateKey().Where(p => !p.IsArchived && p.Identifier == identity.Identifier && p.SharedIdentity == sharedIdentity).FirstOrDefault()!;
			return identity;
		}
		public Identity GetCurrentIdentity()
		{		
			
			if (sharedIdentity == null && context.TryGetValue(ReservedKeywords.MyIdentity, out object? value) && value != null)
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
			if (sharedIdentity == null)
			{
				context.AddOrReplace(ReservedKeywords.MyIdentity, currentIdentity);
			}
			
			return currentIdentity;
		}

		private Identity GetIdentityInstance(Identity identity)
		{
			return new Identity(identity.Name, identity.Identifier, null, identity.IsDefault, identity.SharedIdentity)
			{
				Created = identity.Created,
				IsArchived = identity.IsArchived,
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
			var setting = GetSetting();
			if (setting == null) return new List<Identity>();

			var identities = JsonConvert.DeserializeObject<List<Identity>>(setting.Value);
			if (identities == null) throw new RuntimeException("Could not load Identites. Backup your system.sqlite database to save your Identities, you might have to delete system.sqlite");

			return identities;
		}

		public IEnumerable<Identity> GetAllIdentities()
		{
			return GetIdentitiesWithPrivateKey().Select((identity) => { identity.ClearValue(); return identity; });
		}
		public IEnumerable<Identity> GetIdentities()
		{
			return GetAllIdentities().Where(p => !p.IsArchived && p.SharedIdentity == sharedIdentity);
		}




		public async Task<bool> Authenticate(Dictionary<string, string> keyValues)
		{
			return true;
		}
	}
}
