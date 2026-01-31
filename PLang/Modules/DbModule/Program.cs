using Dapper;
using IdGen;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pqc.Crypto.Hqc;
using PLang.Attributes;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.DbService;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;
using ReverseMarkdown;
using ReverseMarkdown.Converters;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Dapper.SqlMapper;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Utils.VariableHelper;

namespace PLang.Modules.DbModule
{
	[Description("Database access, select, insert, update, delete and execute raw sql. Handles transactions. Sets and create datasources. Isolated data pattern (idp)")]
	public class Program : BaseProgram, IDisposable
	{
		//public static string DbConnectionContextKey = "DbConnection";
		//public static string DbTransactionContextKey = "DbTransaction";
		//public static string CurrentDataSourceKey = "PLang.Modules.DbModule.CurrentDataSourceKey";

		private record DbConnectionSupported(string Key, string Name, Type Type);

		private ModuleSettings dbSettings;
		private readonly PrParser prParser;
		private readonly IDbServiceFactory dbFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IEventSourceFactory eventSourceFactory;
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;

		[Description("ParameterName must be prefixed with @. VariableNameOrValue can be any primative type, string or a %variable%")]
		public record ParameterInfo(string TypeFullName, string ParameterName, object? VariableNameOrValue);
		public record TableInfo(string Name, List<ColumnInfo> Columns);
		public record ColumnInfo(string Name, string Type, bool IsPrimaryKey, bool IsNotNull, object DefaultValue);

		public Program(IDbServiceFactory dbFactory, PLangAppContext appContext, IPLangFileSystem fileSystem, ISettings settings, ILlmServiceFactory llmServiceFactory,
			IEventSourceFactory eventSourceFactory, ILogger logger, ITypeHelper typeHelper, ModuleSettings dbSettings, PrParser prParser) : base()
		{
			this.dbFactory = dbFactory;
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
			this.eventSourceFactory = eventSourceFactory;
			this.logger = logger;
			this.typeHelper = typeHelper;
			this.appContext = appContext;

			this.dbSettings = dbSettings;
			this.prParser = prParser;

			this.dbSettings.IsBuilder = this.IsBuilder;
		}
		/*
		public async Task<IError?> AsyncConstructor()
		{
			(DataSource? DataSource, IError? Error) result;
			if (instruction.Properties.TryGetValue("DataSource", out var name) && !string.IsNullOrWhiteSpace(name?.ToString()))
			{
				result = await dbSettings.GetDataSource(name.ToString(), goalStep);
			}
			else
			{
				result = await dbSettings.GetCurrentDataSource();
			}

			if (result.Error != null) return result.Error;

			context.AddOrReplace(CurrentDataSourceKey, result.DataSource);

			return null;
		}*/


		[Description("Create a datasource to a database")]
		public async Task<(DataSource?, IError?)> CreateDataSource([HandlesVariable] string name = "data", string databaseType = "sqlite",
			bool? setAsDefaultForApp = null, bool? keepHistoryEventSourcing = null)
		{

			if (!goal.IsSetup) return (null, new ProgramError("Create data source can only be in a setup file",
				FixSuggestion: $"Create setup.goal file or a goal file in a setup folder. Only one create data source can be in each setup file"));

			var (datasource, error) = await dbSettings.CreateDataSource(name, databaseType, setAsDefaultForApp ?? false, keepHistoryEventSourcing ?? false);
			if (datasource == null) return (datasource, error);

			if (datasource.Name.Contains("%"))
			{
				(datasource, error) = await dbSettings.GetDataSource(name);
				if (error != null) return (null, error);
			}

			error = AddDataSourceToContext(datasource);
			if (error != null) return (null, error);

			return (datasource, null);
		}

		[Description("gets all databases that have been created")]
		public async Task<List<DataSource>> GetDataSources()
		{
			return await dbSettings.GetAllDataSources();
		}


		[Description("gets the current datasource by name")]
		public async Task<(DataSource? DataSource, IError? Error)> GetDataSource([HandlesVariable] string? name = null)
		{
			var (dataSource, error) = await dbSettings.GetDataSource(name, goalStep);
			if (error != null) return (null, error);


			if (dataSource == null) return (null, new ProgramError($"Could not find datasource {name}", goalStep));
			return (dataSource, null);
		}

		[Description("set the current datasource by name(s). This could be one datasource or many")]
		public async Task<(DataSource? DataSource, IError? Error)> SetDataSourceNames([HandlesVariable] List<string> dataSourceNames)
		{
			if (dataSourceNames.Count == 0) return (null, new ProgramError("datasource name cannot be empty", goalStep));

			(var dataSource, var error) = await GetDataSourcesByNames(dataSourceNames);
			if (error != null) return (null, error);

			if (context.DataSource != null && context.DataSource.LocalPath != dataSource.LocalPath)
			{
				if (context.DataSource.IsInTransaction) return (null, new ProgramError("You cannot set a new datasource while in a transaction.", FixSuggestion: $"Include the data source '{dataSourceNames[0]}' in the original begin transaction, e.g. `- begin transaction 'data', '{dataSourceNames[0]}'`"));
			}

			error = AddDataSourceToContext(dataSource);
			if (error != null) return (null, error);

			return (dataSource, null);
		}


		public async Task<IError?> BeginTransaction([HandlesVariable] List<string>? dataSourceNames = null, GoalToCallInfo? onRollback = null)
		{

			var (dataSource, error) = await GetDataSourcesByNames(dataSourceNames);
			if (error != null) return error;

			return await BeginTransaction(dataSource, onRollback);
		}

		internal async Task<IError?> BeginTransaction(DataSource? dataSource = null, GoalToCallInfo? onRollback = null)
		{
			IError? error;

			if (dataSource == null)
			{
				(dataSource, error) = await dbSettings.GetDataSourceOrDefault();
				if (error != null) return error;

			}

			if (dataSource!.IsInTransaction) return new ProgramError("Datasource is already in a transaction", StatusCode: 409, FixSuggestion: "Either dont call begin transaction, or catch this error and ignore it, e.g. `- begin transaction, on error 'already in transaction' ignore error");

			var dbConnection = dbFactory.CreateHandler(dataSource, memoryStack);
			if (dbConnection.State != ConnectionState.Open)
			{
				dbConnection.Open();
			}

			var transaction = dbConnection.BeginTransaction();

			if (dbConnection is SqliteConnection)
			{

				using var command = transaction.Connection.CreateCommand();
				command.CommandText = "PRAGMA foreign_keys = ON;";
				command.ExecuteNonQuery();

			}

			dataSource.Transaction = transaction;
			dataSource.TransactionStartGoal = goal.RelativePrPath;

			if (dataSource.AttachedDbs.Count > 0)
			{
				using var attachCommand = transaction.Connection.CreateCommand();
				error = await AttachDb(dataSource, (DbCommand)attachCommand);
				if (error != null) return error;
			}
			
			error = AddDataSourceToContext(dataSource);
			if (error != null) return error;

			return null;
		}

		private IError? AddDataSourceToContext(DataSource main)
		{
			if (context.DataSource != null)
			{
				var ds = context.DataSource;

				if (ds.Transaction != null)
				{
					if (IsBuilder)
					{
						ds.Transaction.Commit();
						ds.Transaction.Connection?.Close();
						ds.Transaction = null;
						ds.AttachedDbs.Clear();
					}
					else
					{
						if (ds.Name == main.Name)
						{
							return null;
						}

						return new ProgramError($"Transaction exists on '{ds.NameInStep}', cannot overwrite context. Transaction was started by {ds.TransactionStartGoal}", goalStep);
					}
				}
			}
			context.DataSource = main;
			return null;
		}

		internal async Task<IError?> EndTransaction(bool force = false)
		{
			var datasource = context.DataSource;
			if (datasource == null || datasource.Transaction == null) return null;
			if (!force && datasource.TransactionStartGoal != goal.RelativePrPath) return null;

			var transaction = datasource.Transaction;
			if (transaction.Connection == null)
			{
				datasource.Transaction = null;
				return null;
			}
			try
			{

				var connection = transaction.Connection;
				if (HasError)
				{
					transaction.Rollback();
				}
				else
				{
					transaction.Commit();
				}

				if (datasource != null && connection != null && datasource.AttachedDbs.Count > 0)
				{
					//var cmd = transaction.Connection.CreateCommand();
					var error = await DetachDb(datasource, connection);
					connection.Close();
					if (error != null) return error;
				}

			}
			catch (Exception ex)
			{
				return new ProgramError(ex.Message, goalStep, Exception: ex);
			}
			finally
			{
				transaction.Connection?.Close();
				datasource.Transaction = null;
				datasource.AttachedDbs.Clear();
				if (context.DataSource != null && context.DataSource.Name == datasource.Name)
				{
					context.DataSource = datasource;
				}
				
			}

			return null;
		}
		public async Task<IError?> EndTransaction()
		{
			return await EndTransaction(false);
		}

		public async Task<IError?> Rollback()
		{
			var (dataSource, error) = await dbSettings.GetDataSourceOrDefault();
			if (error != null) return error;

			var transaction = dataSource.Transaction;
			if (transaction == null) return new ProgramError("No transaction found");

			transaction.Rollback();
			transaction.Connection?.Close();

			dataSource.Transaction = null;

			return null;

		}

		public async Task<IError?> ResetSetup(List<string> dataSourceNames)
		{
			var setupOnceDictionary = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
			foreach (var dataSourceName in dataSourceNames)
			{
				(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
				if (error != null) return error;

				var goals = prParser.GetAllGoals().Where(p => p.IsSetup && p.DataSourceName != null && p.DataSourceName.Equals(dataSourceName, StringComparison.OrdinalIgnoreCase));
				if (!goals.Any()) return new ProgramError($"No steps found for datasource {dataSourceName}");

				foreach (var goal in goals)
				{
					foreach (var step in goal.GoalSteps)
					{
						setupOnceDictionary.Remove(step.RelativePrPath);
					}
				}

				settings.Set<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", setupOnceDictionary);

			}
			return null;
		}
		public async Task<IError?> LoadExtension([HandlesVariable] string dataSourceName, string fileName, string? procName = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return error;

			return await LoadExtension(dataSource, fileName, procName);
		}
		internal async Task<IError?> LoadExtension(DataSource dataSource, string fileName, string? procName = null)
		{
			var dbConnection = dbFactory.CreateHandler(dataSource, memoryStack);
			if (dbConnection is not SqliteConnection)
			{
				return new Error("Loading extension only works for Sqlite", "NotSupported");
			}

			fileName = GetPath(fileName);
			if (!fileSystem.File.Exists(fileName))
			{
				return new Error("File could not be found.", "FileNotFound", StatusCode: 404);
			}

			((SqliteConnection)dbConnection).LoadExtension(fileName, procName);
			return null;

		}

		[Description("Return list of tables and views in a datasource")]
		public async Task<(List<string>? Scheme, IError? Error)> GetDbScheme([HandlesVariable] string dataSourceName)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (null, error);

			return await GetDbScheme(dataSource);
		}
		internal async Task<(List<string>? Scheme, IError? Error)> GetDbScheme(DataSource dataSource)
		{
			var result = await Select(dataSource, dataSource.SelectTablesAndViews);
			if (result.Error != null)
			{
				return (null, result.Error);
			}

			if (result.Table!.Count == 0)
			{
				return (null, new ProgramError($"No tables exists in {dataSource.Name}", Key: "NoTables"));
			}

			var list = new List<string>();
			foreach (var item in result.Table)
			{
				list.Add(item["name"].ToString());
			}
			return (list, null);
		}

		[Description("Returns tables and views in database with the columns description")]
		public async Task<(IEnumerable<TableInfo>? TablesAndColumns, IError? Error)> GetDatabaseStructure([HandlesVariable] string dataSourceName, List<string>? tables = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (null, error);

			return await GetDatabaseStructure(dataSource, tables);
		}

		internal async Task<(IEnumerable<TableInfo>? TablesAndColumns, IError? Error)> GetDatabaseStructure(DataSource dataSource, List<string>? tables = null)
		{

			var result = await Select(dataSource, dataSource.SelectTablesAndViews);
			if (result.Error != null)
			{
				return (null, result.Error);
			}

			if (result.Table.Count == 0)
			{
				return (null, new ProgramError($"No tables exists in {dataSource.Name}", Key: "NoTables", StatusCode: 404));
			}

			List<TableInfo> tableInfos = new();

			foreach (var row in result.Table)
			{
				if (tables != null && !tables.Any(p =>
				{
					string tableName = p;
					if (p.Contains("."))
					{
						tableName = p.Substring(p.IndexOf(".") + 1);
					}

					return tableName.Equals(row.Get<string>("name"), StringComparison.OrdinalIgnoreCase);
				}))
				{
					continue;
				}

				var sql = await dbSettings.FormatSelectColumnsStatement(dataSource, row.Get<string>("name"));

				var selectResult = await Select(dataSource, sql);
				if (selectResult.Error != null) return (null, selectResult.Error);

				List<ColumnInfo> columns = new();
				foreach (var column in selectResult.Table)
				{
					columns.Add(new ColumnInfo(column.Get<string>("name"),
							column.Get<string>("type"), column.Get<Int64>("isprimarykey") == 1,
							column.Get<Int64>("isnotnull") == 1, column.Get<object>("default_value")));
				}
				tableInfos.Add(new TableInfo(row.Get<string>("name"), columns));


			}
			return (tableInfos.OrderBy(p => p.Name), null);
		}

		public async void Dispose()
		{
			await EndTransaction();
		}



		private (IDbConnection? connection, IDbTransaction? transaction, DynamicParameters? param, string sql, IError? error) Prepare(DataSource dataSource, string sql, List<ParameterInfo>? Parameters = null, bool isInsert = false, bool readOnly = false)
		{
			IDbConnection? connection = null;
			var transaction = dataSource.Transaction;
			if (transaction != null)
			{
				connection = transaction.Connection;
			}
			else
			{
				connection = dbFactory.CreateHandler(dataSource, memoryStack, readOnly);
			}
			if (connection == null) return (null, null, null, sql, new ProgramError("Connection to db could not be created"));


			bool isSqlite = (dataSource.TypeFullName.Contains("sqlite", StringComparison.OrdinalIgnoreCase));

			var paramResult = GetDynamicParameters(sql, isInsert, Parameters, isSqlite, dataSource);
			if (paramResult.Error != null) return (null, null, null, sql, paramResult.Error);

			if (connection.State == ConnectionState.Open)
			{
				return (connection, transaction, paramResult.DynamicParameters, sql, null);
			}

			connection.Open();
			if (connection is SqliteConnection sqliteConnection)
			{

				using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = "PRAGMA foreign_keys = ON;";
				command.ExecuteNonQuery();

				if (sqliteConnection.ConnectionString.Contains("Memory;"))
				{
					var anchors = appContext.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb", new(StringComparer.OrdinalIgnoreCase)) ?? new(StringComparer.OrdinalIgnoreCase);
					if (!anchors.ContainsKey(dataSource.Name))
					{
						var anchorConnection = dbFactory.CreateHandler(dataSource, memoryStack);
						anchorConnection.Open();
						anchors.Add(dataSource.Name, anchorConnection);

						appContext.AddOrReplace("AnchorMemoryDb", anchors);
					}
				}
			}


			return (connection, transaction, paramResult.DynamicParameters, sql, paramResult.Error);

		}

		/*
		public async Task<(int, IError?)> InsertEventSourceData(long id, string data, string keyHash)
		{
			var dataSource = goalStep.GetVariable<DataSource>();

			var transaction = goal.GetVariable<IDbTransaction>(string.Format(transactionKey, dataSource.Name));
			IDbConnection? connection = goal.GetVariable<IDbConnection>(string.Format(connectionKey, dataSource.Name));

			if (connection == null) connection = dbFactory.CreateHandler(goalStep);

			return await eventSourceRepository.AddEventSourceData(connection, id, data, keyHash, transaction);
		}*/
		[Description("Executes a sql file")]
		public async Task<(long, IError?)> ExecuteSqlFile([HandlesVariable] string dataSourceName, string fileName, List<string> tableAllowList)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await ExecuteSqlFile(dataSource, fileName, tableAllowList);
		}

		internal async Task<(long, IError?)> ExecuteSqlFile(DataSource dataSource, string fileName, List<string> tableAllowList)
		{
			var file = GetProgramModule<Modules.FileModule.Program>();
			var readResult = await file.ReadTextFile(fileName);
			if (readResult.Error != null) return (0, readResult.Error);

			return await Execute(dataSource, readResult.Content.ToString(), tableAllowList);

		}

		[Description("Executes a sql statement that is fully dynamic or from a %variable%. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be updated")]
		public async Task<(long, IError?)> ExecuteDynamicSql([HandlesVariable] string dataSourceName, string sql, List<string> tableAllowList, List<ParameterInfo>? parameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await ExecuteDynamicSql(dataSource, sql, tableAllowList, parameters);
		}

		[Description("Execute sql by connection string")]
		public async Task<(long, IError?)> ExecuteSqlByConnectionString([HandlesVariable] string pathToSql, string sql, List<string> tableAllowList, List<ParameterInfo>? parameters = null)
		{
			pathToSql = memoryStack.LoadVariables(pathToSql).ToString();
			var absolutePath = GetPath(pathToSql);
			var dataSource = new DataSource(pathToSql, typeof(SqliteConnection).FullName, $"Data Source={pathToSql};", "data", "", "", false, false, absolutePath);
			return await ExecuteDynamicSql(dataSource, sql, tableAllowList, parameters);
		}

		[Description("Query the database with a sql file pointed to by path, e.g. query sql/file.sql. It can use multiple datasource and parameterer")]
		[Example(@"query usersCount.sql, parameters: date=%now%, ds:""data"", ""sales"", table: ""users"", write to %result%", @"fileName=""usersCount.sql"", parameter=[{""date"":""%now%""}], dataSourceNames=[""data"", ""sales""], tableAllowList=[""users""], ReturnValues=""%result%""")]
		[Example(@"query usersCount.sql, table: ""users"", write to %result%", @"fileName=""usersCount.sql"", tableAllowList=[""users""], ReturnValues=""%result%""")]
		[Example(@"query sql/totalProducts.sql, table: ""products"", write to %productCount%", @"fileName=""totalProducts.sql"", tableAllowList=[""products""], ReturnValues=""%productCount%""")]
		public async Task<(object?, IError?, Properties?)> QuerySqlFile([HandlesVariable] List<string> dataSourceNames, string fileName, List<string> tableAllowList, List<ParameterInfo>? parameters = null, int? rowsToReturn = null)
		{ 
			(var dataSource, var error) = await GetDataSourcesByNames(dataSourceNames);
			if (error != null) return (0, error, null);

			return await QuerySqlFile(dataSource, fileName, tableAllowList, parameters, rowsToReturn);
		}

		internal async Task<(object?, IError?, Properties?)> QuerySqlFile(DataSource dataSource, string fileName, List<string> tableAllowList, List<ParameterInfo>? parameters = null, int? rowsToReturn = null)
		{
			var file = GetProgramModule<Modules.FileModule.Program>();
			var readResult = await file.ReadTextFile(fileName);
			if (readResult.Error != null) return (0, readResult.Error, null);

			var result = await Select(dataSource, readResult.Content.ToString(), parameters);
			if (rowsToReturn != null && rowsToReturn >= 0 && result.Table != null)
			{
				var table = result.Table.Take(rowsToReturn.Value);
				if (rowsToReturn == 1) return (table.FirstOrDefault(), result.Error, result.Properties);

				return (table, result.Error, result.Properties);
			}
			return result;
		}

		[Description("Query sql statement that is fully from a %variable%. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be queried")]
		[Example(@"execute query %sql%, ds: %dataSource%, tables: users, products, write to %result%", @"sql=%sql%, dataSouceName=%dataSource%, tableAllowList=[""users"", ""products""]")]
		public async Task<(Table?, IError?, Properties?)> QueryDynamicSql([HandlesVariable] List<string> dataSourceNames, string sql, List<string> tableAllowList, List<ParameterInfo>? parameters = null)
		{


			(var dataSources, var error) = await GetDataSourcesByNames(dataSourceNames);
			if (error != null) return (null, error, null);

			return await Select(dataSources, sql, parameters);
		}

		internal async Task<(long, IError?)> ExecuteDynamicSql(DataSource dataSource, string sql, List<string> tableAllowList, List<ParameterInfo>? parameters = null)
		{
			return await Execute(dataSource, sql, tableAllowList, parameters);
		}
		[Description("Executes a sql statement that defined by user, such as `create index ..`, `drop ..`. Preferable not for select,update,insert statements. This statement will be validated. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be updated")]
		public async Task<(long RowsAffected, IError? Error)> Execute([HandlesVariable] string dataSourceName, string sql, List<string> tableAllowList, List<ParameterInfo>? parameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await Execute(dataSource, sql, tableAllowList, parameters);
		}

		[Description("Execute sql statement on specific connection string")]
		public async Task<(long RowsAffected, IError? Error)> ExecuteByConnectionString(string sql, string connectionString, string dbType = "sqlite", List<ParameterInfo>? parameters = null)
		{
			if (dbType != "sqlite") return (0, new ProgramError($"Database type {dbType} is not supported. {ErrorReporting.CreateIssueNotImplemented}"));

			var fullPath = GetPath(connectionString);
			var dataSource = new DataSource("temp", typeof(SqliteConnection).FullName, $"Data Source={connectionString}", "data", "", "");
			return await ExecuteRaw(dataSource, sql, parameters);

		}
		[Description("Reference as db.Execute(%sql%, %parameters%)")]
		public async Task<(long RowsAffected, IError? Error)> ExecutePlang(string sql, List<ObjectValue>? parameters = null)
		{
			List<ParameterInfo>? sqlParameters = null;
			if (parameters != null)
			{
				sqlParameters = new();
				foreach (var parameter in parameters)
				{
					sqlParameters.Add(new ParameterInfo(parameter.Type.FullName, parameter.Name, parameter.Value));
				}
			}

			return await ExecuteRaw(context.DataSource, sql, sqlParameters);
		}

		internal async Task<(long RowsAffected, IError? Error)> Execute(DataSource dataSource, string sql, List<string> tableAllowList, List<ParameterInfo>? parameters = null)
		{
			/*
			if (tableAllowList.Count == 0)
			{
				return (0, new ProgramError("You must define a allow list of tables"));
			}*/

			//todo: this is not a secure allow check
			//should deconstruct the sql to find out real table in sql statement
			foreach (var table in tableAllowList ?? [])
			{
				if (!sql.Contains(table, StringComparison.OrdinalIgnoreCase))
				{
					return (0, new ProgramError($"Table {table} was not in sql: {sql}"));
				}
			}
			return await ExecuteRaw(dataSource, sql, parameters);
		}

		internal async Task<(long RowsAffected, IError? Error)> ExecuteRaw(DataSource dataSource, string sql, List<ParameterInfo>? parameters = null)
		{
			long rowsAffected = 0;

			if (sql.Contains("@id"))
			{
				if (parameters == null) parameters = new List<ParameterInfo>();
				if (parameters.FirstOrDefault(p => p.ParameterName == "@id") == null)
				{
					parameters.Add(new ParameterInfo("System.Int64", "@id", "auto"));
				}
			}

			var prepare = Prepare(dataSource, sql, parameters);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				var eventSourceRepository = eventSourceFactory.CreateHandler(dataSource);
				rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				
				Done(prepare.connection, prepare.transaction);


				return (rowsAffected, null);
			}
			catch (Exception ex)
			{
				if (GoalHelper.IsSetup(goalStep))
				{
					if (ex.ToString().Contains("already exists") || ex.ToString().Contains("duplicate column name"))
					{
						ShowWarning(ex);
						return (1, null);
					}


				}


				return (0, new SqlError(ex.Message, sql, null, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection, prepare.transaction);
			}
		}

		private void ShowWarning(Exception ex)
		{
			logger.LogWarning($"Had error running Setup ({goalStep.Text}) but will continue. Error message:{ex.Message}");
		}

		[Description("When user does not define a primary key, add it to the create statement as id column not null, when KeepHistory is set to false, make the column auto increment. When renaming a table and dropping, include  PRAGMA foreign_keys=OFF; at start and PRAGMA foreign_keys=ON; PRAGMA foreign_key_check; at the end")]
		public async Task<(long, IError?)> CreateTable(string sql)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(goal.DataSourceName, goalStep);
			if (error != null) return (0, error);
			return await ExecuteRaw(dataSource, sql);

		}



		[Description("When SELECT/WITH should return 1 row (limit 1) on multiple data source")]
		public async Task<(object?, IError? errors, Properties? Properties)> SelectOneRowWithMultipleDataSources([HandlesVariable] List<string> dataSourceNames, string sql, List<ParameterInfo>? sqlParameters = null)
		{

			var (dataSource, error) = await GetDataSourcesByNames(dataSourceNames);
			if (error != null) return (null, error, null);

			return await SelectOneRow(dataSource, sql, sqlParameters);
		}

		[Description("When SELECT/WITH should return 1 row (limit 1)")]
		public async Task<(object?, IError? errors, Properties? Properties)> SelectOneRow([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (null, error, null);

			return await SelectOneRow(dataSource, sql, sqlParameters);
		}
		internal async Task<(object?, IError? errors, Properties? Properties)> SelectOneRow(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			var result = await Select(dataSource, sql, sqlParameters);
			if (result.Error != null)
			{
				return (null, result.Error, result.Properties);
			}
			if (result.Table == null || result.Table.Count == 0) return (null, null, result.Properties);

			if (function.ReturnValues != null && function.ReturnValues.Count == 1 && result.Table.ColumnNames.Count != 1)
			{
				return (result.Table[0], null, result.Properties);
			}


			var returnValues = new List<ObjectValue>();
			if (result.Table.ColumnNames.Count == 1)
			{
				var columnName = result.Table.ColumnNames[0];
				return (new ObjectValue(columnName, result.Table[columnName]), null, result.Properties);
			}

			foreach (var columnName in result.Table.ColumnNames)
			{
				returnValues.Add(new ObjectValue(columnName, result.Table[columnName]));
			}
			return (returnValues, null, result.Properties);




		}

		[Description("Doing SELECT/WITH on multiple datasources")]
		[Example("select * from main.users u, join users.orders o on o.userId=u.id where u.id=%id%, ds: data, users/%user.id%, write to %results%", @"sql=""select * from main.users u join users.orders o on o.userId=u.id where u.id=%id%"", sqlParameters=[""id"", ""%id""], dataSourceName=[""data"", ""users/%user.id%""], ReturnValues=[""%results%""]")]
		public async Task<(Table? Table, IError? Error, Properties? Properties)> SelectWithMultipleDataSources([HandlesVariable] List<string> dataSourceNames, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			var (dataSource, error) = await GetDataSourcesByNames(dataSourceNames);
			if (error != null) return (null, error, null);

			return await Select(dataSource, sql, sqlParameters);
		}

		[Description("Any step starting with SELECT/WITH using one datasource.")]
		[Example("select * from orders where id=%id%, ds: users/%user.id%, write to %orders%", @"sql=""select * from orders where id=@id"", sqlParameters=[""id"", ""%id""], dataSourceName=""users/%user.id%"", ReturnValues=[""%orders%""]")]
		public async Task<(Table? Table, IError? Error, Properties? Properties)> Select([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (null, error, null);

			return await Select(dataSource, sql, sqlParameters);
		}

		internal async Task<(Table? Table, IError? Error, Properties? Properties)> Select(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			var prep = Prepare(dataSource, sql, sqlParameters, readOnly : true);
			if (sql.StartsWith("select o.id as orderId, o.type as orderType"))
			{
				Console.WriteLine($"---- FACT SALE: {dataSource.Name} | {dataSource.ConnectionString} - {JsonConvert.SerializeObject(sqlParameters)}");
			}
			Properties properties = new();
			properties.Add(new ObjectValue("DataSource", dataSource));
			properties.Add(new ObjectValue("MethodParameters", new { Sql = sql, SqlParameters = sqlParameters }));

			if (prep.error != null)
			{
				return (null, prep.error, properties);
			}
			logger.LogDebug($"Sql: {prep.sql} - Parameters:{prep.param}");

			var con = (DbConnection)prep.connection;
			await using var cmd = con.CreateCommand();

			try
			{
				if (prep.transaction != null)
				{
					cmd.Transaction = (DbTransaction)prep.transaction;
				}
				else if (dataSource.AttachedDbs.Count > 0)
				{
					var error = await AttachDb(dataSource, cmd);
					if (error != null) return (null, error, properties);
				}


				bool isSqlite = (dataSource.TypeFullName.Contains("sqlite", StringComparison.OrdinalIgnoreCase));


				// Add parameters if any:
				if (sqlParameters is not null)
				{
					foreach (var prop in sqlParameters)
					{

						if (prop.TypeFullName == typeof(System.Array).FullName)
						{
							if (prop.VariableNameOrValue == null || string.IsNullOrEmpty(prop.VariableNameOrValue.ToString()))
							{
								return (null, new ProgramError($"{prop.ParameterName} is emtpy. It cannot be empty", goalStep), null);
							}

							if (prop.VariableNameOrValue is not IList list)
							{
								list = new List<object>();
								list.Add(prop.VariableNameOrValue);
							}

							var ids = Enumerable.Range(0, list.Count).ToArray();

							var paramNames = ids.Select((_, i) => $"@__plang_id{i}").ToArray();
							prep.sql = prep.sql.Replace(prop.ParameterName, string.Join(", ", paramNames));
							foreach (var id in ids)
							{
								var param = cmd.CreateParameter();
								param.ParameterName = $"@__plang_id{id}";
								param.Value = list[id].ToString();

								cmd.Parameters.Add(param);
							}
						}
						else
						{
							var param = cmd.CreateParameter();
							param.ParameterName = prop.ParameterName;
							var (value, error) = ConvertObjectToType(prop.VariableNameOrValue, prop.TypeFullName, prop.ParameterName, prop.VariableNameOrValue, isSqlite);
							if (error != null) return (null, error, properties);

							param.Value = value;
							cmd.Parameters.Add(param);
						}


					}

				}
				cmd.CommandText = prep.sql;
				properties.Add(new ObjectValue("CommandText", cmd.CommandText));
				if (context.DebugMode)
				{

				}

				Stopwatch stopwatch = Stopwatch.StartNew();

				using var reader = await cmd.ExecuteReaderAsync();

				properties.Add(new ObjectValue("QueryTime", stopwatch.Elapsed));
				stopwatch.Restart();

				var cols = Enumerable.Range(0, reader.FieldCount)
					.Select(reader.GetName)
					.ToList();

				var table = new Table(cols);

				while (await reader.ReadAsync())
				{
					var row = new Row(table);
					foreach (var col in cols)
					{
						var type = MapType(reader.GetDataTypeName(col));
						if (type == null)
						{
							row[col] = reader[col];
						}
						else
						{
							row[col] = TypeHelper.ConvertToType(reader[col], type);
						}
					}

					table.Add(row);
				}
				await reader.CloseAsync();
				properties.Add(new ObjectValue("MappingTime", stopwatch.Elapsed));
				properties.Add(new ObjectValue("Columns", cols));
				properties.Add(new ObjectValue("RowCount", table.Count));
				
				logger.LogDebug($"Rows: {table.Count}");
				
				if (prep.transaction == null && dataSource.AttachedDbs.Count > 0)
				{
					var error = await DetachDb(dataSource, prep.connection);
					if (error != null) return (table, error, properties);
				}

				return (table == null) ? (new(cols), null, properties) : (table, null, properties);
			}
			catch (Exception ex)
			{
				var sqlError = new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex);
				if (prep.transaction == null && dataSource.AttachedDbs.Count > 0)
				{
					var error = await DetachDb(dataSource, prep.connection);
					if (error != null)
					{
						sqlError.ErrorChain.Add(error);
					}
				}

				return (null, sqlError, properties);


			}
			finally
			{
				
				Done(prep.connection, prep.transaction);
			}
		}

		private async Task<IError?> AttachDb(DataSource dataSource, DbCommand cmd, int retryCount = 0)
		{
			if (dataSource.AttachedDbs.Count == 0) return null;
			if (dataSource.AttachedDbs.FirstOrDefault(p => !p.IsAttached) == null)
			{
				if (dataSource.AttachedDbs.Count > 0)
				{
					Console.WriteLine($"will not Attach  - {goalStep.RelativePrPath}:{goalStep.LineNumber}");
				}
				return null;
			}
			


			for (int i = 0; i < dataSource.AttachedDbs.Count; i++)
			{
				var alias = dataSource.AttachedDbs[i].Alias;

				if (string.Equals(alias, "main", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(alias, "temp", StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException($"Invalid alias: {alias}");

				var dbAbsolutePath = fileSystem.Path.Join(goalStep.Goal.AbsoluteAppStartupFolderPath, dataSource.AttachedDbs[i].Path);
				
				
				cmd.CommandText += $"ATTACH DATABASE '{dbAbsolutePath}' AS {alias};\n";
				
			}
			try
			{
				//Console.WriteLine($"Try to attach {dataSource.Name}, {string.Join(',', dataSource.AttachedDbs.Select(p => p.Alias))} - {goalStep.RelativePrPath}:{goalStep.LineNumber} - Hash:{cmd.Connection.GetHashCode()} - Handle:{((SqliteConnection)cmd.Connection).Handle.DangerousGetHandle()}");
				await cmd.ExecuteNonQueryAsync();
				
			} catch (Exception ex)
			{
				if (ex.Message.Contains("already in use"))
				{
					if (retryCount < 3)
					{
						logger.LogTrace($"  - {ex.Message}, waiting");
						await Task.Delay(50);
						var error = await AttachDb(dataSource, cmd, ++retryCount);
						if (error != null) return error;
					} else
					{
						return new ProgramError(ex.Message, Exception: ex);
					}
					
					//what to do ??
				}
				else
				{
					return new ProgramError(ex.Message, Exception: ex);
				}
			}

			for (int i = 0; i < dataSource.AttachedDbs.Count; i++)
			{
				dataSource.AttachedDbs[i].IsAttached = true;
			}
			cmd.CommandText = "";

			logger.LogTrace($"  - Attached {dataSource.Name} - Attached:{string.Join(",", dataSource.AttachedDbs.Select(p => p.Alias))}");
			return null;
		}

		private async Task<IError?> DetachDb(DataSource dataSource, IDbConnection connection)
		{
			if (dataSource.AttachedDbs.Count == 0) return null;

			try
			{
				using var cmd = connection.CreateCommand();
				for (int i = 0; i < dataSource.AttachedDbs.Count; i++)
				{
					cmd.CommandText = $"DETACH DATABASE \"{dataSource.AttachedDbs[i].Alias}\";";
					cmd.ExecuteNonQuery();
				}
				
				//Console.WriteLine($"Detach {dataSource.Name}, {string.Join(',', dataSource.AttachedDbs.Select(p => p.Alias))} - {goalStep.RelativePrPath}:{goalStep.LineNumber} - Hash:{cmd.Connection.GetHashCode()} - Handle:{((SqliteConnection)cmd.Connection).Handle.DangerousGetHandle()}");
				for (int i = 0; i < dataSource.AttachedDbs.Count; i++)
				{
					dataSource.AttachedDbs[i].IsAttached = false;
				}
				logger.LogTrace($"  - Detach {dataSource.Name} - Detached:{string.Join(",", dataSource.AttachedDbs.Select(p => p.Alias))}");
				dataSource.AttachedDbs.Clear();


			}
			catch (Exception ex)
			{
				return new ProgramError(ex.Message, Exception: ex);
			}
			return null;

		}

		private object? GetDefaultValue(string strType)
		{
			if (strType == "dynamic") return new List<object>();
			if (strType == "string") return null;

			var type = Type.GetType(strType);
			if (type == null) return null;

			return type.IsValueType && !type.IsPrimitive ? Activator.CreateInstance(type) : null;
		}

		[Description("Allows user to send in json of columns to update. The json can be a %variable%. allowColums is the columns that are allowed to be updated. example: ` update table users with %json% where %id%, allowed columns: name, phone`")]
		public async Task<(long, IError?)> UpdateWithJsonColumns([HandlesVariable] string dataSourceName, string table, string jsonOfColumns, List<string> allowColumns, string? whereStatment = null, List<ParameterInfo>? whereParameter = null)
		{

			if (allowColumns == null || allowColumns.Count == 0) return (0, new ProgramError($"You must provide a list of columns that are allowed to be updated", FixSuggestion: $@"Add the allow column property, e.g. 
`- {goalStep.Text} 
	allow columns: name, age, zip
`
Clearly define which columns are allowed to be updated.
"));

			var columns = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonOfColumns);
			if (columns == null) return (0, new ProgramError($"Could not parse json of columns:{jsonOfColumns}"));

			StringBuilder sql = new($"UPDATE {table} SET ");

			if (whereParameter == null) whereParameter = new();
			foreach (var column in columns)
			{
				if (allowColumns.FirstOrDefault(p => p.Equals(column.Key, StringComparison.OrdinalIgnoreCase)) == null)
				{
					return (0, new ProgramError($"Column {column.Key} is not in the allowed columns list"));
				}
				sql.Append($"{column.Key}=@{column.Key} ");
				whereParameter.Add(new ParameterInfo("System.Object", column.Key, column.Value));
			}

			if (whereStatment != null)
			{

				if (!whereStatment.Contains("where", StringComparison.OrdinalIgnoreCase))
				{
					sql.Append(" WHERE");
				}
				sql.Append($" {whereStatment}");
			}
			var result = await Update(dataSourceName, sql.ToString(), whereParameter);
			return result;
		}


		public async Task<(long, IError?)> Update([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await Update(dataSource, sql, sqlParameters, validateAffectedRows);
		}
		internal async Task<(long, IError?)> Update(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{

			var prepare = Prepare(dataSource, sql, sqlParameters);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				long result;

				var eventSourceRepository = eventSourceFactory.CreateHandler(dataSource);
				result = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				
				if (validateAffectedRows && result == 0)
				{
					return (result, new ProgramError("0 Rows affected. This was not expected.", goalStep, FixSuggestion: $@"If this is expected, include that you dont want to validate affected rows in your step.
For example:
`- {goalStep.Text}, dont validate affected rows`
"));
				}
				return (result, null);
			}
			catch (Exception ex)
			{
				return (0, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection, prepare.transaction);
			}

		}
		public async Task<(long, IError?)> Delete([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await Delete(dataSource, sql, sqlParameters, validateAffectedRows);
		}
		internal async Task<(long, IError?)> Delete(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			long rowsAffected;
			var prepare = Prepare(dataSource, sql, sqlParameters);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				var eventSourceRepository = eventSourceFactory.CreateHandler(dataSource);
				rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				

				if (validateAffectedRows && rowsAffected == 0)
				{
					return (rowsAffected, new ProgramError("0 Rows affected. This was not expected.", goalStep, FixSuggestion: $@"If this is expected, include that you dont want to validate affected rows in your step.
For example:
`- {goalStep.Text}, dont validate affected rows`
"));
				}
				return (rowsAffected, null);
			}
			catch (Exception ex)
			{
				return (0, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection, prepare.transaction);
			}
		}

		[Description("Insert or update table(Upsert). Will return affected row count. Choose when user doesn't write result into %variable%")]
		public async Task<(long rowsAffected, IError? error)> InsertOrUpdate([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await Insert(dataSource, sql, sqlParameters, validateAffectedRows);
		}



		[Description("Insert into table. Will return affected row count. Choose when user doesn't write result into %variable%")]
		public async Task<(long rowsAffected, IError? error)> Insert([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);
			
			return await Insert(dataSource, sql, sqlParameters, validateAffectedRows);
		}

		internal async Task<(long rowsAffected, IError? error)> Insert(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			long rowsAffected = 0;
			var prepare = Prepare(dataSource, sql, sqlParameters, true);
			if (prepare.error != null)
			{
				return (0, prepare.error);
			}
			try
			{
				var eventSourceRepository = eventSourceFactory.CreateHandler(dataSource);
				rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);

				if (validateAffectedRows && rowsAffected == 0)
				{
					return (rowsAffected, new ProgramError("0 Rows affected. This was not expected.", goalStep, FixSuggestion: $@"If this is expected, include that you dont want to validate affected rows in your step.
For example:
`- {goalStep.Text}, dont validate affected rows`
"));
				}
			}
			catch (Exception ex)
			{
				if (GoalHelper.IsSetup(goalStep) && ex.ToString().Contains("duplicate key"))
				{
					ShowWarning(ex);
					return (rowsAffected, null);
				}
				Console.WriteLine(ex.Message);


				return (0, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection, prepare.transaction);
			}
			return (rowsAffected, null);

		}

		[Description("Insert or update table(Upsert). Will return the id/primary key of the affected row. Used when user intends to write into a %id%")]
		public async Task<(object? rowsAffected, IError? error)> InsertOrUpdateAndSelectIdOfRow([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await InsertAndSelectIdOfInsertedRow(dataSource, sql, sqlParameters);
		}

		[Description("Insert statement that will return the id of the inserted row.  Used when user intends to write into a %id%")]
		public async Task<(object?, IError?)> InsertAndSelectIdOfInsertedRow([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null, bool validateAffectedRows = true)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (null, error);

			return await InsertAndSelectIdOfInsertedRow(dataSource, sql, sqlParameters);
		}


		internal async Task<(object?, IError?)> InsertAndSelectIdOfInsertedRow(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			var prepare = Prepare(dataSource, sql, sqlParameters, true);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				var eventSourceRepository = eventSourceFactory.CreateHandler(dataSource);
				
				if (!dataSource.KeepHistory)
				{
					var value = await prepare.connection.QuerySingleOrDefaultAsync(prepare.sql, prepare.param, prepare.transaction) as IDictionary<string, object>;
					Done(prepare.connection, prepare.transaction);

					return (value.FirstOrDefault().Value, null);
				}
				else
				{
					var id = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction, returnId: true);
					Done(prepare.connection, prepare.transaction);

					if (id != 0)
					{
						return (id, null);
					}

					if (prepare.param.ParameterNames.Contains("id"))
					{
						return (prepare.param.Get<object>("id"), null);
					}


					return (null, null);
				}
			}
			catch (Exception ex)
			{
				return (0, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection, prepare.transaction);
			}

		}

		private List<string> GetProperties(object obj)
		{
			if (obj is Row row)
			{
				return row.Columns.Append("!data").ToList();
			}

			if (obj is JObject jObject)
			{
				return jObject.Properties().Select(p => p.Name).Append("!data").ToList();
			}
			else if (obj is ExpandoObject eo)
			{
				return ((IDictionary<string, object>)eo).Keys.Append("!data").ToList();
			}
			else
			{
				var type = obj.GetType();
				var properties = type.GetProperties().Select(p => p.Name).ToList();
				if (properties.Count > 0) return properties;

				return GetProperties(JObject.FromObject(obj));
			}
		}

		private object? GetValue(object obj, string propertyName)
		{
			propertyName = propertyName.Replace("%", "");
			if (obj is JObject jObject)
			{
				return jObject[propertyName];
			}
			else if (obj is ExpandoObject eo)
			{
				return ((IDictionary<string, object>)eo)[propertyName];
			}
			else
			{
				var value = obj.GetValueOnProperty(propertyName);
				if (value != null) return value;

				return GetValue(JObject.FromObject(obj), propertyName);
			}
			return null;
		}
		[Description("ONLY When inserting list of items(%variables% is plural). Insert a list(bulk) into database, return number of rows inserted. columnMapping maps which variable should match with a column. User will define that he is using bulk insert.")]
		public async Task<(long, IError?)> InsertBulk([HandlesVariable] string dataSourceName, string tableName, List<object> itemsToInsert, [HandlesVariable] Dictionary<string, object>? columnMapping = null, bool ignoreContraintOnInsert = false)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return (0, error);

			return await InsertBulk(dataSource, tableName, itemsToInsert, columnMapping, ignoreContraintOnInsert);
		}

		internal async Task<(long, IError?)> InsertBulk(DataSource dataSource, string tableName, List<object> itemsToInsert, [HandlesVariable] Dictionary<string, object>? columnMapping = null, bool ignoreContraintOnInsert = false)
		{
			if (itemsToInsert.Count == 0) return (0, null);


			var propertiesInItems = GetProperties(itemsToInsert[0]);
			if (columnMapping == null)
			{
				var sqlSelectColumns = await dbSettings.FormatSelectColumnsStatement(dataSource, tableName);
				var result = await Select(dataSource, sqlSelectColumns);
				if (result.Error != null)
				{
					return (0, result.Error);
				}
				var columnsInTable = result.Table;
				if (columnsInTable == null)
				{
					return (0, new Error($"Table {tableName} could not be found", "TableNotFound"));
				}

				columnMapping = new();
				foreach (var column in columnsInTable)
				{
					if (propertiesInItems.FirstOrDefault(p => p.Equals(column.Get<string>("name"), StringComparison.OrdinalIgnoreCase)) != null)
					{
						columnMapping.Add(column.Get<string>("name"), column.Get<string>("name"));
					}
				}
			}

			string? sql = GetBulkSql(tableName, columnMapping, itemsToInsert, ignoreContraintOnInsert, dataSource);
			if (sql == null) return (0, null);

			IError? error;

			long affectedRows = 0;
			var generator = new IdGenerator(1);
			var id = generator.ElementAt(0);
			IDbTransaction? transaction = dataSource.Transaction;
			if (transaction == null)
			{
				error = await BeginTransaction(dataSource);
				if (error != null) return (0, error);
			}

			// TODO: This is actually not the most optimized bulk insert, it's inserting each row at a time
			for (int i = 0; i < itemsToInsert.Count; i++)
			{
				var param = new List<ParameterInfo>();
				bool rowHasAnyValue = false;
				foreach (var column in columnMapping)
				{
					string cleanedColumnValue = column.Value.ToString().Replace("%", "").Replace("item.", "");
					bool isListItem = column.Value.ToString().Contains("item.");
					if (column.Key == "id")
					{
						param.Add(new ParameterInfo(typeof(Int64).FullName, "id", id + i));
						rowHasAnyValue = true; 
						continue;
					}
					
					var obj = (isListItem) ? GetValue(itemsToInsert[i], cleanedColumnValue) : memoryStack.Get(cleanedColumnValue);
					if (obj == null)
					{
						param.Add(new ParameterInfo(typeof(DBNull).FullName, column.Key, obj));
						continue;
					}
					else if (obj is ObjectValue ov)
					{
						param.Add(new ParameterInfo(ov.Type.FullName, column.Key, ov.Value));
					}
					else if (obj is JValue value)
					{
						param.Add(new ParameterInfo(value.Value.GetType().FullName, column.Key, value.Value));
					}
					else
					{
						param.Add(new ParameterInfo(obj.GetType().FullName, column.Key, obj));
					}
					rowHasAnyValue = true;
					

				}
				if (!rowHasAnyValue) { continue; }

				var insertResult = await Insert(dataSource, sql, param);
				if (insertResult.error != null && !ignoreContraintOnInsert)
				{
					await Rollback();
					return (0, insertResult.error);
				}
				affectedRows += insertResult.Item1;
			}
			if (transaction == null)
			{
				await EndTransaction();
			}

			return (affectedRows, null);

		}


		static readonly Dictionary<string, Type> SqliteToClr = new(StringComparer.OrdinalIgnoreCase)
		{
			["NULL"] = typeof(DBNull),
			["INTEGER"] = typeof(long),
			["INT"] = typeof(long),
			["REAL"] = typeof(double),
			["NUMERIC"] = typeof(decimal),
			["DECIMAL"] = typeof(decimal),
			["TEXT"] = typeof(string),
			["STRING"] = typeof(string),
			["CHAR"] = typeof(string),
			["CLOB"] = typeof(string),
			["BLOB"] = typeof(byte[]),
			["BOOLEAN"] = typeof(bool),
			["DATE"] = typeof(DateTime),
			["DATETIME"] = typeof(DateTime),
			["TIMESTAMP"] = typeof(DateTime),
			["GUID"] = typeof(Guid)
		};

		//this is only sqlite support, each database should have it's own implementation
		static Type? MapType(string declared) =>
				SqliteToClr.TryGetValue(declared, out var t) ? t
				: declared.IndexOf("INT", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(long)
				: declared.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(long)
				: declared.IndexOf("CHAR", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("CLOB", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(string)
				: declared.IndexOf("BLOB", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(byte[])
				: declared.IndexOf("REAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("FLOA", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("DOUB", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(double) :
				  declared.IndexOf("BIT", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(bool) :
				  declared.IndexOf("", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(string)
				: throw new Exception($"Could not map {declared} to sqlite type");

		private string? ConvertFromColumnTypeToCSharpTypeFullName(string type)
		{
			return MapType(type)?.FullName;
		}


		private string? GetBulkSql(string tableName, Dictionary<string, object> mapping, List<object> items, bool ignoreContraintOnInsert, ModuleSettings.DataSource dataSource)
		{
			if (items.Count == 0) return null;


			var objProperties = GetProperties(items[0]);
			string? columns = null;
			string? values = null;
			if (dataSource.KeepHistory)
			{
				columns = "id";
				values = "@id";
				if (!mapping.ContainsKey("id")) mapping.Add("id", "id");
			}
			foreach (var column in mapping)
			{
				var valueKey = column.Value.ToString().Replace("%", "");
				if (!valueKey.Contains("item."))
				{
					if (objProperties.Contains(valueKey) || memoryStack.GetObjectValue(valueKey).Initiated)
					{
						if (columns != null) columns += ", ";
						columns += column.Key;

						if (values != null) values += ", ";
						values += $"@{column.Key.ToString().Replace("%", "")}";
						
					}
				}
				else
				{
					valueKey = valueKey.Replace("item.", "");
					if (objProperties.FirstOrDefault(p => p.Equals(valueKey, StringComparison.OrdinalIgnoreCase)) != null)
					{
						if (columns != null) columns += ", ";
						columns += column.Key;

						if (values != null) values += ", ";
						
						values += $"@{column.Key.ToString().Replace("%", "")}";
						
					}
				}
			}
			if (ignoreContraintOnInsert)
			{
				if (dataSource.TypeFullName.ToLower().Contains("sqlite"))
				{
					return $"INSERT OR IGNORE INTO {tableName} ({columns}) VALUES ({values})";
				}
				else
				{
					throw new Exception("Only support sqlite. You can help improve the code, it's open source");
				}
			}
			return $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
		}

		public async override Task<string> GetAdditionalSystemErrorInfo()
		{
			return "You will be provided with tables that already exists in the database";
		}

		public async override Task<(string, IError?)> GetAdditionalAssistantErrorInfo()
		{
			var (dataSource, error) = await dbSettings.GetDataSourceOrDefault();
			if (error != null) return (string.Empty, error);

			List<ParameterInfo> parameters = new();
			parameters.Add(new ParameterInfo("System.String", "Database", dataSource.DbName));

			(var connection, var transaction, var par, _, error) = Prepare(dataSource, "", parameters);
			if (error != null) return (string.Empty, error);

			var result = await connection.QueryAsync(dataSource.SelectTablesAndViews, par, transaction);


			return (@$"## tables in database ##
{JsonConvert.SerializeObject(result)}
## tables in database ##
", null);
		}


		private (DynamicParameters DynamicParameters, IError? Error) GetDynamicParameters(string sql, bool isInsert, List<ParameterInfo>? Parameters, bool isSqlite, DataSource dataSource)
		{
			DynamicParameters param = new();
			if (Parameters == null) return (param, null);

			var multipleErrors = new GroupedErrors("sqlParameters");

			foreach (var p in Parameters)
			{
				var parameterName = p.ParameterName.Replace("@", "");
				if (parameterName.Contains("."))
				{
					var oldParameterName = parameterName;
					parameterName = parameterName.Replace(".", "");
					sql = sql.Replace(oldParameterName, parameterName);
				}

				if (p.VariableNameOrValue?.ToString() == "auto" && eventSourceFactory.GetType() == typeof(DisableEventSourceRepository))
				{
					throw new Exception($"Auto incremental cannot be handled by plang when Event sourcing is disabled | DataSource:{JsonConvert.SerializeObject(dataSource)}");
					return (param, new Error("Auto incremental cannot be handled by plang when Event sourcing is disabled"));
				}

				if (isInsert && parameterName == "id" && (p.VariableNameOrValue?.ToString() == "auto" || eventSourceFactory.GetType() != typeof(DisableEventSourceRepository)))
				{
					var id = p.VariableNameOrValue.ToString();
					if (id == "auto" || string.IsNullOrEmpty(id))
					{
						var generator = new IdGenerator(new Random().Next(0, 1023));
						var newId = generator.ElementAt(0);
						param.Add("@" + parameterName, newId, DbType.Int64);
					}
					else
					{
						param.Add("@" + parameterName, id, DbType.Int64);
					}
				}
				else if (p.VariableNameOrValue == null)
				{
					param.Add("@" + parameterName, null);
				}
				else if (VariableHelper.ContainsVariable(p.VariableNameOrValue))
				{
					var variableName = p.VariableNameOrValue.ToString();
					string prefix = "";
					string postfix = "";
					if (variableName.ToString().StartsWith("\\%"))
					{
						variableName = variableName.Substring(2);
						prefix = "%";
					}
					if (variableName.ToString().EndsWith("\\%"))
					{
						variableName = variableName.TrimEnd('%').TrimEnd('\\');
						postfix = "%";
					}
					var variableValue = variableName; // memoryStack.LoadVariables(variableName);
					(object? value, Error? error) = ConvertObjectToType(variableValue, p.TypeFullName, parameterName, p.VariableNameOrValue, isSqlite);
					if (error != null) multipleErrors.Add(error);

					param.Add("@" + parameterName, prefix + value + postfix);
				}
				else
				{
					var variableName = WrapForLike(p.VariableNameOrValue);

					(object? value, IError? error) = ConvertObjectToType(variableName, p.TypeFullName, parameterName, p.VariableNameOrValue, isSqlite);
					if (error != null)
					{
						if (parameterName == "id" && eventSourceFactory.GetType() == typeof(DisableEventSourceRepository))
						{
							multipleErrors.Add(new ProgramError($"Parameter @id is empty. Are you on the right data source? Current data source is {dataSource.Name}", goalStep, function));
						}
						multipleErrors.Add(error);
					}
					param.Add("@" + parameterName, value);
				}
			}

			IError? errorToReturn = (multipleErrors.Count > 0) ? multipleErrors : null;
			return (param, errorToReturn);

		}

		private object WrapForLike(object variableName)
		{
			if (variableName is not string str) return variableName;

			if (str.StartsWith("\\%") == true)
			{
				variableName = "%" + str.Substring(2);
			}
			if (str.EndsWith("\\%") == true)
			{
				variableName = variableName.ToString().TrimEnd('%').TrimEnd('\\') + "%";

			}
			return variableName;
		}

		private (object?, Error?) ConvertObjectToType(object obj, string typeFullName, string parameterName, object variableNameOrValue, bool isSqlite)
		{
			// TODO: because of bad structure in building, can be removed when fix
			if (typeFullName == "String") typeFullName = "System.String";


			if (obj is ObjectValue ov)
			{
				obj = ov.Value;
			}
			else if (obj is List<ObjectValue> ovList)
			{
				List<object?> list = new();
				ovList.ForEach(p => list.Add(p.Value));
				obj = list;
			}

			if (obj is System.DBNull || obj == null) return (null, null);

			Type? targetType = Type.GetType(typeFullName);
			if (targetType == null)
			{
				typeFullName = ConvertFromColumnTypeToCSharpTypeFullName(typeFullName);
				if (typeFullName != null)
				{
					targetType = Type.GetType(typeFullName);
				}
			}
			try
			{
				if (targetType == null)
				{
					return (null, new Error($"Could not find {typeFullName} for parameter {parameterName}", "TypeNotFound"));
				}

				var parseMethod = targetType.GetMethod("Parse", new[] { typeof(string) });
				if (parseMethod != null)
				{
					try
					{
						string value = FormatType(obj.ToString(), targetType);
						return (parseMethod.Invoke(null, [value]), null);
					}
					catch { }
				}

				if ((targetType == typeof(string) || targetType == typeof(object)) && (obj is JObject || obj is JArray || obj is JProperty || obj is JValue))
				{
					return (obj.ToString(), null);
				}
				if (obj is JArray jarray && (targetType.Name.StartsWith("IEnumerable") || targetType.Name.StartsWith("List")))
				{
					var array = Array.CreateInstance(targetType.GenericTypeArguments[0], jarray.Count);
					int idx = 0;
					foreach (JToken item in jarray)
					{
						var tmp = item.ToObject(targetType.GenericTypeArguments[0]);
						array.SetValue(tmp, idx++);
					}
					return (array, null);
				}
				if (isSqlite && obj is DateTime dt && targetType == typeof(string))
				{
					return (dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), null);
				}

				var convertedObj = TypeHelper.ConvertToType(obj, targetType);
				if (convertedObj == null) return (convertedObj, null);

				if (TypeHelper.IsConsideredPrimitive(convertedObj.GetType())) return (convertedObj, null);
				if (TypeHelper.IsRecordWithToString(convertedObj)) return (convertedObj.ToString(), null);

				return (JsonConvert.SerializeObject(convertedObj), null);
			}
			catch (Exception ex)
			{
				if (string.IsNullOrWhiteSpace(obj.ToString()) && (targetType == typeof(long) || targetType == typeof(double)))
				{
					var filterModule = GetProgramModule<FilterModule.Program>();
					var task = filterModule.FilterOnPropertyAndValue(new ObjectValue("function", function), "ParameterName", "@" + parameterName, retrieveOneItem: "first", propertyToExtract: "parent");
					task.Wait();
					var result = task.Result;
					if (result.Item1 != null)
					{
						var parameter = result.Item1 as JObject;
						return (0, new Error($"{parameter["VariableNameOrValue"]} is empty. Empty content cannot be used for the column {parameterName}. It must contains some value", "ConvertFailed", Exception: ex));
					}
					else
					{
						return (0, new Error($"{parameterName} is empty. Empty content cannot be used, it must contains some value", "ConvertFailed", Exception: ex));
					}
				}

				return (null, new Error($"Error converting '{obj}' to type {targetType} for parameter {parameterName}", "ConvertFailed", Exception: ex));

			}


		}

		private IEnumerable ConvertJArray(JArray jArray, Type targetType)
		{
			var listType = typeof(List<>).MakeGenericType(targetType);
			var toObjectMethod = typeof(JArray).GetMethod("ToObject", new Type[] { });
			var genericToObjectMethod = toObjectMethod.MakeGenericMethod(listType);

			var list = genericToObjectMethod.Invoke(jArray, null);

			return (IEnumerable)list;
		}

		private string FormatType(string value, Type targetType)
		{
			if (!value.StartsWith("0")) return value;
			if (targetType == null) return value;

			if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
			{
				if (value.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)) return value;
				var idx = FindFirstNonDigitIndex(value);
				value = value.Substring(0, idx) + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + value.Substring(idx + 1);
			}
			else if ((targetType == typeof(int) || targetType == typeof(long)) && value.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
			{
				var tmp = double.Parse(value);
				value = ((long)tmp).ToString();
			}

			return value;
		}

		private static int FindFirstNonDigitIndex(string input)
		{
			for (int i = 0; i < input.Length; i++)
			{
				if (!char.IsDigit(input[i]))
				{
					return i; // Return the index of the first non-digit character
				}
			}
			return -1; // Return -1 if no non-digit characters are found
		}

		private void Done(IDbConnection connection, IDbTransaction? transaction)
		{
			if (transaction == null && connection != null)
			{
				connection.Close(); 
				if (connection is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}
		}



		private async Task<(DataSource? DataSource, IError? Error)> GetDataSourcesByNames(List<string>? dataSourceNames = null)
		{

			if (dataSourceNames == null || dataSourceNames.Count == 0)
			{
				return await dbSettings.GetDataSourceOrDefault();
			}


			var main = dataSourceNames[0].Trim();
			var (dataSource, error) = await dbSettings.GetDataSource(main, goalStep);
			if (error != null) return (null, error);

			if (dataSourceNames.Count > 1)
			{
				foreach (var dataSourceName in dataSourceNames[1..])
				{

					(var ds, error) = await dbSettings.GetDataSource(dataSourceName.Trim(), goalStep, false);
					if (error != null) return (null, error);

					dataSource.Attach(ds.Name, ds.LocalPath);
				}
			}

			return (dataSource, null);
		}


	}
}
