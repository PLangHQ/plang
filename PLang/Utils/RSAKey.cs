using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Services.SettingsService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
    internal class RSAKey
	{
		private readonly ISettings settings;
		private readonly ISettingsRepository settingsRepository;

		public record EncryptionKeys(string PublicKey, string PrivateKey, bool IsDefault);

		public RSAKey(ISettings settings, ISettingsRepository settingsRepository)
		{
			this.settings = settings;
			this.settingsRepository = settingsRepository;
		}


	}
}
