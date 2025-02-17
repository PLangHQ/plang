using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils;
using System.Data;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Text;
using PLang.Errors;

namespace PLang.Services.SettingsService
{
	public class SqliteSettingsRepository : ISettingsRepository
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PLangAppContext context;
		private readonly ILogger logger;
		private bool inMemory = false;
		private readonly string contextKey = "__SqliteSettingsRepository_DataSource__";
		public static Dictionary<string, IDbConnection> InMemoryDbCache = new();
		private string defaultSystemDbPath = Path.Join(".", ".db", "system.sqlite");
		private string systemDbPath;
		public SqliteSettingsRepository(IPLangFileSystem fileSystem, PLangAppContext context, ILogger logger)
		{			
			this.fileSystem = fileSystem;
			this.context = context;
			this.logger = logger;
			AppContext.TryGetSwitch(ReservedKeywords.Test, out inMemory);
			systemDbPath = defaultSystemDbPath;
			context.AddOrReplace(contextKey, LocalDataSourcePath);
			
			Init();
		}
		

		public void SetSharedDataSource(string? appId = null)
		{
			if (appId != null)
			{
				var sharedPath = GetSharedDataSourcePath(appId);
				CheckSettingsTable(sharedPath);
				context.AddOrReplace(contextKey, sharedPath);
				logger.LogTrace("SetSharedDataSource to " + sharedPath);
			} else
			{
				context.AddOrReplace(contextKey, LocalDataSourcePath);
			}			
		}

		

		public string DataSource
		{
			get
			{
				if (!context.TryGetValue(contextKey, out var value) || value == null) return LocalDataSourcePath;
				return value.ToString()!;
			}
		}

		public IError? SetSystemDbPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return new Error("Path to system database cannot be empty",
					FixSuggestion: $@"Create your step with the path clearly defined. e.g. 
- create data source main, path: /path/to/system.sqlite");
			}
			systemDbPath = path;
			Init();
			return null;
		}

		public void ResetSystemDbPath()
		{
			systemDbPath = defaultSystemDbPath;
		}

		public bool IsDefaultSystemDbPath {
			get { return systemDbPath == defaultSystemDbPath; }
		}
		public string LocalDataSourcePath
		{
			get
			{
				if (inMemory)
				{
					string dbName = "InMemorySystemDb";
					if (!fileSystem.IsRootApp)
					{
						var path = fileSystem.RelativeAppPath.Replace(Path.DirectorySeparatorChar, '_');
						if (dbName != "/") dbName = path;
					}
					

					return $"Data Source={dbName};Mode=Memory;Cache=Shared;";

				}
								
				string datasource = $"Data Source={systemDbPath};";				
				
				return datasource;
			}
		}


		public string GetSharedDataSourcePath(string appId) 
		{
			if (string.IsNullOrEmpty(appId))
			{
				throw new RuntimeException("name of share data source cannot be empty");
			}

			if (inMemory)
			{
				return "Data Source=InMemorySharedDb" + appId + ";Mode=Memory;Cache=Shared;";
			}

			string sharedDataSourcePath = Path.Join(fileSystem.SharedPath, appId, ".db", "system.sqlite");
			return $"Data Source={sharedDataSourcePath};";
			
		}

		
		private void CreateSettingsTable(string datasource)
		{
			var dbPath = datasource.Between("=", ";");

			//only time System.IO is used in the system.
			if (!File.Exists(dbPath))
			{
				string? dirName = Path.GetDirectoryName(dbPath);
				if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
				{
					var dir = Directory.CreateDirectory(dirName!);
					dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
				} else if (string.IsNullOrEmpty(dirName))
				{
					if (File.Exists(dbPath))
					{
						File.Delete(dbPath);
					}
				}
				using (var fs = File.Create(dbPath)) { 
					fs.Close();
				}
			}

			using (IDbConnection connection = new SqliteConnection(datasource))
			{
				string sql = $@"
CREATE TABLE IF NOT EXISTS Settings (
    [ClassOwnerFullName] TEXT NOT NULL,
    [ValueType] TEXT NOT NULL,
    [Key] TEXT NOT NULL,
    Value TEXT NOT NULL,
    [Created] DATETIME DEFAULT CURRENT_TIMESTAMP,
	SignatureData TEXT NOT NULL,
	UNIQUE ([ClassOwnerFullName], [Key])
);
";
				int rowsAffected = connection.Execute(sql);
				if (datasource.Contains("Mode=Memory"))
				{
					var memoryCon = new SqliteConnection(datasource);
					memoryCon.Open();
					SqliteSettingsRepository.InMemoryDbCache.AddOrReplace(datasource, memoryCon);
				}
			}
		}


		public void Init()
		{
			CheckSettingsTable(LocalDataSourcePath);
		}

		private void CheckSettingsTable(string dataSource)
		{
			CreateSettingsTable(dataSource);
			using (IDbConnection connection = new SqliteConnection(dataSource))
			{
				var settingsExists = connection.Query<dynamic>("PRAGMA table_info(Settings)");
				if (settingsExists.Count() == 0)
				{
					CreateSettingsTable(dataSource);
				}
				else
				{
					var settingProperties = typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Instance);
					if (settingProperties.Length != settingsExists.Count()) {
						ModifySettingsTable(connection, settingsExists, settingProperties);
					}
				}
			}
		}

		private void ModifySettingsTable(IDbConnection connection, IEnumerable<dynamic> settingsExists, PropertyInfo[] settingProperties)
		{
			foreach (var property in settingProperties)
			{
				// Check if the column exists in the table
				var columnExists = settingsExists.Any(row => string.Equals(row.name, property.Name, StringComparison.OrdinalIgnoreCase));

				if (!columnExists)
				{
					// If the column doesn't exist, create it
					var columnName = property.Name;
					var columnType = ConvertToSQLiteType(property.PropertyType);

					var alterTableCommand = $"ALTER TABLE Settings ADD COLUMN {columnName} {columnType};";
					connection.Execute(alterTableCommand);
				}
			}
		}

		private static string ConvertToSQLiteType(Type type)
		{
			// You can expand this method to handle more types as needed
			if (type == typeof(int) || type == typeof(long))
			{
				return "INTEGER";
			}
			else if (type == typeof(string) || type.Name == typeof(Dictionary<,>).Name || type.Name == typeof(List<>).Name)
			{
				return "TEXT";
			}
			else if (type == typeof(double) || type == typeof(float))
			{
				return "DOUBLE";
			}
			else if (type == typeof(DateTime))
			{
				return "DATETIME";
			}
			else
			{
				throw new NotSupportedException($"Type {type.Name} not supported.");
			}
		}

		


		public IEnumerable<Setting> GetSettings()
		{
			using (IDbConnection connection = new SqliteConnection(DataSource))
			{
				return connection.Query<Setting>("SELECT * FROM Settings");			
			}
		}

		public void Remove(Setting? setting)
		{
			if (setting == null) return;

			using (IDbConnection connection = new SqliteConnection(DataSource))
			{
				connection.Execute("DELETE FROM Settings WHERE [ClassOwnerFullName]=@ClassOwnerFullName AND [Key]=@Key",
					new { setting.ClassOwnerFullName, setting.Key });
				return;
			}
		}


		public void Set(Setting setting)
		{
			using (IDbConnection connection = new SqliteConnection(DataSource))
			{				
				var result = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM Settings WHERE [ClassOwnerFullName]=@ClassOwnerFullName AND [Key]=@Key",
					new { setting.ClassOwnerFullName, setting.Key });
				if (result == null)
				{
					connection.Execute(@"INSERT INTO Settings (AppId, ClassOwnerFullName, ValueType, [Key], [Value], SignatureData, Created) 
						VALUES (@AppId, @ClassOwnerFullName, @ValueType, @Key, @Value, @SignatureData, @Created)",
						new { setting.AppId, setting.ClassOwnerFullName, setting.ValueType, setting.Key, setting.Value, setting.SignatureData, setting.Created });
				} else
				{
					connection.Execute(@"UPDATE Settings SET Value=@Value, SignatureData=@SignatureData WHERE [ClassOwnerFullName]=@ClassOwnerFullName AND [Key]=@Key",
						new { setting.ClassOwnerFullName, setting.Key, setting.Value, setting.SignatureData });
				}				
			}
		}

		public Setting? Get(string? fullName, string? type, string? key)
		{
			var setting = GetSettings().FirstOrDefault(p => p.ClassOwnerFullName == fullName && p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			/*
			var verifiedData = signingService.VerifySignature(salt, setting.Value, "Setting", fullName, setting.Signature).Result;
			if (verifiedData == null)
			{
				logger.LogWarning($"Signature for setting {setting.Key} | {setting.ClassOwnerFullName} is not valid.");
			}
			*/
			return setting;
		}

		public string SerializeSettings()
		{
			StringBuilder sb = new StringBuilder();
			var settings = GetSettings();
			foreach (var setting in settings)
			{
				var key = setting.ClassOwnerFullName.Replace("+", "-") + "_" + setting.Key.Replace("+", "-");
				var json = JsonConvert.SerializeObject(setting);
				sb.Append(key + "=" + json + Environment.NewLine);
			}
			return sb.ToString();
		}
	}
}
