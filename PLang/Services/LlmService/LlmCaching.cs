using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils;
using System.Data;

namespace PLang.Services.LlmService
{
	public class LlmCaching(IPLangFileSystem fileSystem, ISettings settings)
	{
		private void CreateLlmCacheTable(string datasource)
		{
			var dbPath = datasource.Between("=", ";");
			if (!File.Exists(dbPath))
			{
				string? dirName = Path.GetDirectoryName(dbPath);
				if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
				{
					var dir = Directory.CreateDirectory(dirName!);
					dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
				}
				else if (string.IsNullOrEmpty(dirName))
				{
					if (File.Exists(dbPath))
					{
						File.Delete(dbPath);
					}
				}
				File.Create(dbPath).Close();
			}
			using (IDbConnection connection = new SqliteConnection(datasource))
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

		public string GetStringToHash(LlmRequest question)
		{
			return question.type + JsonConvert.SerializeObject(question.promptMessage).ComputeHash() + question.model + question.maxLength + question.top_p + question.frequencyPenalty + question.presencePenalty + question.temperature;
		}

		public LlmRequest? GetCachedQuestion(string appId, LlmRequest question)
		{
			var hash = GetStringToHash(question).ComputeHash();

			return GetLlmRequestCache(appId, hash);

		}
		public string GetSharedDataSourcePath(string? appId)
		{
			appId = appId ?? settings.AppId;
			string dataSourcePath = Path.Join(fileSystem.SharedPath, appId, ".db", "system.sqlite");
				
			string dataSource = $"Data Source={dataSourcePath};";
			return dataSource;
			
		}
		public void SetCachedQuestion(string appId, LlmRequest question)
		{
			var hash = GetStringToHash(question).ComputeHash();
			SetLlmRequestCache(appId, hash, question);
		}

		public LlmRequest? GetLlmRequestCache(string appId, string hash)
		{
			var shareDataSourcePath = GetSharedDataSourcePath(appId);
			CreateLlmCacheTable(shareDataSourcePath);
			using (IDbConnection connection = new SqliteConnection(shareDataSourcePath))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache == null) return null;

				connection.Execute("UPDATE LlmCache SET LastUsed=CURRENT_TIMESTAMP WHERE Id=@id", new { id = cache.Id });
				return JsonConvert.DeserializeObject<LlmRequest>(cache.LlmQuestion);
			}
		}
		public void SetLlmRequestCache(string appId, string hash, LlmRequest llmQuestion)
		{
			var shareDataSourcePath = GetSharedDataSourcePath(appId);
			CreateLlmCacheTable(shareDataSourcePath);
			using (IDbConnection connection = new SqliteConnection(shareDataSourcePath))
			{
				var cache = connection.QueryFirstOrDefault<dynamic>("SELECT * FROM LlmCache WHERE Hash=@hash", new { hash });
				if (cache != null) return;

				connection.Execute("INSERT INTO LlmCache (Hash, LlmQuestion) VALUES (@hash, @llmQuestion)",
						new { hash, llmQuestion = JsonConvert.SerializeObject(llmQuestion) });
			}
		}
	}
}
