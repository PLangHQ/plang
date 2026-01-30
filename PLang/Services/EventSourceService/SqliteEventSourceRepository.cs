using Dapper;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PLang.Errors;
using PLang.Errors.Runtime;
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

		public async Task<long> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null, bool returnId = false)
		{
			var result = await dbConnection.ExecuteAsync(sql, parameters, transaction);
			return result;
		}

	}

	public class SqliteEventSourceRepository : IEventSourceRepository
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IEncryption encryption;

		public SqliteEventSourceRepository(IPLangFileSystem fileSystem, IEncryption encryption)
		{
			this.fileSystem = fileSystem;
			this.encryption = encryption;
		}


		public record EventRow(long Id, string Data, string Pkey, bool Proccessed);
		public record EventData(string ConnectionString, string Sql, Dictionary<string, object>? Parameters);


		public async Task<long> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null, bool returnId = false)
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
				bool success = false;
				while (!success)
				{
					(success, var error) = await InsertIntoEvent(dbConnection, eventId, encryptedData, pkey, transaction);
					if (error != null) throw new ExceptionWrapper(error);

					++eventId;
				}

                return await InsertData(dbConnection, sql, parameters, transaction, returnId);

			}
			catch (Exception ex)
			{
				throw;
			}

		}

		private async Task<long> InsertData(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction, bool returnId, int retryCount = 0)
		{
			try
			{
				if (returnId)
				{
					var result = await dbConnection.QuerySingleOrDefaultAsync<long>(sql, parameters, transaction);

					return result;
				}
				else
				{
					var result = await dbConnection.ExecuteAsync(sql, parameters, transaction);
					return result;
				}
			} catch (Exception ex)
			{
				if (ex.Message.Contains("database is locked") && retryCount < 3)
				{
					await Task.Delay(new Random().Next(50));
					return await InsertData(dbConnection, sql, parameters, transaction, returnId, ++retryCount);
				}

				throw;
			}
		}

		private async Task<(bool, IError?)> InsertIntoEvent(IDbConnection dbConnection, long eventId, string encryptedData, string pkey, IDbTransaction? transaction, int retryCount = 0)
		{
			try
			{
				int affectedRows = await dbConnection.ExecuteAsync("INSERT INTO __Events__ (id, data, key_hash, processed) VALUES (@id, @data, @key_hash, 1)",
					new { id = eventId, data = encryptedData, key_hash = pkey }, transaction);
				if (affectedRows > 0) return (true, null);
				return (false, new ServiceError("Nothing inserted into __Events__ table. This is unexpected.", GetType()));
			} catch (Exception ex)
			{
				if (ex.Message.Contains("UNIQUE constraint failed: __Events__.id"))
				{
					return (false, null);
				} else if (ex.Message.Contains("database is locked") && retryCount < 3)
				{
					await Task.Delay(new Random().Next(50));
					return await InsertIntoEvent(dbConnection, eventId, encryptedData, pkey, transaction, ++retryCount);
				}
				throw;
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

	}
}
