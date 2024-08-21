using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils;
using System.Xml.Linq;

namespace PLang.Services.IdentityService
{
	public class IdentityException : Exception
	{
		public IdentityException(string message) : base(message) { }
	}


	public class PLangIdentityService : IPLangIdentityService
	{
		public static readonly string SettingKey = "PLangIdentityService";
		private readonly ISettingsRepository settingsRepository;
		private readonly IPublicPrivateKeyCreator publicPrivateKeyCreator;
		private readonly PLangAppContext context;
		string appId;

		public PLangIdentityService(ISettingsRepository settingsRepository, IPublicPrivateKeyCreator publicPrivateKeyCreator, PLangAppContext context)
		{
			this.settingsRepository = settingsRepository;
			this.publicPrivateKeyCreator = publicPrivateKeyCreator;
			this.context = context;
		}

		public void UseSharedIdentity(string? appId = null)
		{
			settingsRepository.SetSharedDataSource(appId);
			this.appId = appId;
		}

		public Identity CreateIdentity(string name, bool setAsDefault = false)
		{
			var identities = GetIdentities().ToList();
			var identity = identities.FirstOrDefault(p => p.Name == name && !p.IsArchived);
			if (identity != null)
			{
				throw new IdentityException($"Identity named '{name}' already exists");
			}

			if (identities.Count == 0)
			{
				setAsDefault = true;
			}
			else if (setAsDefault)
			{
				identities = identities.Select((id) =>
				{
					if (id.IsDefault) id.IsDefault = false;
					return id;
				}).ToList();
			}

			return CreatePrivatePublicKeyIdentity(identities, name, setAsDefault);
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
			var setting = new Setting("1", GetType().FullName, typeof(List<Identity>).ToString(), SettingKey, jsonIdentities);

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

			context.AddOrReplace(ReservedKeywords.MyIdentity + $"_{appId}", identity);

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
			Identity? identity;
			var identities = GetIdentities();
			
			identity = context[ReservedKeywords.MyIdentity + $"_{appId}"] as Identity;
			if (identity != null) return identity;

			identity = identities.FirstOrDefault(p => p.IsDefault);
			if (identity != null) return GetIdentityInstance(identity);


			identity = identities.FirstOrDefault(p => !p.IsArchived);
			if (identity != null) return GetIdentityInstance(identity);

			identity = CreateIdentity("MyIdentity", true);

			var currentIdentity = GetIdentityInstance(identity);
			
			return currentIdentity;
		}

		private Identity GetIdentityInstance(Identity identity)
		{
			return new Identity(identity.Name, identity.Identifier, null, identity.IsDefault)
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
			var setting = settingsRepository.Get(GetType().FullName, typeof(List<Identity>).ToString(), SettingKey);
			if (setting == null)
			{
				CreatePrivatePublicKeyIdentity(new(), "MyIdentity", true);
				setting = settingsRepository.Get(GetType().FullName, typeof(List<Identity>).ToString(), SettingKey);
			}
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
			return GetAllIdentities().Where(p => !p.IsArchived);
		}

		public async Task<bool> Authenticate(Dictionary<string, string> keyValues)
		{
			return true;
		}
	}
}
