using Microsoft.IdentityModel.Tokens;
using PLang.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.CryptographicModule
{
	public class ModuleSettings : IModuleSettings
	{
		private readonly ISettings settings;

		public ModuleSettings(ISettings settings)
		{
			this.settings = settings;
		}

		public record BearerSecret(string Name, string Secret)
		{
			public bool IsDefault = false;
			public bool IsArchived = false;
		};

		public List<BearerSecret> GetBearerTokenSecrets()
		{
			var tokens = settings.GetValues<BearerSecret>(this.GetType());
			if (tokens == null || tokens.Count == 0)
			{
				GenerateNewBearerSecretKey("Default", true);
				tokens = settings.GetValues<BearerSecret>(this.GetType()) ?? new List<BearerSecret>();
			}
			return tokens;
		}
		public BearerSecret GetDefaultBearerSecret()
		{
			var tokens = settings.GetValues<BearerSecret>(this.GetType());
			if (tokens == null || tokens.Count == 0)
			{
				GenerateNewBearerSecretKey("Default", true);
				tokens = settings.GetValues<BearerSecret>(this.GetType()) ?? new List<BearerSecret>();
			}
			var token = tokens.FirstOrDefault(p => p.IsDefault);
			if (token == null) token = tokens[0];
			return token;
		}
		public BearerSecret GetBearerSecret(string name) {
			var tokens = settings.GetValues<BearerSecret>(this.GetType());
			if (tokens == null || tokens.Count == 0)
			{
				GenerateNewBearerSecretKey("Default", true);
				tokens = settings.GetValues<BearerSecret>(this.GetType()) ?? new List<BearerSecret>();
			}
			var token = tokens.FirstOrDefault(p => p.Name == name);
			if (token == null) token = tokens[0];
			return token;
		}

		public void SetAsDefault(string name)
		{
			var tokens = settings.GetValues<BearerSecret>(this.GetType());
			if (tokens != null && tokens.Count > 0)
			{
				var defaultToken = tokens.FirstOrDefault(p => p.IsDefault);
				if (defaultToken != null && defaultToken.Name == name) return;

				var newDefaultToken = tokens.FirstOrDefault(p => p.Name == name);
				if (newDefaultToken == null)
				{
					throw new ArgumentNullException($"{name} could not be found");
				}
				newDefaultToken.IsDefault = true;
				defaultToken.IsDefault = false;

				settings.SetList(GetType(), tokens);
			}
		}

		public void ArchiveToken(string name)
		{
			var tokens = settings.GetValues<BearerSecret>(this.GetType());
			if (tokens != null && tokens.Count > 0)
			{
				var token = tokens.FirstOrDefault(p => p.Name == name);
				if (token == null) return;

				token.IsArchived = true;

				settings.SetList(GetType(), tokens);
			}
		}

		public void GenerateNewBearerSecretKey(string name = "Default", bool setAsDefault = false)
		{	
			using (var rng = RandomNumberGenerator.Create())
			{
				int keySize = 256;
				byte[] key = new byte[keySize / 8]; 
				rng.GetBytes(key); 

				var symmetricKey = new SymmetricSecurityKey(key);
				string base64Key = Convert.ToBase64String(key);

				var tokens = settings.GetValues<BearerSecret>(this.GetType()) ?? new List<BearerSecret>();
				var bearerSecret = new BearerSecret(name, base64Key);
				if (tokens.Count == 0)
				{
					bearerSecret.IsDefault = true;
				}
				else
				{
					bearerSecret.IsDefault = setAsDefault;
				}

				tokens.Add(bearerSecret);
				settings.SetList(GetType(), tokens);


			}

		}
	}
}
