using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Exceptions.AskUser.Database;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Runtime.Startup.ModuleLoader;

namespace PLang.Modules.DbModule
{
	public class ModuleSettings : IModuleSettings
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IDbConnection db;
		private readonly ILogger logger;

		public record SqlStatement(string SelectTablesAndViewsInMyDatabaseSqlStatement, string SelectColumnsFromTablesSqlStatement);

		public ModuleSettings(IPLangFileSystem fileSystem, ISettings settings, PLangAppContext context, ILlmServiceFactory llmServiceFactory, IDbConnection db, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.context = context;
			this.llmServiceFactory = llmServiceFactory;
			this.db = db;
			this.logger = logger;
		}


		public record DataSource(string Name, string TypeFullName, string ConnectionString, string DbName, string SelectTablesAndViews, string SelectColumns, bool KeepHistory = true, bool IsDefault = false);


		public async Task CreateDataSource(string dataSourceName = "data", string dbType = "", bool setAsDefaultForApp = false, bool keepHistoryEventSourcing = false)
		{
			var dataSources = await GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.Name.ToLower() == dataSourceName.ToLower());
			if (dataSource != null) {
				logger.LogWarning($"Data source with the name '{dataSourceName}' already exists.");
				return;
			}
			var listOfDbSupported = GetSupportedDbTypes();
			if (listOfDbSupported.Count == 1 && dataSources.Count == 0)
			{
				string dbPath = Path.Join(".db", "data.sqlite");
				AppContext.TryGetSwitch(ReservedKeywords.Test, out bool inMemory);
				if (inMemory)
				{
					dbPath = "Data Source=InMemoryDataDb;Mode=Memory;Cache=Shared;";
				}
				await SetDatabaseConnectionString("data", typeof(SqliteConnection).FullName, "data.sqlite", $"Data Source={dbPath};", true, true);
				return;
			}
			
			if (listOfDbSupported.Count == 1)
			{
				throw new AskUserSqliteName(fileSystem.RootDirectory, $"What is the name you want to give to your database?", SetDatabaseConnectionString);
			}

			var supportedDbTypes = GetSupportedDbTypesAsString();
			var system = @$"Map user request

If user provides a full data source connection, return {{error:explainWhyConnectionStringShouldNotBeInCodeMax100Characters}}.

typeFullName: from database types provided, is the type.FullName for IDbConnection in c# for this database type for .net 7
nugetCommand: nuget package name, for running ""nuget install ...""
dataSourceConnectionStringExample: create an example of a connection string for this type of databaseType
regexToExtractDatabaseNameFromConnectionString: generate regex to extract the database name from a connection string from user selected databaseType
";
			string assistant = $"## database types ##\r\n{supportedDbTypes}\r\n## database types ##";

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", system));
			promptMessage.Add(new LlmMessage("assistant", assistant));
			promptMessage.Add(new LlmMessage("user", $"Create {dbType} for data source named: {dataSourceName}"));

			var llmRequest = new LlmRequest("AskUserDatabaseType", promptMessage);
			llmRequest.scheme = TypeHelper.GetJsonSchema(typeof(DatabaseTypeResponse));

			var result = await llmServiceFactory.CreateHandler().Query<DatabaseTypeResponse>(llmRequest);
			if (result == null)
			{
				throw new BuilderException("Could not get information from LLM service. Try again.");
			}
			if (dataSources.Count == 0) setAsDefaultForApp = true;

			await AddDataSource(result.typeFullName, dataSourceName, result.nugetCommand, 
									result.dataSourceConnectionStringExample, result.regexToExtractDatabaseNameFromConnectionString, 
									keepHistoryEventSourcing, setAsDefaultForApp);
		}

		private record DatabaseTypeResponse(string typeFullName, string nugetCommand, string regexToExtractDatabaseNameFromConnectionString, string dataSourceConnectionStringExample);


		public async Task AddDataSource(string typeFullName, string dataSourceName, string nugetCommand,
			string dataSourceConnectionStringExample, string regexToExtractDatabaseNameFromConnectionString, bool keepHistory, bool isDefault = false)
		{
			var datasources = await GetAllDataSources();
			if (datasources.FirstOrDefault(p => p.Name.ToLower() == dataSourceName.ToLower()) != null)
			{
				throw new AskUserDataSourceNameExists(llmServiceFactory, typeFullName, dataSourceName, nugetCommand,
						dataSourceConnectionStringExample, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault,
						$"'{dataSourceName}' already exists. Give me different name if you like to add it.", AddDataSource);
			}

			if (!IsModuleInstalled(typeFullName))
			{
				var listOfDbSupported = GetSupportedDbTypesAsString();
				throw new AskUserDatabaseType(llmServiceFactory, isDefault, keepHistory, listOfDbSupported, dataSourceName, $"{typeFullName} is not supported. Following databases are supported: {listOfDbSupported}. If you need {typeFullName}, you must install it into modules folder in your app using {nugetCommand}.", AddDataSource);
			}


			throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault,
				$@"What is the connection string for {dataSourceName}? This is for {typeFullName}.

This is an example of a connection string:
	{dataSourceConnectionStringExample}

Your connection string needs to be 100% correct to work (LLM will not read this).

Connection string:",
				SetDatabaseConnectionString);

		}


		private async Task SetDatabaseConnectionString(string dataSourceName, string typeFullName,
					string regexToExtractDatabaseNameFromConnectionString, string databaseConnectionString,
					bool keepHistory = true, bool isDefault = false)
		{
			var dbType = GetDbType(typeFullName);
			string? error = Test(dbType, databaseConnectionString);
			if (error != null)
			{
				throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault, $"Could not connect to {databaseConnectionString}. The error message was {error}.\n\nFix the connection string and type in again:\n", SetDatabaseConnectionString);
			}

			var regex = new Regex(regexToExtractDatabaseNameFromConnectionString);
			var dbName = regex.Match(databaseConnectionString).Value;
			if (dbName.Contains("="))
			{
				dbName = dbName.Substring(dbName.IndexOf("=") + 1);
			}
			if (dbName.EndsWith(";"))
			{
				dbName = dbName.TrimEnd(';');
			}

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", @$"
Give me sql statement to list all the tables and views in my database {dbName} on {typeFullName}.
Give me sql statement on how to get all column names and type in a table
Table name should be @TableName, database name is @Database if needed as parameters

Be concise"));

			var llmRequest = new LlmRequest("SetDatabaseConnectionString", promptMessage);
			var statement = await llmServiceFactory.CreateHandler().Query<SqlStatement>(llmRequest);
			if (statement == null)
			{
				throw new BuilderException("Could not get select statement for tables, views and columns. Try again.");
			}
			var dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				isDefault = true;
			}
			var dataSource = dataSources.FirstOrDefault(p => p.Name == dataSourceName);
			if (dataSource != null)
			{
				throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault, $"{dataSourceName} already exists. Please choose a different name.", SetDatabaseConnectionString);
			}
			
			dataSource = new DataSource(dataSourceName, typeFullName, databaseConnectionString.Replace(fileSystem.RootDirectory, ""), dbName,
									statement.SelectTablesAndViewsInMyDatabaseSqlStatement, statement.SelectColumnsFromTablesSqlStatement,
									keepHistory, isDefault);

			dataSources.Add(dataSource);
			settings.SetList(this.GetType(), dataSources);

			if (isDefault || dataSources.Count == 1) {
				AppContext.SetData(ReservedKeywords.Inject_IDbConnection, typeFullName);
				context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, typeFullName);

				AppContext.SetData(ReservedKeywords.CurrentDataSourceName, dataSource);
				context.AddOrReplace(ReservedKeywords.CurrentDataSourceName, dataSource);
			}
		}

		public async Task RemoveDataSource(string dataSourceName)
		{
			var dataSources = await GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.Name == dataSourceName);
			if (dataSource != null)
			{
				dataSources.Remove(dataSource);
			}
			settings.SetList(typeof(ModuleSettings), dataSources);
		}

		public async Task<List<DataSource>> GetAllDataSources()
		{
			return settings.GetValues<DataSource>(this.GetType()).ToList();
		}

		public async Task<DataSource?> GetDataSource(string dataSourceName)
		{
			var dataSources = await GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.Name == dataSourceName);
			context.AddOrReplace(ReservedKeywords.CurrentDataSourceName, dataSource);
			return dataSource;
		}

		public async Task<DataSource> GetCurrentDataSource()
		{
			if (context.ContainsKey(ReservedKeywords.CurrentDataSourceName))
			{
				return context[ReservedKeywords.CurrentDataSourceName] as DataSource;
			}


			var dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				string name = context.ContainsKey(ReservedKeywords.CurrentDataSourceName + "_string") ? context[ReservedKeywords.CurrentDataSourceName + "_string"].ToString() : "data";
				await CreateDataSource(name);
				dataSources = await GetAllDataSources();
			}

			var dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			if (dataSource != null)
			{
				context.AddOrReplace(ReservedKeywords.CurrentDataSourceName, dataSource);
				return dataSource;
			}
			dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				throw new RuntimeException("Could not find any data source.");
			}
			context.AddOrReplace(ReservedKeywords.CurrentDataSourceName, dataSources[0]);
			return dataSources[0];
		}
		private Type GetDbType(string typeFullName)
		{
			var types = GetSupportedDbTypes();
			return types.FirstOrDefault(p => p.FullName == typeFullName);
		}

		public  List<Type> GetSupportedDbTypes()
		{
			var typeHelper = new TypeHelper(fileSystem, settings);
			var types = typeHelper.GetTypesByType(typeof(IDbConnection)).ToList();
			types.Remove(typeof(DbConnectionUndefined));

			if (types.FirstOrDefault(p => p == typeof(SqliteConnection)) == null)
			{
				types.Add(typeof(SqliteConnection));
			}
			return types;
		}
		public string GetSupportedDbTypesAsString()
		{
			var types = GetSupportedDbTypes();
			return "\n- " + string.Join("\n- ", types.Select(p => p.FullName));
		}

		private bool IsModuleInstalled(string typeFullName)
		{
			return GetDbType(typeFullName) != null;
		}

		private IDbConnection GetDbConnection(Type dbType, string connectionString)
		{
			ConstructorInfo? constructor = dbType.GetConstructor(new Type[] { typeof(string) });

			object instance = constructor.Invoke(new object[] { connectionString });
			return instance as IDbConnection;
		}

		private string? Test(Type dbType, string connectionString)
		{
			if (connectionString.Contains(";Mode=Memory;")) return null;
			if (dbType == typeof(SqliteConnection))
			{
				var startIdx = connectionString.IndexOf('=') + 1;
				var endIdx = connectionString.IndexOf(';') - startIdx;
				string filePath = connectionString.Substring(startIdx, endIdx);

				var dbDir = Path.GetDirectoryName(filePath);
				if (!fileSystem.Directory.Exists(dbDir))
				{
					fileSystem.Directory.CreateDirectory(dbDir);
				}

				if (!fileSystem.File.Exists(filePath))
				{
					var stream = fileSystem.File.Create(filePath);
					stream.Close();
				}
			}

			var connection = GetDbConnection(dbType, connectionString);
			try
			{
				connection.Open();

			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			finally
			{
				connection.Close();
			}
			return null;
		}


		public async Task<string> FormatSelectColumnsStatement(string tableName)
		{
			var dataSource = await GetCurrentDataSource();
			string selectColumns = dataSource.SelectColumns.ToLower();

			if (selectColumns.Contains("'@tablename'"))
			{
				selectColumns = selectColumns.Replace("@tablename", tableName);
			}
			else if (selectColumns.Contains("@tablename"))
			{
				selectColumns = selectColumns.Replace("@tablename", "'" + tableName + "'");
			}
			if (selectColumns.Contains("'@database'"))
			{
				selectColumns = selectColumns.Replace("@database", dataSource.DbName);
			}
			else if (selectColumns.Contains("@database"))
			{
				selectColumns = selectColumns.Replace("@database", "'" + dataSource.DbName + "'");
			}
			return selectColumns;
		}

		public async Task<IDbConnection> GetDefaultDbConnection(IServiceFactory factory)
		{
			IDbConnection dbConnection;
			var dataSources = await GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			if (dataSource != null)
			{
				dbConnection = factory.GetInstance<IDbConnection>(dataSource.TypeFullName);
				dbConnection.ConnectionString = dataSource.ConnectionString;
				AppContext.SetData(ReservedKeywords.Inject_IDbConnection, dataSource.TypeFullName);
				context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, dataSource.TypeFullName);
				
				AppContext.SetData(ReservedKeywords.CurrentDataSourceName, dataSource);
				context.AddOrReplace(ReservedKeywords.CurrentDataSourceName, dataSource);
								
				return dbConnection;
			}

			var dbtypes = GetSupportedDbTypes();
			if (dbtypes.Count == 1)
			{
				dbConnection = factory.GetInstance<IDbConnection>(typeof(SqliteConnection).FullName);
				dbConnection.ConnectionString = "DataSource=" + Path.Join(".db", "data.sqlite");
				AppContext.SetData(ReservedKeywords.Inject_IDbConnection, dbConnection.GetType().FullName);
				context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, dbConnection.GetType().FullName);

			}
			else
			{

				dbConnection = factory.GetInstance<IDbConnection>(typeof(DbConnectionUndefined).FullName);
				context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, dbConnection.GetType().FullName);
			}
			return dbConnection;


		}
	}
}
