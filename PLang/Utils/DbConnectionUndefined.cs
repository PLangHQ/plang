using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PLang.Exceptions;
using PLang.Interfaces;
using System.Data;

namespace PLang.Utils
{
	internal class DbConnectionUndefined : IDbConnection
	{
		string errorMessage = "";
		public DbConnectionUndefined(IPLangFileSystem fileSystem, ISettings settings, ITypeHelper typeHelper)
		{
			var types = typeHelper.GetTypesByType(typeof(IDbConnection)).ToList();
			types.Remove(typeof(DbConnectionUndefined));
			if (types.FirstOrDefault(p => p == typeof(SqliteConnection)) == null)
			{
				types.Add(typeof(SqliteConnection));
			}

			string databaseTypes = $@"These database types are available
";
			foreach (var db in types)
			{
				databaseTypes += $" - {db.Name} ({db.FullName})";
			}

			errorMessage = @$"Multiple database types are available, I dont know which one to use. 
			You must create a step, for example:
- create data source 'data' using sqlite, make it default and keep history

{databaseTypes}
";
		}

		public string ConnectionString { get => throw new RuntimeException(errorMessage); set => throw new RuntimeException(errorMessage); }

		public int ConnectionTimeout => throw new RuntimeException(errorMessage);

		public string Database => throw new RuntimeException(errorMessage);

		public ConnectionState State => throw new RuntimeException(errorMessage);

		public IDbTransaction BeginTransaction()
		{
			throw new RuntimeException(errorMessage);
		}

		public IDbTransaction BeginTransaction(IsolationLevel il)
		{
			throw new RuntimeException(errorMessage);
		}

		public void ChangeDatabase(string databaseName)
		{
			throw new RuntimeException(errorMessage);
		}

		public void Close()
		{
			throw new RuntimeException(errorMessage);
		}

		public IDbCommand CreateCommand()
		{
			throw new RuntimeException(errorMessage);
		}

		public void Dispose()
		{
			throw new RuntimeException(errorMessage);
		}

		public void Open()
		{
			throw new RuntimeException(errorMessage);
		}
	}
}
