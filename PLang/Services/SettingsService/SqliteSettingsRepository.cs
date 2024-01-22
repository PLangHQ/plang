using Dapper;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils;
using System.Data;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace PLang.Services.SettingsService
{
	public class SqliteSettingsRepository : ISettingsRepository
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PLangAppContext context;
		private bool inMemory = false;
		private readonly string contextKey = "__SqliteSettingsRepository_DataSource__";
		
		public SqliteSettingsRepository(IPLangFileSystem fileSystem, PLangAppContext context)
		{
			this.fileSystem = fileSystem;
			this.context = context;			

			AppContext.TryGetSwitch(ReservedKeywords.Test, out inMemory);

			context.AddOrReplace(contextKey, LocalDataSourcePath);

			Init();
		}

		public void UseSharedDataSource(bool activateSharedDataSource = false)
		{
			if (activateSharedDataSource)
			{
				context.AddOrReplace(contextKey, SharedDataSourcePath);
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
					

					return $"Data Source={dbName};Mode=Memory;Cache=Shared;Version=3;";

				}

				string systemDbPath = Path.Join(".", ".db", "system.sqlite");
				string datasource = $"Data Source={systemDbPath};Version=3;";				
				
				return datasource;
			}
		}

		public string SharedDataSourcePath
		{
			get
			{
				if (inMemory)
				{
					return "Data Source=InMemorySharedDb;Mode=Memory;Cache=Shared;Version=3;";
				}

				string shareDataSourcePath = Path.Join(fileSystem.SharedPath, ".db", "system.sqlite");
				return $"Data Source={shareDataSourcePath};Version=3;";
			}
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

			var dbPath = datasource.Between("=", ";");
			if (!fileSystem.File.Exists(dbPath))
			{
				string? dirName = Path.GetDirectoryName(dbPath);
				if (!string.IsNullOrEmpty(dirName) && !fileSystem.Directory.Exists(dirName))
				{
					var dir = fileSystem.Directory.CreateDirectory(dirName!);
					dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
				} else if (string.IsNullOrEmpty(dirName))
				{
					if (File.Exists(dbPath))
					{
						File.Delete(dbPath);
					}
				}
				fileSystem.File.Create(dbPath).Close();

			}

			using (IDbConnection connection = new SQLiteConnection(datasource))
			{
				string sql = $@"
CREATE TABLE IF NOT EXISTS Settings (
    AppId TEXT NOT NULL,
    [ClassOwnerFullName] TEXT NOT NULL,
    [ValueType] TEXT NOT NULL,
    [Key] TEXT NOT NULL,
    Value TEXT NOT NULL,
	SignatureData TEXT NOT NULL,
    [Created] DATETIME DEFAULT CURRENT_TIMESTAMP	
);
CREATE UNIQUE INDEX IF NOT EXISTS Settings_appId_IDX ON Settings (AppId, [ClassOwnerFullName], [ValueType], [Key]);
";
				connection.Execute(sql);

			}
		}


		public void Init()
		{
			
			CreateSettingsTable(LocalDataSourcePath);
			CreateSettingsTable(SharedDataSourcePath);

			CreateLlmCacheTable(SharedDataSourcePath);

		}
	

		public LlmRequest? GetLlmRequestCache(string hash)
		{
			using (IDbConnection connection = new SQLiteConnection(SharedDataSourcePath))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache == null) return null;

				connection.Execute("UPDATE LlmCache SET LastUsed=CURRENT_TIMESTAMP WHERE Id=@id", new { id = cache.Id });
				return JsonConvert.DeserializeObject<LlmRequest>(cache.LlmQuestion);
			}
		}
		public void SetLlmRequestCache(string hash, LlmRequest llmQuestion)
		{
			using (IDbConnection connection = new SQLiteConnection(SharedDataSourcePath))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache != null) return;

				connection.Execute("INSERT INTO LlmCache (Hash, LlmQuestion) VALUES (@hash, @llmQuestion)",
						new { hash, llmQuestion = JsonConvert.SerializeObject(llmQuestion) });
			}
		}


		public IEnumerable<Setting> GetSettings()
		{
			using (IDbConnection connection = new SQLiteConnection(DataSource))
			{
				var settingsExists = connection.QueryFirstOrDefault<dynamic>("PRAGMA table_info(Settings)");
				if (settingsExists == null)
				{
					CreateSettingsTable(DataSource);
				}

				return connection.Query<Setting>("SELECT * FROM Settings");
			}

		}

		public void Remove(Setting? setting)
		{
			if (setting == null) return;

			using (IDbConnection connection = new SQLiteConnection(DataSource))
			{
				connection.Execute("DELETE FROM Settings WHERE AppId=@AppId AND [ClassOwnerFullName]=@ClassOwnerFullName AND [ValueType]=@ValueType",
					new { setting.AppId, setting.ClassOwnerFullName, setting.ValueType });
				return;
			}
		}


		public void Set(Setting setting)
		{
			using (IDbConnection connection = new SQLiteConnection(DataSource))
			{
				connection.Execute(@"
					INSERT OR IGNORE INTO Settings (AppId, ClassOwnerFullName, ValueType, [Key], [Value], SignatureData, Created) VALUES (@AppId, @ClassOwnerFullName, @ValueType, @Key, @Value, @SignatureData, @Created)
					ON CONFLICT(AppId, [ClassOwnerFullName], [ValueType], [Key]) DO UPDATE SET Value = excluded.Value;
					", new { setting.AppId, setting.ClassOwnerFullName, setting.ValueType, setting.Key, setting.Value, SignatureData = JsonConvert.SerializeObject(setting.SignatureData), setting.Created });

			}
		}

		
	}
}
