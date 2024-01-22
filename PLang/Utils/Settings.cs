using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.SigningService;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PLang.Utils
{

	public class Settings : ISettings
	{
		private readonly ISettingsRepository settingsRepository;
		private readonly IPLangFileSystem fileSystem;
		private readonly IPLangSigningService signingService;
		private readonly ILogger logger;
		private readonly PLangAppContext context;

		public Settings(ISettingsRepository settingsRepository, IPLangFileSystem fileSystem, IPLangSigningService signingService, ILogger logger, PLangAppContext context)
		{
			this.settingsRepository = settingsRepository;
			this.fileSystem = fileSystem;
			this.signingService = signingService;
			this.logger = logger;
			this.context = context;

			LoadSalt();
		}

		public LlmRequest? GetLlmRequest(string hash)
		{
			return settingsRepository.GetLlmRequestCache(hash);
		}

		public void SetLlmQuestion(string hash, LlmRequest question)
		{
			settingsRepository.SetLlmRequestCache(hash, question);
		}
		public string AppId
		{
			get
			{

				var buildPath = Path.Join(fileSystem.RootDirectory, ".build");
				string infoFile = Path.Combine(buildPath!, "info.txt");
				string appId;
				if (fileSystem.File.Exists(infoFile))
				{
					appId = fileSystem.File.ReadAllText(infoFile);
					string pattern = @"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b";

					Match match = Regex.Match(appId, pattern);
					if (!string.IsNullOrEmpty(match.Value))
					{
						return match.Value;
					}
				}

				string guid = Guid.NewGuid().ToString();
				appId = $"\n\nAppId: {guid}";
				if (fileSystem.File.Exists(infoFile))
				{
					fileSystem.File.AppendAllText(infoFile, appId);
				}
				else
				{
					fileSystem.File.WriteAllText(infoFile, appId);
				}
				return guid;
			}
		}

		private void SetInternal<T>(Type callingType, string type, string key, T? value)
		{
			if (string.IsNullOrEmpty(callingType.FullName))
			{
				throw new BuilderException("Class must have a name and namespace");
			}

			string settingValue = JsonConvert.SerializeObject(value);
			var signatureData = signingService.Sign(settingValue, "Setting", callingType.FullName);

			var setting = new Setting(AppId, callingType.FullName, type, key, settingValue, signatureData);
			settingsRepository.Set(setting);

			var settings = settingsRepository.GetSettings().ToList();
			int idx = settings.FindIndex(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);
			if (idx == -1)
			{
				settings.Add(setting);
			}
			else
			{
				settings[idx] = setting;
			}
		}



		public void Set<T>(Type callingType, string key, T value)
		{
			var typeName = typeof(T).FullName;

			if (value is System.Collections.IList && value.GetType().IsGenericType)
			{
				throw new Exception("Use SetList for saving lists");
			}
			SetInternal<T>(callingType, typeName, key, value);
		}
		public void SetList<T>(Type callingType, T value, string? key = null)
		{
			var typeName = typeof(T).FullName;

			if (value is System.Collections.IList && value.GetType().IsGenericType)
			{
				var listType = value.GetType();
				var listItemType = listType.GetGenericArguments()[0];
				typeName = listItemType.FullName;
			}

			if (key == null) key = typeName;

			SetInternal<T>(callingType, typeName, key, value);
		}

		private Setting? GetSetting<T>(Type callingType, string? key = null)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;

			var settings = settingsRepository.GetSettings();
			var setting = settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);
			if (setting == null) return null;

			var verifiedData = signingService.VerifySignature(setting.Value, "Setting", callingType.FullName, setting.SignatureData).Result;
			if (verifiedData == null) {
				logger.LogWarning($"Signature for setting {setting.Key} | {setting.ClassOwnerFullName} is not valid.");
			}
			return setting;
		}

		public List<T> GetValues<T>(Type callingType)
		{
			var type = typeof(T).FullName;

			var settings = settingsRepository.GetSettings();
			var setts = settings.Where(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type).ToList();
			if (setts.Count == 0) return new();

			List<T> list = JsonConvert.DeserializeObject<List<T>>(setts[0].Value) ?? new();
			return list;
		}

		public void Remove<T>(Type callingType, string? key = null)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;
			var setting = GetSetting<T>(callingType, key);
			if (setting == null) return;

			settingsRepository.Remove(setting);
		}


		public T Get<T>(Type callingType, string key, T defaultValue, string explain)
		{
			if (defaultValue == null)
			{
				throw new RuntimeException("defaultValue cannot be null");
			}
			var type = defaultValue.GetType().FullName;

			var settings = settingsRepository.GetSettings();
			var setting = settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);
			if (setting == null)
			{
				throw new MissingSettingsException(callingType, type, key, defaultValue, explain, SetInternal);
			}

			var obj = JsonConvert.DeserializeObject<T>(setting.Value);
			if (obj == null)
			{
				throw new MissingSettingsException(callingType, type, key, defaultValue, explain, SetInternal);
			}
			return obj;
		}
		public T GetOrDefault<T>(Type callingType, string? key, T defaultValue)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;

			var settings = settingsRepository.GetSettings();
			var setting = settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);

			if (setting == null) return defaultValue;

			var obj = JsonConvert.DeserializeObject<T>(setting.Value);
			if (obj == null) return defaultValue;
			return obj;
		}


		public bool Contains<T>(Type callingType, string? key = null)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;

			var settings = settingsRepository.GetSettings();
			return settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key) != null;
		}

		public IEnumerable<Setting> GetAllSettings()
		{
			return settingsRepository.GetSettings();
		}

		private void LoadSalt()
		{
			var setting = GetAllSettings().FirstOrDefault(p => p.ClassOwnerFullName == GetType().FullName && p.ValueType == typeof(string).ToString() && p.Key == "Salt");
			if (setting != null)
			{
				context.AddOrReplace(ReservedKeywords.Salt, setting.Value);
				return;
			}

			var salt = GenerateSalt(32);

			var signatureData = signingService.SignWithTimeout(salt, "Salt", GetType().FullName, SystemTime.OffsetUtcNow().AddYears(500));

			setting = new Setting("1", GetType().FullName, salt.GetType().ToString(), "Salt", salt, signatureData);
			settingsRepository.Set(setting);

			context.AddOrReplace(ReservedKeywords.Salt, salt);
		}

		private string GenerateSalt(int length)
		{
			byte[] salt = new byte[length];
			RandomNumberGenerator.Fill(salt);
			return Convert.ToBase64String(salt);
		}

	}
}
