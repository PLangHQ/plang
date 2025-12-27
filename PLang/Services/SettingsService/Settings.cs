using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PLang.Services.SettingsService
{

	public class Settings : ISettings
    {
		private readonly string environment;
		private readonly ISettingsRepositoryFactory settingsRepositoryFactory;
        private readonly IPLangFileSystem fileSystem;


        public Settings(PLangAppContext appContext, ISettingsRepositoryFactory settingsRepositoryFactory, IPLangFileSystem fileSystem)
        {
			this.environment = appContext.Environment;
			this.settingsRepositoryFactory = settingsRepositoryFactory;
            this.fileSystem = fileSystem;
        }


        public string AppId
        {
            get
            {

                var buildPath = Path.Join(fileSystem.RootDirectory, ".build");
                string infoFile = Path.Join(buildPath!, "info.txt");
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

        public bool IsDefaultSystemDbPath
		{
            get { return settingsRepositoryFactory.CreateHandler().IsDefaultSystemDbPath;  }
        }


		private void InitFolders()
		{
			var buildPath = Path.Join(fileSystem.RootDirectory, ".build");
			if (!fileSystem.Directory.Exists(buildPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(buildPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}

			var dbPath = Path.Join(fileSystem.RootDirectory, ".db");
			if (!fileSystem.Directory.Exists(dbPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(dbPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}
		}

        private void SetInternal<T>(Type callingType, string type, string key, T? value)
        {
            if (string.IsNullOrEmpty(callingType.FullName) || !callingType.FullName.Contains("."))
            {
                throw new BuilderException($"Class '{callingType}' must have a name and namespace");
            }

            string settingValue = JsonConvert.SerializeObject(value);
			key = GetKey(key);
			var signature = ""; //todo sign settings
            var setting = new Setting(AppId, callingType.FullName, type, key, settingValue);
            var handler = settingsRepositoryFactory.CreateHandler();

			handler.Set(setting);
        }



        public void Set<T>(Type callingType, string key, T value)
        {
            var typeName = typeof(T).FullName;

            if (value is System.Collections.IList && value.GetType().IsGenericType)
            {
                throw new Exception("Use SetList for saving lists");
            }
            SetInternal(callingType, typeName, key, value);
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

            SetInternal(callingType, typeName, key, value);
        }


        public List<T> GetValues<T>(Type callingType, string? key = null)
        {
            var type = typeof(T).FullName;
            if (key == null && (callingType == typeof(string) || callingType.IsPrimitive))
            {
                throw new ArgumentException("key is missing");
            }

            if (key == null) key = type;
            var settingsRepo = settingsRepositoryFactory.CreateHandler();

			key = GetKey(key);
			var setting = settingsRepo.Get(callingType.FullName, type, key);
            if (setting == null) return new();

            List<T> list = JsonConvert.DeserializeObject<List<T>>(setting.Value) ?? new();
            return list;
        }

		private string GetKey(string key)
		{
			key = key.Replace("%", "");
			if (key.Contains(environment + ".")) return key;

			return environment + "." + key;
		}

		public void Remove<T>(Type callingType, string? key = null)
        {
            var type = typeof(T).FullName;
            if (key == null) key = type;

			key = GetKey(key);
			var settingsRepo = settingsRepositoryFactory.CreateHandler();
			var setting = settingsRepo.Get(callingType.FullName, type, key);
            if (setting == null) return;

			settingsRepo.Remove(setting);
        }


        public T Get<T>(Type callingType, string key, T defaultValue, string explain)
        {
            if (defaultValue == null)
            {
                throw new RuntimeException("defaultValue cannot be null");
            }
            var type = defaultValue.GetType().FullName;
			key = GetKey(key);
			var setting = settingsRepositoryFactory.CreateHandler().Get(callingType.FullName, type, key);
            if (setting == null)
            {
                throw new MissingSettingsException(callingType, type, key, defaultValue, explain, SetInternal);
            }
			try
			{
				var obj = JsonConvert.DeserializeObject<T>(setting.Value);
				if (obj == null)
				{
					throw new MissingSettingsException(callingType, type, key, defaultValue, explain, SetInternal);
				}
				return obj;
			}
			catch (Exception ex)
			{

				throw new MissingSettingsException(callingType, type, key, defaultValue, explain, SetInternal);
			}
            
        }
        public T GetOrDefault<T>(Type callingType, string? key, T defaultValue)
        {
            var type = typeof(T).FullName;
            if (key == null) key = type;
			key = GetKey(key);
			var setting = settingsRepositoryFactory.CreateHandler().Get(callingType.FullName, type, key);
			if (setting == null) return defaultValue;

            T? obj;
            if (typeof(T) == typeof(string) && !string.IsNullOrEmpty(setting.Value) && !setting.Value.StartsWith("\""))
            {
                obj = (T) Convert.ChangeType(setting.Value, typeof(T));
            }
            else
            {
                obj = JsonConvert.DeserializeObject<T>(setting.Value);
            }
            if (obj == null) return defaultValue;
            return obj;
        }

        public void SetSharedSettings(string appId)
        {
            settingsRepositoryFactory.CreateHandler().SetSharedDataSource(appId);
        }
        public void RemoveSharedSettings()
        {
            settingsRepositoryFactory.CreateHandler().SetSharedDataSource();
        }

        public bool Contains<T>(Type callingType, string? key = null)
        {
            var type = typeof(T).FullName;
            if (key == null) key = type;
			key = GetKey(key);
			var setting = settingsRepositoryFactory.CreateHandler().Get(GetType().FullName, type, key);
            return setting != null;

		}

        public IEnumerable<Setting> GetAllSettings()
        {
            return settingsRepositoryFactory.CreateHandler().GetSettings();
        }
        public static readonly string SaltKey = "__Salt__";
        public string GetSalt()
        {
            var salt = GetOrDefault<string>(GetType(), SaltKey, null); 
            if (salt != null)
            {
                return salt;
            }

            salt = GenerateSalt(32);
            var setting = new Setting("1", GetType().FullName, salt.GetType().ToString(), SaltKey, JsonConvert.SerializeObject(salt));
            settingsRepositoryFactory.CreateHandler().Set(setting);

            return setting.Value;
        }

        private string GenerateSalt(int length)
        {
            byte[] salt = new byte[length];
            RandomNumberGenerator.Fill(salt);
            return Convert.ToBase64String(salt);
        }

        public string SerializeSettings()
        {
            return settingsRepositoryFactory.CreateHandler().SerializeSettings();
        }

    }
}
