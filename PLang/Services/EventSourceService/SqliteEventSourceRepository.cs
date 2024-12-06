using Dapper;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Utils;
using System.Data;
using System.Data.Common;
using System.Reflection;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Services.EventSourceService
{
	public class DisableEventSourceRepository : IEventSourceRepository
	{
		public DataSource DataSource { get; set; }

		public async Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null)
		{
			return 0;
		}

		public async Task<(int, IError?)> AddEventSourceData(IDbConnection dbConnection, long id, string data, string keyHash, IDbTransaction? transaction)
		{
			return (0, null);
		}

		public async Task<List<SqliteEventSourceRepository.EventData>> GetUnprocessedData()
		{
			return new();
		}
	}

	public class SqliteEventSourceRepository : IEventSourceRepository
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IEncryption encryption;
		public DataSource DataSource { get; set; }

		public SqliteEventSourceRepository(IPLangFileSystem fileSystem, IEncryption encryption)
		{
			this.fileSystem = fileSystem;
			this.encryption = encryption;
		}


		public record EventRow(long Id, string Data, string Pkey, bool Proccessed);
		public record EventData(string ConnectionString, string Sql, Dictionary<string, object>? Parameters);


		public async Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null)
		{
			var parameterValues = new Dictionary<string, object>();
			if (parameters != null)
			{
				foreach (var paramName in parameters.ParameterNames)
				{
					object obj = parameters.Get<object>(paramName);
					parameterValues.Add(paramName, obj);
				}
			}

			var eventData = new EventData(dbConnection.ConnectionString, sql, parameterValues);
			var encryptedData = encryption.Encrypt(eventData);

			var pkey = encryption.GetKeyHash();

			var generator = new IdGenerator(4);
			var eventId = generator.CreateId();
			var dataId = generator.ElementAt(1);

			CreateEventsDb(dbConnection);

			try
			{
				while (!(await InsertIntoEvent(dbConnection, eventId, encryptedData, pkey, transaction)))
				{
					++eventId;
				}			
				
				var result = await dbConnection.ExecuteAsync(sql, parameters, transaction);

				return result;

			}
			catch (Exception ex)
			{
				throw;
			}

		}

		private async Task<bool> InsertIntoEvent(IDbConnection dbConnection, long eventId, string encryptedData, string pkey, IDbTransaction? transaction)
		{
			try
			{
				await dbConnection.ExecuteAsync("INSERT INTO __Events__ (id, data, key_hash, processed) VALUES (@id, @data, @key_hash, 1)",
					new { id = eventId, data = encryptedData, key_hash = pkey }, transaction);
				return true;
			} catch (Exception ex)
			{
				if (ex.Message.Contains("UNIQUE constraint failed: __Events__.id"))
				{
					return false;
				}
				throw;
			}
		}

		public async Task<List<EventData>> GetUnprocessedData()
		{
			IEnumerable<EventRow> encrytpedEvents;
			using (IDbConnection db = new SqliteConnection(DataSource.ConnectionString))
			{
				encrytpedEvents = await db.QueryAsync<EventRow>("SELECT * FROM __Events__ WHERE processed=0 ORDER BY id");
			}

			var events = new List<EventData>();
			foreach (var encryptedEvent in encrytpedEvents)
			{
				var eventData = encryption.Decrypt<EventData>(encryptedEvent.Data);
				events.Add(eventData);
			}
			return events;
		}

		public async Task MarkAsProccesd(long id)
		{
			using (IDbConnection db = new SqliteConnection(DataSource.ConnectionString))
			{
				await db.ExecuteAsync("UPDATE __Events__ SET processed=1 WHERE id=@id",
				new { id });
			}
		}

		private void CreateEventsDb(IDbConnection dataDb)
		{

			string sql = $@"CREATE TABLE IF NOT EXISTS __Events__ (
									id BIGINT PRIMARY KEY,
									data TEXT NOT NULL,
									key_hash TEXT NOT NULL,
									processed BOOLEAN DEFAULT 0 NOT NULL 
								);";
			dataDb.Execute(sql);

		}

		public async Task<(int, IError?)> AddEventSourceData(IDbConnection dbConnection, long id, string data, string keyHash, IDbTransaction? transaction)
		{
			var decryptedData = encryption.Decrypt<EventData>(data, keyHash);

			var param = new DynamicParameters();
			if (decryptedData.Parameters != null)
			{
				foreach (var item in decryptedData.Parameters)
				{
					param.Add(item.Key, item.Value);
				}
			}

			await dbConnection.ExecuteAsync("INSERT OR IGNORE INTO __Events__ (id, data, key_hash, processed) VALUES (@id, @data, @key_hash, 1) ",
				new { id, data, key_hash = keyHash }, transaction);

			int result = 0;
			try
			{
				result = await dbConnection.ExecuteAsync(decryptedData.Sql, param, transaction);
			} catch (Exception ex)
			{
				string message = ex.ToString().ToLower();
				if (message.Contains("unique constraint failed") || message.Contains("already exists") || message.Contains("duplicate column name"))
				{
					return (result, null);
				}
				return (0, new Error(ex.Message, "SqlError", Exception: ex));
			}

			return (result, null);
		}
	}
}
