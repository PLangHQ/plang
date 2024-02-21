using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PLang.Services.SettingsService
{

    public class Settings : ISettings
    {
        private readonly ISettingsRepositoryFactory settingsRepositoryFactory;
        private readonly IPLangFileSystem fileSystem;
        private readonly ILogger logger;
        private readonly PLangAppContext context;
        private readonly IPLangIdentityService identityService;
        private readonly IPLangSigningService signingService;

        public Settings(ISettingsRepositoryFactory settingsRepositoryFactory, IPLangFileSystem fileSystem, ILogger logger, PLangAppContext context, IPLangIdentityService identityService, IPLangSigningService signingService)
        {
            this.settingsRepositoryFactory = settingsRepositoryFactory;
            this.fileSystem = fileSystem;
            this.logger = logger;
            this.context = context;
            this.identityService = identityService;
            this.signingService = signingService;
            string appId = AppId;
            LoadSalt();
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
            if (string.IsNullOrEmpty(callingType.FullName) || !callingType.FullName.Contains("."))
            {
                throw new BuilderException($"Class '{callingType}' must have a name and namespace");
            }

            string settingValue = JsonConvert.SerializeObject(value);

            var signature = ""; //todo sign settings
            var setting = new Setting(AppId, callingType.FullName, type, key, settingValue);

            settingsRepositoryFactory.CreateHandler().Set(setting);
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
            if (key == null) key = type;

            var setting = settingsRepositoryFactory.CreateHandler().Get(callingType.FullName, type, key);
            if (setting == null) return new();

            List<T> list = JsonConvert.DeserializeObject<List<T>>(setting.Value) ?? new();
            return list;
        }

        public void Remove<T>(Type callingType, string? key = null)
        {
            var type = typeof(T).FullName;
            if (key == null) key = type;

            var setting = settingsRepositoryFactory.CreateHandler().Get(callingType.FullName, type, key);
            if (setting == null) return;

            settingsRepositoryFactory.CreateHandler().Remove(setting);
        }


        public T Get<T>(Type callingType, string key, T defaultValue, string explain)
        {
            if (defaultValue == null)
            {
                throw new RuntimeException("defaultValue cannot be null");
            }
            var type = defaultValue.GetType().FullName;

            var setting = settingsRepositoryFactory.CreateHandler().Get(callingType.FullName, type, key);
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

            var settings = settingsRepositoryFactory.CreateHandler().GetSettings();
            var setting = settings.FirstOrDefault(p => p.AppId == AppId && p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (setting == null) return defaultValue;

            var obj = JsonConvert.DeserializeObject<T>(setting.Value);
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

            var settings = settingsRepositoryFactory.CreateHandler().GetSettings();
            return settings.FirstOrDefault(p => p.AppId == AppId && p.ClassOwnerFullName == callingType.FullName && p.ValueType == type && p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public IEnumerable<Setting> GetAllSettings()
        {
            return settingsRepositoryFactory.CreateHandler().GetSettings();
        }

        public string GetSalt()
        {
            var setting = GetAllSettings().FirstOrDefault(p => p.ClassOwnerFullName == GetType().FullName && p.ValueType == typeof(string).ToString() && p.Key.Equals("Salt", StringComparison.OrdinalIgnoreCase));
            if (setting != null)
            {
                return setting.Value;
            }

            var salt = GenerateSalt(32);


            setting = new Setting("1", GetType().FullName, salt.GetType().ToString(), "Salt", salt);
            settingsRepositoryFactory.CreateHandler().Set(setting);

            return setting.Value;
        }

        private void LoadSalt()
        {
            var setting = GetSalt();
            context.AddOrReplace(ReservedKeywords.Salt, setting);
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
