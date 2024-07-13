using Dapper;
using IdGen;
using Microsoft.Data.Sqlite;
using PLang.Interfaces;
using PLang.Utils;
using System.ComponentModel.DataAnnotations;
using System.Data;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;

namespace PLang.Services.EventSourceService
{
	public class DisableEventSourceRepository : IEventSourceRepository
	{
		public DataSource DataSource { get; set; }

		public async Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null)
		{
			return 0;
		}

		public async Task<int> AddEventSourceData(string data, string privateKey, IDbTransaction? transaction)
		{
			return 0;
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
		public record EventData(string ConnectionString, string Sql, object? Parameters);


		public async Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null)
		{
			var eventData = new EventData(dbConnection.ConnectionString, sql, parameters);
			var encryptedData = encryption.Encrypt(eventData);

			var pkey = encryption.GetKeyHash();

			var generator = new IdGenerator(2);
			var eventId = generator.ElementAt(0);
			var dataId = generator.ElementAt(1);

			CreateEventsDb(dbConnection);

			try
			{

				await dbConnection.ExecuteAsync("INSERT INTO __Events__ (id, data, key_hash, processed) VALUES (@id, @data, @key_hash, 1)",
				new { id = eventId, data = encryptedData, key_hash = pkey }, transaction);
			
				var result = await dbConnection.ExecuteAsync(sql, parameters, transaction);

				return result;

			}
			catch (Exception ex)
			{
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

		public async Task<int> AddEventSourceData(string data, string privateKey, IDbTransaction? transaction)
		{
			int i = 0;

			return i;
		}
	}
}
