using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509.Qualified;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Exceptions.AskUser.Database;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Data;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Modules.DbModule
{
	public class ModuleSettings : IModuleSettings
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;
		private readonly PrParser prParser;
		private string defaultLocalDbPath = "./.db/data/data.sqlite";
		public record SqlStatement(string SelectTablesAndViewsInMyDatabaseSqlStatement, string SelectColumnsFromTablesSqlStatement);

		public ModuleSettings(IPLangFileSystem fileSystem, ISettings settings, PLangAppContext context, ILlmServiceFactory llmServiceFactory,
			ILogger logger, ITypeHelper typeHelper, PrParser prParser)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.context = context;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.typeHelper = typeHelper;
			this.prParser = prParser;
			
		}


		public record DataSource(string Name, string TypeFullName, string ConnectionString, string DbName, string SelectTablesAndViews, string SelectColumns, bool KeepHistory = true, bool IsDefault = false, string? LocalPath = null)
		{
			public bool IsDefault { get; set; } = IsDefault;
		}


		public async Task<(DataSource?, IError?)> CreateDataSource(string dataSourceName = "data", string dbType = "sqlite", bool setAsDefaultForApp = false, bool keepHistoryEventSourcing = false)
		{
			var dataSources = await GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.Name.Equals(dataSourceName, StringComparison.OrdinalIgnoreCase));
			if (dataSource != null && setAsDefaultForApp == dataSource.IsDefault && keepHistoryEventSourcing == dataSource.KeepHistory)
			{
				return (dataSource, new Error($"Data source with the name '{dataSourceName}' already exists."));
			}
			if (dataSource != null)
			{
				if (dataSource.IsDefault != setAsDefaultForApp || dataSource.KeepHistory != keepHistoryEventSourcing)
				{
					dataSource = dataSource with { IsDefault = setAsDefaultForApp, KeepHistory = keepHistoryEventSourcing };
					await AddDataSourceToSettings(dataSource);
				}
				return (dataSource, null);
			}

			if (dbType == "sqlite" || dbType == typeof(SqliteConnection).FullName)
			{
				if (dataSourceName.Contains(".db", StringComparison.OrdinalIgnoreCase))
				{
					string formattedDataSource = dataSourceName.AdjustPathToOs().Replace(".db", "").Replace(Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString(), "");
					return (null, new ProgramError("Data source name should not contain .db. Use a different name.", FixSuggestion: $"Use a different name for your data source, e.g. '{formattedDataSource}'"));
				}
				if (dataSourceName.StartsWith("/"))
				{
					dataSourceName = dataSourceName.TrimStart('/');
				}

				(dataSourceName, _) = GetNameAndPathByVariable(dataSourceName, "");

				string? fileName = "/data.sqlite";
				string ext = fileSystem.Path.GetExtension(dataSourceName);
				if (!string.IsNullOrEmpty(ext)) fileName = null;

				var goal = prParser.GetAllGoals().FirstOrDefault(p => p.DataSourceName != null && p.DataSourceName.Equals(dataSourceName));
				if (goal != null)
				{
					var instruction = prParser.GetInstructions(goal.GoalSteps, "CreateDataSource").FirstOrDefault();
					if (instruction != null)
					{
						var gf = instruction.GetFunctions().FirstOrDefault();

						setAsDefaultForApp = GeneralFunctionHelper.GetParameterValueAsBool(gf, "setAsDefaultForApp") ?? false;
						keepHistoryEventSourcing = GeneralFunctionHelper.GetParameterValueAsBool(gf, "keepHistoryEventSourcing") ?? false;
					}
				}
				var localPath = $"/.db/{dataSourceName}{fileName}".AdjustPathToOs();
				return await CreateSqliteDataSource(dataSourceName, localPath, setAsDefaultForApp, keepHistoryEventSourcing);
			}

			var listOfDbSupported = GetSupportedDbTypes();
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

			(var result, var queryError) = await llmServiceFactory.CreateHandler().Query<DatabaseTypeResponse>(llmRequest);
			if (result == null)
			{
				return (null, new BuilderError("Could not get information from LLM service. Try again."));
			}
			if (dataSources.Count == 0) setAsDefaultForApp = true;

			return await AddDataSource(result.typeFullName, dataSourceName, result.nugetCommand,
									result.dataSourceConnectionStringExample, result.regexToExtractDatabaseNameFromConnectionString,
									keepHistoryEventSourcing, setAsDefaultForApp);
		}

		private async Task<(DataSource?, IError?)> CreateSqliteDataSource(string dataSourceName, string localPath, bool setAsDefaultForApp, bool keepHistoryEventSourcing)
		{

			string fileName = fileSystem.Path.GetFileName(localPath);
			if (string.IsNullOrEmpty(fileName))
			{
				return (null, new Error($"You need to define the path with a file name",
					FixSuggestion: $"When you create the datasource include the path, e.g. '- create datasource name: {dataSourceName}"));
			}

			localPath = localPath.Replace(fileSystem.RootDirectory, "").AdjustPathToOs();
			string dataSourcePath = "";
			AppContext.TryGetSwitch(ReservedKeywords.Test, out bool inMemory);
			AppContext.TryGetSwitch("Builder", out bool isBuilder);

			if (inMemory)
			{
				dataSourcePath = "Data Source=InMemoryDataDb;Mode=Memory;Cache=Shared;";
			}
			else if (!dataSourceName.Contains("%"))
			{
				string dirPath = Path.GetDirectoryName(localPath) ?? "/.db/";
				if (!fileSystem.Directory.Exists(dirPath))
				{
					fileSystem.Directory.CreateDirectory(dirPath);
				}
				if (!fileSystem.File.Exists(localPath))
				{
					using (var fs = fileSystem.File.Create(localPath))
					{
						fs.Close();
					}
				}
			}

			dataSourcePath = $"Data Source=.{localPath.TrimStart('.')};";
			var dataSource = GetSqliteDataSource(dataSourceName, dataSourcePath, localPath, keepHistoryEventSourcing, setAsDefaultForApp);

			await AddDataSourceToSettings(dataSource);

			return (dataSource, null);
		}

		private (string dataSourceName, string dataSourcePath) GetNameAndPathByVariable(string dataSourceName, string? dataSourcePath)
		{
			var varToMatch = dataSourceName ?? dataSourcePath;
			if (varToMatch == null) return (dataSourceName, dataSourcePath);

			var matches = Regex.Matches(varToMatch, @"%[\p{L}\p{N}#+-\[\]_\.\+\(\)\*\<\>\!\s\""]*%");
			if (matches.Count == 0) return (dataSourceName, dataSourcePath);

			int count = 0;
			foreach (Match match in matches)
			{
				dataSourceName = dataSourceName?.Replace(match.Value, $"%variable{count}%");
				dataSourcePath = dataSourcePath?.Replace(match.Value, $"%variable{count}%");

				count++;
			}

			return (dataSourceName, dataSourcePath);
		}

		private DataSource GetSqliteDataSource(string name, string dataSourceUri, string localPath, bool history = true, bool isDefault = false)
		{
			var statement = new SqlStatement("SELECT name FROM sqlite_master WHERE type IN ('table', 'view');", "SELECT name, type, [notnull] as isNotNull, pk as isPrimaryKey FROM pragma_table_info(@TableName);");

			var dataSource = new DataSource(name, typeof(SqliteConnection).FullName, dataSourceUri, "data",
				statement.SelectTablesAndViewsInMyDatabaseSqlStatement, statement.SelectColumnsFromTablesSqlStatement,
				history, isDefault, localPath);
			return dataSource;
		}
		private record DatabaseTypeResponse(string typeFullName, string nugetCommand, string regexToExtractDatabaseNameFromConnectionString, string dataSourceConnectionStringExample);


		public async Task<(DataSource?, IError?)> AddDataSource(string typeFullName, string dataSourceName, string nugetCommand,
			string dataSourceConnectionStringExample, string regexToExtractDatabaseNameFromConnectionString, bool keepHistory, bool isDefault = false)
		{
			var datasources = await GetAllDataSources();
			if (datasources.FirstOrDefault(p => p.Name.ToLower() == dataSourceName.ToLower()) != null)
			{
				return (null, new Error($"Data source with the name '{dataSourceName}' already exists.",
						FixSuggestion: $"You must write a different name for your {dataSourceName} data source in your step.\nYou can defined such:\n-create datasource name:MyCustomName"));
			}

			if (!IsModuleInstalled(typeFullName))
			{
				var listOfDbSupported = GetSupportedDbTypesAsString();
				throw new AskUserDatabaseType(llmServiceFactory, isDefault, keepHistory, listOfDbSupported, dataSourceName, $"{typeFullName} is not supported. Following databases are supported: {listOfDbSupported}.\n\n If you need {typeFullName}, you must install it into modules folder in your app using {nugetCommand}.", AddDataSource);
			}


			throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault,
				$@"What is the connection string for {dataSourceName}? This is for {typeFullName}.

This is an example of a connection string:
	{dataSourceConnectionStringExample}

Your connection string needs to be 100% correct to work (LLM will not read this).

Connection string:",
				SetDatabaseConnectionString);

		}


		private async Task<IError?> SetDatabaseConnectionString(string dataSourceName, string typeFullName,
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

			SqlStatement? statement = null;
			if (dbType.Name == "SqliteConnection")
			{
				statement = new SqlStatement("SELECT name FROM sqlite_master WHERE type IN ('table', 'view');", "SELECT name, type, [notnull] as isNotNull, pk as isPrimaryKey FROM pragma_table_info(@TableName);");
			}
			else
			{
				var promptMessage = new List<LlmMessage>();
				promptMessage.Add(new LlmMessage("system", @$"
Give me sql statement to list all the tables and views in my database {dbName} on {typeFullName}.
Give me sql statement on how to get all column names and type in a table
Table name should be @TableName, database name is @Database if needed as parameters

If you need to do subquery, make sure to use IN statement incase the subquery returns multiple rows
Be concise"));

				var llmRequest = new LlmRequest("SetDatabaseConnectionString", promptMessage);
				(statement, var queryError) = await llmServiceFactory.CreateHandler().Query<SqlStatement>(llmRequest);
				if (queryError != null)
				{
					return queryError;
				}
			}

			if (statement == null)
			{
				throw new BuilderException("Could not get select statement for tables, views and columns. Try again.");
			}
			DataSource dataSource = new DataSource(dataSourceName, typeFullName, databaseConnectionString.Replace(fileSystem.RootDirectory, ""), dbName,
										statement.SelectTablesAndViewsInMyDatabaseSqlStatement, statement.SelectColumnsFromTablesSqlStatement,
										keepHistory, isDefault);

			List<DataSource> dataSources = await AddDataSourceToSettings(dataSource);

			if (isDefault || dataSources.Count == 1)
			{
				AppContext.SetData(ReservedKeywords.Inject_IDbConnection, typeFullName);
				context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, typeFullName);

				AppContext.SetData(ReservedKeywords.CurrentDataSource, dataSource);
				context.AddOrReplace(ReservedKeywords.CurrentDataSource, dataSource);
			}
			return null;
		}

		private async Task<List<DataSource>> AddDataSourceToSettings(DataSource dataSource)
		{
			// todo: This needs to move to its own table, Datasource table, to expensive to load all datasources using json when thousands of datasources are avialable
			var dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				dataSource.IsDefault = true;
			}
			else if (dataSource.IsDefault)
			{
				dataSources.ForEach(p => p.IsDefault = false);
			}

			var dataSourceIdx = dataSources.FindIndex(p => p.Name == dataSource.Name);
			if (dataSourceIdx != -1)
			{
				dataSources[dataSourceIdx] = dataSource;
			}
			else
			{
				dataSources.Add(dataSource);
			}


			settings.SetList(this.GetType(), dataSources);
			return dataSources;
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
		public async Task<(DataSource? DataSource, IError? Error)> GetDefaultDataSource(GoalStep? step = null)
		{
			var dataSources = await GetAllDataSources();

			var dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			if (dataSource == null)
			{
				await CreateSqliteDataSource("data", defaultLocalDbPath, true, true);
				dataSources = await GetAllDataSources();

				dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			}

			AppContext.TryGetSwitch("Builder", out bool isBuilder);
			if (isBuilder)
			{
				dataSource = dataSource with { ConnectionString = $"Data Source={dataSource.Name};Mode=Memory;Cache=Shared;" };
				return (dataSource, null);
			}

			return (dataSource, null);
		}
		public async Task<(DataSource? DataSource, IError? Error)> GetDataSource(string name, GoalStep? step = null)
		{

			if (string.IsNullOrEmpty(name))
			{
				return (null, new ProgramError("You need to provide a data source name", step));
			}
			if (name.StartsWith("/"))
			{
				name = name.TrimStart('/');
			}
			var dataSources = await GetAllDataSources();
			var dataSourceName = name;
			if (name.Contains("%"))
			{
				(dataSourceName, _) = GetNameAndPathByVariable(name, null);
			}

			var dataSource = dataSources.FirstOrDefault(p => p.Name.Equals(dataSourceName, StringComparison.OrdinalIgnoreCase));

			if (dataSource == null)
			{
				return (null, await GetDataSourceNotFoundError(name, step));
			}

			if (dataSource.TypeFullName != typeof(SqliteConnection).FullName) return (dataSource, null);


			AppContext.TryGetSwitch("Builder", out bool isBuilder);
			if (isBuilder)
			{
				dataSource = dataSource with { ConnectionString = $"Data Source={dataSource.Name};Mode=Memory;Cache=Shared;" };
				return (dataSource, null);
			}

			if (!string.IsNullOrEmpty(dataSource.LocalPath)
					&& !dataSource.LocalPath.Contains("%")
					&& (!isBuilder && !fileSystem.File.Exists(dataSource.LocalPath)))
			{
				using (var fs = fileSystem.File.Create(dataSource.LocalPath))
				{
					fs.Close();
				}

			}


			return (dataSource, null);
		}

		public async Task<Error> GetDataSourceNotFoundError(string name, GoalStep? step)
		{
			var dataSources = await GetAllDataSources();
			logger.LogDebug("Datasources: {0}", JsonConvert.SerializeObject(dataSources));

			string stepExample = (step != null) ? step.Text : "set datasource name:myCustomDb";
			string existingDatasources = "";
			if (dataSources.Count > 0)
			{
				existingDatasources = "These are the available datasources:\n" + string.Join("\n", dataSources.Select(p => $"\t- Name:{p.Name} - Path:{p.LocalPath}"));
			}
			else
			{
				existingDatasources = "No datasources available";
			}

			return new Error($"Datasource {name} does not exists", Key: "DataSourceNotFound",
						FixSuggestion: $@"{existingDatasources}

Create a step that creates a new Data source, e.g.
- create datasource, name: myCustomDb, path '/.db/my_custom_data.sqlite'

or you can catch this error and create it on this error

- {stepExample}, 
	on error key=DataSourceNotFound, call CreateDataSource and retry

where the goal CreateDataSource would create the database and table
", HelpfulLinks: "https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.DbModule.md");
		}

		public async Task<DataSource> GetCurrentDataSource()
		{
			if (context.ContainsKey(ReservedKeywords.CurrentDataSource + "_name"))
			{
				string name = context[ReservedKeywords.CurrentDataSource + "_name"].ToString();
				(var ds, _) = await GetDataSource(name, null);

				if (ds != null)
				{
					context.AddOrReplace(ReservedKeywords.CurrentDataSource, ds);
					return ds;
				}
			}

			if (context.ContainsKey(ReservedKeywords.CurrentDataSource))
			{
				var ds = context[ReservedKeywords.CurrentDataSource] as DataSource;
				if (ds != null) return ds;
			}

			var dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				string name = context.ContainsKey(ReservedKeywords.CurrentDataSource + "_name") ? context[ReservedKeywords.CurrentDataSource + "_name"].ToString() : "data";
				await CreateDataSource(name);
				dataSources = await GetAllDataSources();
			}

			var dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			if (dataSource != null)
			{
				context.AddOrReplace(ReservedKeywords.CurrentDataSource, dataSource);
				return dataSource;
			}
			dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				throw new RuntimeException("Could not find any data source.");
			}
			context.AddOrReplace(ReservedKeywords.CurrentDataSource, dataSources[0]);
			return dataSources[0];
		}
		private Type? GetDbType(string typeFullName)
		{
			var types = GetSupportedDbTypes();
			return types.FirstOrDefault(p => p.FullName == typeFullName);
		}

		public List<Type> GetSupportedDbTypes()
		{
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
					using var stream = fileSystem.File.Create(filePath);
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

				AppContext.SetData(ReservedKeywords.CurrentDataSource, dataSource);
				context.AddOrReplace(ReservedKeywords.CurrentDataSource, dataSource);

				return dbConnection;
			}

			var dbtypes = GetSupportedDbTypes();
			if (dbtypes.Count == 1)
			{
				await CreateDataSource("data", "sqlite", true, true);
				return await GetDefaultDbConnection(factory);
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
