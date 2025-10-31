using Dapper;
using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509.Qualified;
using PLang.Attributes;
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
using PLang.Services.DbService;
using PLang.Services.LlmService;
using PLang.Utils;
using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Utils.VariableHelper;

namespace PLang.Modules.DbModule
{
	public class ModuleSettings : IModuleSettings
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly PLangAppContext appContext;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;
		private readonly PrParser prParser;
		private readonly IPLangContextAccessor contextAccessor;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;
		private readonly IDbServiceFactory dbFactory;
		private readonly IAppCache appCache;
		private readonly PLangContext context;
		private string defaultLocalDbPath = "./.db/data/data.sqlite";
		public record SqlStatement(string SelectTablesAndViewsInMyDatabaseSqlStatement, string SelectColumnsFromTablesSqlStatement);

		public ModuleSettings(IPLangFileSystem fileSystem, ISettings settings, PLangAppContext appContext, ILlmServiceFactory llmServiceFactory,
			ILogger logger, ITypeHelper typeHelper, PrParser prParser, IPLangContextAccessor contextAccessor, VariableHelper variableHelper, IDbServiceFactory dbFactory, IAppCache appCache)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.appContext = appContext;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.typeHelper = typeHelper;
			this.prParser = prParser;
			this.contextAccessor = contextAccessor;
			this.memoryStack = contextAccessor.Current.MemoryStack;
			this.variableHelper = variableHelper;
			this.dbFactory = dbFactory;
			this.appCache = appCache;
			context = contextAccessor.Current;
			AppContext.TryGetSwitch(ReservedKeywords.Test, out bool inMemory);
			UseInMemoryDataSource = inMemory;
		}

		public bool UseInMemoryDataSource { get; set; } = false;
		public bool IsBuilder { get; internal set; }

		public record DataSource(string Name, string TypeFullName, string ConnectionString, string DbName, string SelectTablesAndViews, string SelectColumns, bool KeepHistory = true, bool IsDefault = false, string? LocalPath = null)
		{
			public bool IsDefault { get; set; } = IsDefault;
			public bool KeepHistory { get; set; } = KeepHistory;
			[LlmIgnore]
			public string NameInStep { get; set; }

			[System.Text.Json.Serialization.JsonIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public List<string> AttachedDbs { get; set; } = new();
			[System.Text.Json.Serialization.JsonIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public bool IsInTransaction { get { return _transaction != null; } }
			[System.Text.Json.Serialization.JsonIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public string? TransactionStartGoal { get; set; }

			[System.Text.Json.Serialization.JsonIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public IDbTransaction? Transaction
			{
				get { return _transaction; }
				set
				{
					_transaction = value;
					if (value == null) TransactionStartGoal = null;
				}
			}
			private IDbTransaction? _transaction;
		}

		public async Task<(DataSource?, IError?)> CreateOrUpdateDataSource(string dataSourceName = "data", string dbType = "sqlite", bool setAsDefaultForApp = false, bool keepHistoryEventSourcing = false)
		{
			var result = await CreateDataSource(dataSourceName, dbType, setAsDefaultForApp, keepHistoryEventSourcing);
			if (result.Error != null && result.Error.Key == "DataSourceExists")
			{
				var dataSource = result.DataSource!;

				dataSource.IsDefault = setAsDefaultForApp;
				dataSource.KeepHistory = keepHistoryEventSourcing;
				await AddDataSourceToSettings(dataSource);

				return (dataSource, null);
			}
			return result;
		}
		public async Task<(DataSource? DataSource, IError? Error)> CreateDataSource(string dataSourceName = "data", string dbType = "sqlite", bool setAsDefaultForApp = false, bool keepHistoryEventSourcing = false)
		{
			if (dataSourceName == "%variable0%") { throw new Exception("Why?"); }

			var dataSources = await GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.Name.Equals(dataSourceName, StringComparison.OrdinalIgnoreCase));
			if (dataSource != null && setAsDefaultForApp == dataSource.IsDefault && keepHistoryEventSourcing == dataSource.KeepHistory)
			{
				return (dataSource, new Error($"Data source with the name '{dataSourceName}' already exists.", Key: "DataSourceExists"));
			}
			if (dataSource != null)
			{
				if (dataSource.IsDefault != setAsDefaultForApp || dataSource.KeepHistory != keepHistoryEventSourcing)
				{
					dataSource = dataSource with { IsDefault = setAsDefaultForApp, KeepHistory = keepHistoryEventSourcing };
					await AddDataSourceToSettings(dataSource);
				}
				return await ProcessDataSource(dataSource);
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
					var instructionResult = prParser.GetInstructions(goal.GoalSteps, "CreateDataSource");
					if (instructionResult.Error != null) return (null, instructionResult.Error);

					var instruction = instructionResult.Instructions!.FirstOrDefault();
					if (instruction != null)
					{
						var gf = instruction.Function;

						setAsDefaultForApp = GenericFunctionHelper.GetParameterValueAsBool(gf, "setAsDefaultForApp") ?? false;
						keepHistoryEventSourcing = GenericFunctionHelper.GetParameterValueAsBool(gf, "keepHistoryEventSourcing") ?? false;
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


			if (!dataSourceName.Contains("%"))
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

			return await ProcessDataSource(dataSource);
		}

		public static string? ConvertDataSourceNameInStep(string? dataSourceNameInStep)
		{
			if (string.IsNullOrEmpty(dataSourceNameInStep) || !dataSourceNameInStep.Contains("%")) return dataSourceNameInStep;

			var matches = Regex.Matches(dataSourceNameInStep, @"%[\p{L}\p{N}#+-\[\]_\.\+\(\)\*\<\>\!\s\""]*%", RegexOptions.Compiled);
			if (matches.Count == 0) return dataSourceNameInStep;

			int count = 0;
			foreach (Match match in matches)
			{
				dataSourceNameInStep = dataSourceNameInStep.Replace(match.Value, $"%variable{count++}%");
			}

			return dataSourceNameInStep;
		}

		public (string dataSourceName, string? dataSourcePath) GetNameAndPathByVariable(string dataSourceName, string? dataSourcePath)
		{
			if (!dataSourceName.Contains("%")) return (dataSourceName, dataSourcePath);

			var ov = memoryStack.GetObjectValue(dataSourceName);
			if (ov.Initiated) return (ov.ValueAs<string>()!, dataSourcePath);

			var varToMatch = dataSourceName ?? dataSourcePath;

			dataSourceName = ConvertDataSourceNameInStep(dataSourceName);
			dataSourcePath = ConvertDataSourceNameInStep(dataSourcePath);
			
			return (dataSourceName, dataSourcePath);
		}
		private SqlStatement GetSqliteDbInfoStatement()
		{
			var statement = new SqlStatement("SELECT name FROM sqlite_master WHERE type IN ('table', 'view');", "SELECT name, type, [notnull] as isNotNull, pk as isPrimaryKey, dflt_value as default_value FROM pragma_table_xinfo(@TableName);");
			return statement;
		}

		private DataSource GetSqliteDataSource(string name, string dataSourceUri, string localPath, bool history = true, bool isDefault = false)
		{
			var statement = GetSqliteDbInfoStatement();

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
				statement = GetSqliteDbInfoStatement();
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
				appContext.AddOrReplace(ReservedKeywords.Inject_IDbConnection, typeFullName);
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

		public async Task<List<DataSource>> GetAllDataSourcesForBuilder()
		{
			var dataSources = settings.GetValues<DataSource>(this.GetType()).ToList();
			for (int i = 0; i < dataSources.Count; i++)
			{
				(dataSources[i], _) = await ProcessDataSource(dataSources[i]);
			}
			return dataSources;
		}

		public async Task<List<DataSource>> GetAllDataSources()
		{
			var dataSources = settings.GetValues<DataSource>(this.GetType()).OrderByDescending(p => p.IsDefault).ToList();
			return dataSources;
		}

		public async Task<(DataSource? DataSource, IError? Error)> GetDataSourceOrDefault()
		{
			// first check if datasource is on context
			if (context.TryGetValue(Program.CurrentDataSourceKey, out DataSource? dataSource) && dataSource != null)
			{
				return (dataSource, null);
			}

			var dataSources = await GetAllDataSources();
			dataSource = dataSources.FirstOrDefault(p => p.IsDefault);

			if (dataSource == null)
			{
				(dataSource, var error) = await CreateDataSource("data", setAsDefaultForApp: true, keepHistoryEventSourcing: true);
				if (error != null) return (dataSource, error);
			}

			return await ProcessDataSource(dataSource);
		}

		public async Task<(DataSource? DataSource, IError? Error)> GetDataSource(string? name, GoalStep? step = null, bool transactionDependant = true)
		{
			DataSource? dataSource;
			if (string.IsNullOrEmpty(name))
			{
				if (context.TryGetValue(Program.CurrentDataSourceKey, out dataSource) && dataSource != null)
				{
					return (dataSource, null);
				}

				return (null, new ProgramError("You need to provide a data source name", step));
			}
			if (name.StartsWith("/"))
			{
				name = name.TrimStart('/');
			}
			if (context.TryGetValue(Program.CurrentDataSourceKey, out dataSource) && dataSource != null)
			{
				if (dataSource.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return (dataSource, null);
				if (transactionDependant && dataSource.AttachedDbs.FirstOrDefault(p => p.Equals(name, StringComparison.OrdinalIgnoreCase)) != null)
				{
					return (dataSource, null);
				}
			}

			var dataSources = await GetAllDataSources();
			var dsNameAndPath = GetNameAndPathByVariable(name, null);
			dataSource = dataSources.FirstOrDefault(p => p.Name.Equals(dsNameAndPath.dataSourceName, StringComparison.OrdinalIgnoreCase));

			if (dataSource == null)
			{
				(dataSource, var error) = await GetDataSourceNotFoundError(dsNameAndPath.dataSourceName, step);
				if (dataSource == null) return (null, error);
			}

			dataSource = dataSource with { NameInStep = name };
			if (!IsBuilder && name.Contains("%"))
			{
				var dataSourceDynamicName = memoryStack.LoadVariables(name);
				string cacheKey = "__plang_DataSource__" + dataSourceDynamicName;
				var obj = await appCache.Get("__plang_DataSource__" + dataSourceDynamicName);
				if (obj != null)
				{
					dataSource = (DataSource)obj;
				}
				else
				{
					(dataSource, var error) = await InitiateDatabase(dataSource, name, cacheKey);
					if (error != null) return (dataSource, error);
				}
			}

			return await ProcessDataSource(dataSource);

		}

		private async Task<(DataSource?, IError?)> InitiateDatabase(DataSource dataSource, string name, string cacheKey)
		{
			var dataSourceVariables = variableHelper.GetVariables(dataSource.Name, memoryStack);
			string localPath = dataSource.LocalPath;
			string connectionString = dataSource.ConnectionString;

			var variables = variableHelper.GetVariables(name, memoryStack);
			var emptyVariables = variables.Where(p => p.IsEmpty);
			if (emptyVariables.Any())
			{
				string emptyVars = string.Join(", ", emptyVariables.Select(p => p.Name).ToArray());
				return (null, new ProgramError($"Could not load all variables. {emptyVars} is empty."));
			}


			for (int i = 0; i < variables.Count; i++)
			{
				if (variables[i].Value == null || string.IsNullOrEmpty(variables[i].Value?.ToString()))
				{
					return (null, new Error($"Variable {variables[i].Name} has not been set.", "UndefinedVariable"));
				}

				localPath = localPath.Replace($"%variable{i}%", variables[i].Value.ToString());
				connectionString = connectionString.Replace($"%variable{i}%", variables[i].Value.ToString());
			}

			string dirPath = fileSystem.Path.GetDirectoryName(localPath);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}

			if (!fileSystem.File.Exists(localPath))
			{
				var error = await CreateDatabase(localPath, connectionString, dataSource.Name);
				if (error != null) return (dataSource, error);
			}
			else
			{

				var connection = new SqliteConnection(connectionString);
				await connection.OpenAsync();
				var result = await connection.QueryFirstOrDefaultAsync("SELECT value FROM __Variables__ WHERE variable='SetupHash'");
				if (result == null)
				{
					if (dataSource.IsDefault) return (dataSource, null);
					return (dataSource, new Error("SetupHash is missing from __Variables__"));
				}

				var setupHashKey = "__plang_SetupHash_" + dataSource.Name;
				var value = result.value;
				var setupCache = await appCache.Get(setupHashKey);
				if (value.ToString() != setupCache?.ToString())
				{
					var transaction = await connection.BeginTransactionAsync();
					try
					{
						IDbConnection? anchorDb = null;
						var anchors = appContext.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb");
						if (anchors != null && anchors.TryGetValue(dataSource.Name, out anchorDb))
						{
							anchorDb.Close();
						}
						var error = await ExecuteSetup(transaction, dataSource.Name);
						if (error != null)
						{
							await transaction.RollbackAsync();
							return (null, error);
						}
						else
						{
							await transaction.CommitAsync();

							if (anchorDb != null)
							{
								anchorDb.Open();
							}
						}
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();
						return (null, new ExceptionError(ex, ex.Message));
					}
					finally
					{
						await connection.CloseAsync();
					}


				}
			}

			dataSource = dataSource with { LocalPath = localPath, ConnectionString = connectionString, NameInStep = name };
			await appCache.Set(cacheKey, dataSource, TimeSpan.FromMinutes(5));

			return (dataSource, null);
		}

		private async Task<IError?> CreateDatabase(string localPath, string connectionString, string name)
		{
			if (!fileSystem.File.Exists(localPath))
			{
				using (var fs = fileSystem.File.Create(localPath))
				{
					fs.Close();
				}
			}

			string sql = @"CREATE TABLE __Variables__ (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    variable TEXT NOT NULL UNIQUE,
    value TEXT,
    created DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated DATETIME DEFAULT CURRENT_TIMESTAMP,
    expires DATETIME)";

			var connection = new SqliteConnection(connectionString);
			await connection.OpenAsync();
			var transaction = await connection.BeginTransactionAsync();
			await connection.ExecuteAsync(sql, transaction: transaction);

			var error = await ExecuteSetup(transaction, name);
			if (error != null)
			{
				await transaction.RollbackAsync();
				await connection.CloseAsync();

				fileSystem.File.Delete(localPath);

				return error;
			}
			await transaction.CommitAsync();
			await connection.CloseAsync();
			await connection.DisposeAsync();
			return null;
		}

		private async Task<IError?> ExecuteSetup(DbTransaction transaction, string name)
		{
			var setupGoal = prParser.GetAllGoals().FirstOrDefault(p => p.DataSourceName != null && p.DataSourceName.Equals(name));
			if (setupGoal == null) return new Error($"Could not find setup file matching datasource {name}"); ;


			foreach (var step in setupGoal.GoalSteps)
			{
				if (step.Instruction == null)
				{
					step.Instruction = JsonHelper.ParseFilePath<Building.Model.Instruction>(fileSystem, step.AbsolutePrFilePath);
					if (step.Instruction == null) return new Error("Could not load instruction file");
				}

				var gf = step.Instruction.Function as DbGenericFunction;
				if (gf == null) return new Error($"Could not load generice function from instruction: {step.AbsolutePrFilePath}");

				var sql = gf.GetParameter<string>("sql");
				if (string.IsNullOrEmpty(sql)) continue;
				try
				{
					await transaction.Connection!.ExecuteAsync(sql, transaction: transaction);
				}
				catch (Microsoft.Data.Sqlite.SqliteException ex)
				{
					List<string> ignoreErrorMessages = ["already exists", "duplicate column name"];
					if (!ignoreErrorMessages.Any(p => ex.Message.Contains(p)))
					{
						return new StepError(ex.Message, step, Exception: ex);
					}

					int i = 0;
					// Error code 1 = "table already exists" in most cases
					// Ignore and continue
				}
				catch (Exception ex)
				{
					return new StepError(ex.Message, step, Exception: ex);
				}
			}

			var command = transaction.Connection.CreateCommand();
			command.CommandText = @"INSERT INTO __Variables__ (variable, value) VALUES ('SetupHash', @value)
									ON CONFLICT(variable) DO UPDATE SET value = excluded.value";
			command.Parameters.Add(new SqliteParameter("@value", setupGoal.Hash));
			command.ExecuteNonQuery();
			return null;

		}

		private async Task<(DataSource? DataSource, IError? Error)> ProcessDataSource(DataSource? dataSource)
		{
			if (dataSource == null)
			{
				(dataSource, var error) = await GetDataSourceNotFoundError(null, null);
				if (error != null) return (null, error);
			}

			if (dataSource.TypeFullName != typeof(SqliteConnection).FullName) return (dataSource, null);

			if (UseInMemoryDataSource || IsBuilder)
			{
				dataSource = dataSource with { ConnectionString = $"Data Source={dataSource.Name};Mode=Memory;Cache=Shared;" };
				return (dataSource, null);
			}

			if (!string.IsNullOrEmpty(dataSource.LocalPath)
					&& !dataSource.LocalPath.Contains("%")
					&& !fileSystem.File.Exists(dataSource.LocalPath))
			{
				using (var fs = fileSystem.File.Create(dataSource.LocalPath))
				{
					fs.Close();
				}

			}


			return (dataSource, null);
		}

		public DataSource GetTempMemoryDataSource(string name)
		{
			var dataSource = GetSqliteDataSource(name, $"Data Source={name};Mode=Memory;Cache=Shared;", "data");
			return dataSource;
		}

		public async Task<(DataSource?, IError?)> GetDataSourceNotFoundError(string? name = null, GoalStep? step = null)
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


			var setupGoal = prParser.GetAllGoals().FirstOrDefault(p => p.IsSetup && p.DataSourceName == name);
			if (setupGoal != null)
			{
				var createDataSourceStep = setupGoal.GoalSteps[0];
				var instruction = JsonHelper.ParseFilePath<Building.Model.Instruction>(fileSystem, createDataSourceStep.AbsolutePrFilePath);
				var gf = instruction.Function as DbGenericFunction;

				if (gf != null && gf.Name == "CreateDataSource")
				{
					var dsName = gf.GetParameter<string>("name");
					var dsDataType = gf.GetParameter<string>("databaseType");
					var setAsDefaultForApp = gf.GetParameter<bool?>("setAsDefaultForApp") ?? false;
					var keepHistoryEventSourcing = gf.GetParameter<bool?>("keepHistoryEventSourcing") ?? false;

					(var ds, var error) = await CreateDataSource(dsName, dsDataType, setAsDefaultForApp, keepHistoryEventSourcing);

					if (ds != null) return (ds, null);
				}
			}


			return (null, new BuilderError($"Datasource '{name}' does not exists", Key: "DataSourceNotFound",
						FixSuggestion: $@"{existingDatasources}

Create a step that creates a new Data source, e.g.
- create datasource, name: myCustomDb, path '/.db/my_custom_data.sqlite'

or you can catch this error and create it on this error

- {stepExample}, 
	on error key=DataSourceNotFound, call CreateDataSource and retry

where the goal CreateDataSource would create the database and table
", HelpfulLinks: "https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.DbModule.md"));
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


		public async Task<string> FormatSelectColumnsStatement(DataSource dataSource, string tableName)
		{
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
				appContext.AddOrReplace(ReservedKeywords.Inject_IDbConnection, dataSource.TypeFullName);


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
				appContext.AddOrReplace(ReservedKeywords.Inject_IDbConnection, dbConnection.GetType().FullName);
			}
			return dbConnection;


		}



	}
}
