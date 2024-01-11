using Dapper;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils;
using System.Data;
using System.Data.SQLite;
using System.Security.Cryptography;

namespace PLang.Services.SettingsService
{
	public class SqliteSettingsRepository : ISettingsRepository
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PLangAppContext context;
		private string datasource;
		private string global_datasource = "";
		private bool inMemory = false;


		public SqliteSettingsRepository(IPLangFileSystem fileSystem, PLangAppContext context)
		{
			this.fileSystem = fileSystem;
			this.context = context;
			AppContext.TryGetSwitch(ReservedKeywords.Test, out inMemory);

			string dbAbsolutePath = Path.Join(fileSystem.RootDirectory, ".db", "system.sqlite");
			datasource = $"Data Source={dbAbsolutePath};Version=3;";

			Init();
		}
		private void CreateLlmCacheTable(string datasource)
		{
			using (IDbConnection connection = new SQLiteConnection(datasource))
			{
				string sql = $@"CREATE TABLE IF NOT EXISTS LlmCache (
					Id INTEGER PRIMARY KEY,
					Hash TEXT NOT NULL,
					LlmQuestion TEXT NOT NULL,
					Created DATETIME DEFAULT CURRENT_TIMESTAMP,
					LastUsed DATETIME DEFAULT CURRENT_TIMESTAMP
				);
				CREATE UNIQUE INDEX IF NOT EXISTS idx_hash ON LlmCache (Hash);
				";
				connection.Execute(sql);

			}
		}
		private void CreateSettingsTable(string datasource)
		{
			using (IDbConnection connection = new SQLiteConnection(datasource))
			{
				string sql = $@"
CREATE TABLE IF NOT EXISTS Settings (
    AppId TEXT NOT NULL,
    [ClassOwnerFullName] TEXT NOT NULL,
    [ValueType] TEXT NOT NULL,
    [Key] TEXT NOT NULL,
    Value TEXT NOT NULL,
    [Created] DATETIME DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX IF NOT EXISTS Settings_appId_IDX ON Settings (AppId, [ClassOwnerFullName], [ValueType], [Key]);
";
				connection.Execute(sql);

			}
		}


		public void Init()
		{
			if (inMemory)
			{
				global_datasource = "Data Source=GlobalDb;Mode=Memory;Cache=Shared;Version=3;";
				if (File.Exists("GlobalDb"))
				{
					File.Delete("GlobalDb");
				}
				CreateSettingsTable(global_datasource);
				
			}
			else
			{
				string globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plang", ".db");
				if (globalPath == "plang")
				{
					globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "plang", ".db");
				}

				// ONLY time Directory and File is used directly.
				// This is because PLangFileSystem depends on SqlSettingsRepository
				if (!Directory.Exists(globalPath))
				{
					Directory.CreateDirectory(globalPath);
				}

				string sqliteLocation = Path.Combine(globalPath, "system.sqlite");
				global_datasource = $"Data Source={sqliteLocation};Version=3;";
				if (!File.Exists(sqliteLocation))
				{
					File.Create(sqliteLocation).Close();
					CreateSettingsTable(global_datasource);
				}
			}
			CreateLlmCacheTable(global_datasource);
			LoadSalt();

			if (inMemory)
			{
				string dbName = "AppDb";
				if (!fileSystem.IsRootApp)
				{
					dbName = fileSystem.RelativeAppPath.Replace(Path.DirectorySeparatorChar, '_');
					if (dbName == "/") dbName = "AppDb";
				}
				if (File.Exists(dbName))
				{
					File.Delete(dbName);
				}

				datasource = $"Data Source={dbName};Mode=Memory;Cache=Shared;Version=3;";
				CreateSettingsTable(datasource);

			}
			else
			{
				string systemDbPath = Path.Join(".", ".db", "system.sqlite");
				if (!fileSystem.File.Exists(systemDbPath))
				{
					string dirName = Path.GetDirectoryName(systemDbPath);
					if (!fileSystem.Directory.Exists(dirName))
					{
						var dir = fileSystem.Directory.CreateDirectory(dirName);
						dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
					}

					fileSystem.File.Create(systemDbPath).Close();

					CreateSettingsTable(datasource);
				}

			}


		}
		public static string GenerateSalt(int length)
		{
			using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
			{
				byte[] salt = new byte[length];
				rng.GetBytes(salt);
				return Convert.ToBase64String(salt);
			}
		}
		private void LoadSalt()
		{
			using (IDbConnection connection = new SQLiteConnection(global_datasource))
			{
				var setting = connection.QueryFirstOrDefault<Setting>("SELECT * FROM Settings WHERE AppId=1 AND Key='Salt'");
				if (setting != null)
				{
					context.AddOrReplace(ReservedKeywords.Salt, setting.Value);
				}
				else
				{
					var salt = GenerateSalt(32);
					connection.Execute(@"INSERT INTO Settings (AppId, ClassOwnerFullName, ValueType, Key, Value) VALUES (1, @ClassOwnerFullName, @ValueType, @Key, @Value)", 
						new {
							ClassOwnerFullName = this.GetType().FullName,
							ValueType = typeof(string).ToString(),
							Key = "Salt",
							Value = salt
						});
					context.AddOrReplace(ReservedKeywords.Salt, salt);
				}
			}
		}

		public LlmQuestion? GetLlmCache(string hash) {
			using (IDbConnection connection = new SQLiteConnection(global_datasource))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new {hash});
				if (cache == null) return null;

				connection.Execute("UPDATE LlmCache SET LastUsed=CURRENT_TIMESTAMP WHERE Id=@id", new { id = cache.Id });
				return JsonConvert.DeserializeObject<LlmQuestion>(cache.LlmQuestion);
			}
		}
		public void SetLlmCache(string hash, LlmQuestion llmQuestion)
		{
			using (IDbConnection connection = new SQLiteConnection(global_datasource))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache != null) return;

				connection.Execute("INSERT INTO LlmCache (Hash, LlmQuestion) VALUES (@hash, @llmQuestion)", 
						new { hash, llmQuestion = JsonConvert.SerializeObject(llmQuestion)});
			}
		}

		public LlmRequest? GetLlmRequestCache(string hash)
		{
			using (IDbConnection connection = new SQLiteConnection(global_datasource))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache == null) return null;

				connection.Execute("UPDATE LlmCache SET LastUsed=CURRENT_TIMESTAMP WHERE Id=@id", new { id = cache.Id });
				return JsonConvert.DeserializeObject<LlmRequest>(cache.LlmQuestion);
			}
		}
		public void SetLlmRequestCache(string hash, LlmRequest llmQuestion)
		{
			using (IDbConnection connection = new SQLiteConnection(global_datasource))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache != null) return;

				connection.Execute("INSERT INTO LlmCache (Hash, LlmQuestion) VALUES (@hash, @llmQuestion)",
						new { hash, llmQuestion = JsonConvert.SerializeObject(llmQuestion) });
			}
		}


		public IEnumerable<Setting> GetSettings()
		{
			IEnumerable<Setting> settings;
			using (IDbConnection connection = new SQLiteConnection(datasource))
			{
				var settingsExists = connection.QueryFirstOrDefault<dynamic>("PRAGMA table_info(Settings)");
				if (settingsExists == null)
				{
					CreateSettingsTable(datasource);
				}

				settings = connection.Query<Setting>("SELECT * FROM Settings");
			}

			// should an apps/* have access to global_datasource?
			using (IDbConnection connection = new SQLiteConnection(global_datasource))
			{
				var settingsExists = connection.QueryFirstOrDefault<dynamic>("PRAGMA table_info(Settings)");
				if (settingsExists == null)
				{
					CreateSettingsTable(global_datasource);
				}

				var queryResult = connection.Query<Setting>("SELECT * FROM Settings");
				settings = settings.Concat(queryResult);
			}

			return settings;
		}

		public void Remove(Setting? setting)
		{
			if (setting == null) return;

			var dbSource = GetDatasource(setting);
			using (IDbConnection connection = new SQLiteConnection(dbSource))
			{
				connection.Execute("DELETE FROM Settings WHERE AppId=@AppId AND [ClassOwnerFullName]=@ClassOwnerFullName AND [ValueType]=@ValueType",
					new { setting.AppId, setting.ClassOwnerFullName, setting.ValueType });
				return;
			}
		}


		public void Set(Setting setting)
		{
			string dbSource = GetDatasource(setting);

			using (IDbConnection connection = new SQLiteConnection(dbSource))
			{
				connection.Execute(@"
					INSERT OR IGNORE INTO Settings (AppId, ClassOwnerFullName, ValueType, [Key], [Value], Created) VALUES (@AppId, @ClassOwnerFullName, @ValueType, @Key, @Value, @Created)
					ON CONFLICT(AppId, [ClassOwnerFullName], [ValueType], [Key]) DO UPDATE SET Value = excluded.Value;
					", new { setting.AppId, setting.ClassOwnerFullName, setting.ValueType, setting.Key, setting.Value, setting.Created });

			}
		}

		private string GetDatasource(Setting setting)
		{
			string dbSource = datasource;
			if (setting.Key.StartsWith("Global_"))
			{
				dbSource = global_datasource;
			}

			return dbSource;
		}
	}
}
