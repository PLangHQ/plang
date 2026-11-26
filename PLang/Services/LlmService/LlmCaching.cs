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
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> tablesCreated = new();
		private static readonly object createLock = new();

		// Goals build in parallel and each asks the cache. The file used to be made with File.Create, and a goal that
		// opened it while another still held that handle failed with "being used by another process", which stopped
		// the build. SQLite makes the file itself; the table is made once per file, under a lock.
		private void CreateLlmCacheTable(string datasource)
		{
			if (tablesCreated.ContainsKey(datasource)) return;
			lock (createLock)
			{
				if (tablesCreated.ContainsKey(datasource)) return;

				var dbPath = datasource.Between("=", ";");
				string? dirName = Path.GetDirectoryName(dbPath);
				if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
				{
					var dir = Directory.CreateDirectory(dirName!);
					dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
				}
				CreateTable(datasource);
				tablesCreated[datasource] = true;
			}
		}

		private static void CreateTable(string datasource)
		{
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
			return question.type + JsonConvert.SerializeObject(question.promptMessage).ComputeHash().Hash + question.model + question.maxLength + question.top_p + question.frequencyPenalty + question.presencePenalty + question.temperature + question.scheme;
		}

		public LlmRequest? GetCachedQuestion(string appId, LlmRequest question)
		{
			var hash = GetStringToHash(question).ComputeHash().Hash;

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
			var hash = GetStringToHash(question).ComputeHash().Hash;
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
				var serialized = JsonConvert.SerializeObject(llmQuestion);

				// An answer is only written for a request that went to the model, which is either a miss or
				// a request that asked to skip the cache (Reload). The latest answer replaces the old one,
				// or the next build of the same request would read the answer that was just replaced.
				if (cache != null)
				{
					connection.Execute("UPDATE LlmCache SET LlmQuestion=@llmQuestion, LastUsed=CURRENT_TIMESTAMP WHERE Id=@id",
						new { llmQuestion = serialized, id = (long)cache.Id });
					return;
				}

				connection.Execute("INSERT INTO LlmCache (Hash, LlmQuestion) VALUES (@hash, @llmQuestion)",
						new { hash, llmQuestion = serialized });
			}
		}
	}
}
