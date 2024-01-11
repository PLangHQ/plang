using Nethereum.Model;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PLang.Utils
{

	public class Settings : ISettings
	{
		private List<Setting> settings;
		private readonly ISettingsRepository settingsRepository;
		private readonly IPLangFileSystem fileSystem;

		public string BuildPath { get; private set; }
		public string GoalsPath { get; private set; }


		public Settings(ISettingsRepository settingsRepository, IPLangFileSystem fileSystem)
		{
			this.settingsRepository = settingsRepository;
			this.fileSystem = fileSystem;

			settings = settingsRepository.GetSettings().ToList();

			GoalsPath = fileSystem.RootDirectory;
			BuildPath = Path.Join(GoalsPath, ".build");

			if (!fileSystem.Directory.Exists(BuildPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(BuildPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}
		}

		public static string GlobalPath
		{
			get
			{
				string globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plang");
				if (globalPath == "plang")
				{
					globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "plang");
				}
				return globalPath;
			}
		}

		public string AppId
		{
			get
			{

				string infoFile = Path.Combine(BuildPath!, "info.txt");
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
			Setting? setting;
			string settingValue = JsonConvert.SerializeObject(value);
			setting = new Setting(AppId, callingType.FullName, type, key, settingValue);
			settingsRepository.Set(setting);

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

		public LlmQuestion? GetLlmQuestion(string hash)
		{
			return settingsRepository.GetLlmCache(hash);
		}

		public void SetLlmQuestion(string hash,  LlmQuestion question)
		{
			settingsRepository.SetLlmCache(hash, question);
		}

		public LlmRequest? GetLlmRequest(string hash)
		{
			return settingsRepository.GetLlmRequestCache(hash);
		}

		public void SetLlmQuestion(string hash, LlmRequest question)
		{
			settingsRepository.SetLlmRequestCache(hash, question);
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
			return settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);
		}

		public List<T> GetValues<T>(Type callingType)
		{
			var type = typeof(T).FullName;
			var setts = settings.Where(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type).ToList();
			if (setts.Count == 0) return new();

			List<T> list = JsonConvert.DeserializeObject<List<T>>(setts[0].Value);
			return list;
		}

		public void Remove<T>(Type callingType, string? key = null)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;
			var setting = GetSetting<T>(callingType, key);
			if (setting == null) return;

			settingsRepository.Remove(setting);
			settings.Remove(setting);
		}


		public T? Get<T>(Type callingType, string key, T defaultValue, string explain)
		{
			var type = defaultValue.GetType().FullName;

			var setting = settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);
			if (setting == null)
			{
				throw new MissingSettingsException(callingType, type, key, defaultValue, explain, SetInternal);
			}

			return JsonConvert.DeserializeObject<T>(setting.Value);
		}
		public T? GetOrDefault<T>(Type callingType, string? key, T defaultValue)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;

			var setting = settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key);

			if (setting == null) return defaultValue;

			return JsonConvert.DeserializeObject<T>(setting.Value);
		}


		public bool Contains<T>(Type callingType, string? key = null)
		{
			var type = typeof(T).FullName;
			if (key == null) key = type;


			return settings.FirstOrDefault(p => p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key == key) != null;
		}

		public List<Setting> GetAllSettings()
		{
			return settings;
		}

	}
}
