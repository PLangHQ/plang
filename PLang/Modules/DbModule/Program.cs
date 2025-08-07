using Dapper;
using IdGen;
using Markdig.Extensions.TaskLists;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using ReverseMarkdown.Converters;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;
using System.IO.Abstractions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Dapper.SqlMapper;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Utils.VariableHelper;

namespace PLang.Modules.DbModule
{
	[Description("Database access, select, insert, update, delete and execute raw sql. Handles transactions. Sets and create datasources. Isolated data pattern (idp)")]
	public class Program : BaseProgram, IDisposable, IAsyncConstructor
	{
		//public static string DbConnectionContextKey = "DbConnection";
		//public static string DbTransactionContextKey = "DbTransaction";
		public static string CurrentDataSourceKey = "PLang.Modules.DbModule.CurrentDataSourceKey";

		private record DbConnectionSupported(string Key, string Name, Type Type);

		private ModuleSettings dbSettings;
		private readonly PrParser prParser;
		private readonly ProgramFactory programFactory;
		private readonly IDbServiceFactory dbFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IEventSourceRepository eventSourceRepository;
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;

		[Description("ParameterName must be prefixed with @. VariableNameOrValue can be any primative type, string or a %variable%")]
		public record ParameterInfo(string TypeFullName, string ParameterName, object? VariableNameOrValue);
		public record TableInfo(string Name, List<ColumnInfo> Columns);
		public record ColumnInfo(string Information);

		public Program(IDbServiceFactory dbFactory, IPLangFileSystem fileSystem, ISettings settings, ILlmServiceFactory llmServiceFactory,
			IEventSourceRepository eventSourceRepository, PLangAppContext context, ILogger logger, ITypeHelper typeHelper, ModuleSettings dbSettings, PrParser prParser, ProgramFactory programFactory) : base()
		{
			this.dbFactory = dbFactory;
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
			this.eventSourceRepository = eventSourceRepository;
			this.logger = logger;
			this.typeHelper = typeHelper;
			this.context = context;

			this.dbSettings = dbSettings;
			this.prParser = prParser;
			this.programFactory = programFactory;

			this.dbSettings.IsBuilder = this.IsBuilder;
		}

		public async Task<IError?> AsyncConstructor()
		{
			(DataSource? DataSource, IError? Error) result;
			if (instruction.Properties.TryGetValue("DataSource", out var name) && !string.IsNullOrWhiteSpace(name?.ToString()))
			{
				result = await dbSettings.GetDataSource(name.ToString(), goalStep);
			}
			else
			{
				result = await dbSettings.GetCurrentDataSource(goalStep);
			}

			if (result.Error != null) return result.Error;

			goalStep.AddVariable<DataSource>(result.DataSource);

			return null;
		}


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

			goal.AddVariable(datasource);
			return (datasource, null);
		}

		[Description("gets all databases that have been created")]
		public async Task<List<DataSource>> GetDataSources()
		{
			return await dbSettings.GetAllDataSources();
		}


		[Description("gets the current datasource or by name")]
		public async Task<(DataSource? DataSource, IError? Error)> GetDataSource([HandlesVariable] string? name = null)
		{
			DataSource? dataSource;
			if (string.IsNullOrEmpty(name))
			{
				dataSource = goal.GetVariable<DataSource>();
				if (dataSource != null) return (dataSource, null);
			}

			(dataSource, var error) = await dbSettings.GetDataSource(name);
			if (error != null) return (null, error);


			if (dataSource == null) return (null, new ProgramError($"Could not find datasource {name}", goalStep));
			return (dataSource, null);
		}

		public async Task<(DataSource? DataSource, IError? Error)> SetDataSourceName([HandlesVariable] string name)
		{
			if (string.IsNullOrEmpty(name)) return (null, new ProgramError("datasource name cannot be empty", goalStep));

			return await SetInternalDataSourceName(name, true);
		}
		private async Task<(DataSource? DataSource, IError? Error)> SetInternalDataSourceName([HandlesVariable] string name, bool setForGoal = false)
		{
			if (string.IsNullOrEmpty(name)) return (null, new ProgramError("Name of the datasource cannot be empty"));

			(var dataSource, var error) = await dbSettings.GetDataSource(name);
			if (error != null) return (null, error);

			if (setForGoal)
			{
				goal.AddVariable(dataSource);
				var @event = goal.GetVariable<EventBinding>(ReservedKeywords.Event);
				if (@event != null && @event.Goal != null)
				{
					@event.Goal.AddVariable(dataSource);
				}
			}
			else
			{
				goalStep.AddVariable(dataSource);
			}

			return (dataSource, null);
		}

		private string transactionKey = "transaction_{0}";
		private string connectionKey = "con_{0}";

		public async Task<IError?> BeginTransaction([HandlesVariable] List<string>? dataSourceNames = null, GoalToCallInfo? onRollback = null)
		{

			string? dataSourceName = (dataSourceNames != null) ? dataSourceNames[0] : "data";

			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName, goalStep);
			if (error != null) return error;

			goal.AddVariable(dataSource, variableName: "dataSourceTransaction");
			goalStep.AddVariable(dataSource);

			var dbConnection = dbFactory.CreateHandler(dataSource);
			if (dbConnection.State != ConnectionState.Open)
			{
				dbConnection.Open();

				if (dbConnection is SqliteConnection)
				{
					using var command = dbConnection.CreateCommand();
					command.CommandText = "PRAGMA foreign_keys = ON;";
					command.ExecuteNonQuery();
				}
			}
			var transaction = dbConnection.BeginTransaction();
			goal.AddVariable(transaction, () =>
			{
				transaction.Dispose();
				return Task.CompletedTask;
			});

			for (int i = 1; dataSourceNames?.Count > 1 && i < dataSourceNames.Count; i++)
			{
				(var attachDataSource, error) = await dbSettings.GetDataSource(dataSourceNames[i]);
				if (error != null) return error;

				var dbAbsolutePath = fileSystem.Path.Join(goalStep.Goal.AbsoluteAppStartupFolderPath, attachDataSource.LocalPath);

				await ExecuteRaw(null, $"ATTACH DATABASE '{dbAbsolutePath}' AS {attachDataSource.Name};");
			}

			return null;
		}
		/*
		private async Task<(string? Name, IError? Error)> GetNameForConnection(string? name = null)
		{
			if (!string.IsNullOrEmpty(name)) return (name, null);

			if (goal == null) return (null, new ProgramError("No goal loaded", Key: "NoGoal"));

			var dataSource = goalStep.GetVariable<DataSource>();
			if (dataSource == null)
			{
				var dataSources = await dbSettings.GetAllDataSources();
				dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			}


			if (dataSource == null) return (null, new ProgramError("No datasource to create transaction on", Key: "NoDataSource"));

			return (dataSource.Name, null);
		}*/

		public async Task<IError?> EndTransaction()
		{
			var transaction = goal.GetVariable<IDbTransaction>();
			if (transaction == null) return new ProgramError("No transaction found");

			transaction.Commit();
			transaction.Connection?.Close();

			goal.RemoveVariable<IDbTransaction>();

			return null;
		}

		public async Task<IError?> Rollback()
		{
			var transaction = goal.GetVariable<IDbTransaction>();
			if (transaction == null) return new ProgramError("No transaction found");

			transaction.Rollback();
			transaction.Connection?.Close();

			goal.RemoveVariable<IDbTransaction>();

			return null;

		}
		public async Task<IError?> LoadExtension([HandlesVariable] string dataSourceName, string fileName, string? procName = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return error;

			return await LoadExtension(dataSource, fileName, procName);
		}
		internal async Task<IError?> LoadExtension(DataSource dataSource, string fileName, string? procName = null)
		{
			var dbConnection = dbFactory.CreateHandler(dataSource);
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
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
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
		public async Task<(List<TableInfo>? TablesAndColumns, IError? Error)> GetDatabaseStructure([HandlesVariable] string dataSourceName, List<string>? tables = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (null, error);

			return await GetDatabaseStructure(dataSource, tables);
		}
			
		internal async Task<(List<TableInfo>? TablesAndColumns, IError? Error)> GetDatabaseStructure(DataSource dataSource, List<string>? tables = null)
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
					columns.Add(new ColumnInfo(JsonConvert.SerializeObject(column)));
				}
				tableInfos.Add(new TableInfo(row.Get<string>("name"), columns));


			}
			return (tableInfos, null);
		}

		public async void Dispose()
		{
			await EndTransaction();
		}



		private (IDbConnection? connection, IDbTransaction? transaction, DynamicParameters? param, string sql, IError? error) Prepare(DataSource dataSource, string sql, List<ParameterInfo>? Parameters = null, bool isInsert = false)
		{
			IDbConnection? connection;
			var transaction = goal.GetVariable<IDbTransaction>();
			if (transaction != null)
			{
				connection = transaction.Connection;
				dataSource = goal.GetVariable<DataSource>("dataSourceTransaction")!;
			}
			else
			{
				connection = dbFactory.CreateHandler(dataSource);
			}
			bool isSqlite = (dataSource.TypeFullName.Contains("sqlite", StringComparison.OrdinalIgnoreCase));

			var paramResult = GetDynamicParameters(sql, isInsert, Parameters, isSqlite);
			if (paramResult.Error != null) return (null, null, null, sql, paramResult.Error);

			if (connection != null && connection.State != ConnectionState.Open)
			{
				connection.Open();
				if (connection is SqliteConnection sqliteConnection)
				{
					using var command = connection.CreateCommand();
					command.Transaction = transaction;
					command.CommandText = "PRAGMA foreign_keys = ON;";
					command.ExecuteNonQuery();

					if (sqliteConnection.ConnectionString.Contains("Memory;"))
					{
						var anchors = context.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb", new(StringComparer.OrdinalIgnoreCase)) ?? new(StringComparer.OrdinalIgnoreCase);
						if (!anchors.ContainsKey(dataSource.Name))
						{
							var anchorConnection = dbFactory.CreateHandler(dataSource);
							anchorConnection.Open();
							anchors.Add(dataSource.Name, anchorConnection);

							context.AddOrReplace("AnchorMemoryDb", anchors);
						}


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
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
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
		public async Task<(long, IError?)> ExecuteDynamicSql([HandlesVariable] string dataSourceName, string sql, List<string> tableAllowList)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await ExecuteDynamicSql(dataSource, sql, tableAllowList);
		}


		internal async Task<(long, IError?)> ExecuteDynamicSql(DataSource dataSource, string sql, List<string> tableAllowList)
		{
			return await Execute(dataSource, sql, tableAllowList);
		}
		[Description("Executes a sql statement that defined by user. This statement will be validated. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be updated")]
		public async Task<(long RowsAffected, IError? Error)> Execute([HandlesVariable] string dataSourceName, string sql, List<string> tableAllowList)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await Execute(dataSource, sql, tableAllowList);
		}

		
		internal async Task<(long RowsAffected, IError? Error)> Execute(DataSource dataSource, string sql, List<string> tableAllowList)
		{
			if (tableAllowList.Count == 0)
			{
				return (0, new ProgramError("You must define a allow list of tables"));
			}

			//todo: this is not a secure allow check
			//should deconstruct the sql to find out real table in sql statement
			foreach (var table in tableAllowList)
			{
				if (!sql.Contains(table, StringComparison.OrdinalIgnoreCase))
				{
					return (0, new ProgramError($"Table {table} was not in sql: {sql}"));
				}
			}
			return await ExecuteRaw(dataSource, sql);
		}

		private async Task<(long RowsAffected, IError? Error)> ExecuteRaw(DataSource dataSource, string sql)
		{
			long rowsAffected = 0;

			List<ParameterInfo>? parameters = null;
			if (sql.Contains("@id"))
			{
				parameters = new List<ParameterInfo>();
				parameters.Add(new ParameterInfo("System.Int64", "@id", "auto"));
			}

			var prepare = Prepare(dataSource, sql, parameters);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				}
				else
				{
					rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param, prepare.transaction);
				}

				Done(prepare.connection);
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
				Done(prepare.connection);
			}
		}

		private void ShowWarning(Exception ex)
		{
			logger.LogWarning($"Had error running Setup ({goalStep.Text}) but will continue. Error message:{ex.Message}");
		}

		[Description("When user does not define a primary key, add it to the create statement as id column not null, when KeepHistory is set to false, make the column auto increment")]
		public async Task<(long, IError?)> CreateTable(string sql)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(goal.DataSourceName);
			if (error != null) return (0, error);
			return await ExecuteRaw(dataSource, sql);

		}
		[Description("When select should return 1 row (limit 1)")]
		public async Task<(object?, IError? errors)> SelectOneRow([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (null, error);

			return await SelectOneRow(dataSource, sql, sqlParameters);
		}
		internal async Task<(object?, IError? errors)> SelectOneRow(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			var result = await Select(dataSource, sql, sqlParameters);
			if (result.Error != null)
			{
				return (null, result.Error);
			}
			if (result.Table == null || result.Table.Count == 0) return (null, null);

			if (function.ReturnValues != null && function.ReturnValues.Count == 1 && result.Table.ColumnNames.Count != 1)
			{
				return (result.Table[0], null);
			}


			var returnValues = new List<ObjectValue>();
			if (result.Table.ColumnNames.Count == 1)
			{
				var columnName = result.Table.ColumnNames[0];
				return (new ObjectValue(columnName, result.Table[columnName]), null);
			}
			foreach (var columnName in result.Table.ColumnNames)
			{
				returnValues.Add(new ObjectValue(columnName, result.Table[columnName]));
			}
			return (returnValues, null);




		}
		public async Task<(Table? Table, IError? Error)> Select([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (null, error);

			return await Select(dataSource, sql, sqlParameters);
		}

		internal async Task<(Table? Table, IError? Error)> Select(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			var prep = Prepare(dataSource, sql, sqlParameters);

			try
			{
				if (prep.error != null)
				{
					return (null, prep.error);
				}
				logger.LogDebug($"Sql: {prep.sql} - Parameters:{prep.param}");

				var con = (DbConnection)prep.connection;
				await using var cmd = con.CreateCommand();
				cmd.CommandText = prep.sql;
				if (prep.transaction != null)
				{
					cmd.Transaction = (DbTransaction)prep.transaction;
				}

				// Add parameters if any:
				if (sqlParameters is not null)
				{
					foreach (var prop in sqlParameters)
					{
						var param = cmd.CreateParameter();
						param.ParameterName = prop.ParameterName;
						param.Value = ConvertToType(prop.VariableNameOrValue, prop.TypeFullName) ?? DBNull.Value;

						cmd.Parameters.Add(param);
					}
				}

				using var reader = await cmd.ExecuteReaderAsync();


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


				//var rows = (await prep.connection.QueryAsync<dynamic>(prep.sql, prep.param)).ToList();
				logger.LogDebug($"Rows: {table.Count}");


				return (table == null) ? (new(cols), null) : (table, null);
			}
			catch (Exception ex)
			{
				return (null, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prep.connection);
			}
		}

		private object? ConvertToType(object? variableNameOrValue, string fullType)
		{
			if (variableNameOrValue is JToken token)
			{
				variableNameOrValue = token.ToString();
			}

			return TypeHelper.ConvertToType(variableNameOrValue, Type.GetType(fullType));
		}

		private object? GetDefaultValue(string strType)
		{
			if (strType == "dynamic") return new List<object>();
			if (strType == "string") return null;

			var type = Type.GetType(strType);
			if (type == null) return null;

			return type.IsValueType && !type.IsPrimitive ? Activator.CreateInstance(type) : null;
		}
		public async Task<(long, IError?)> Update([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await Update(dataSource, sql, sqlParameters);
		}
		internal async Task<(long, IError?)> Update(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{

			var prepare = Prepare(dataSource, sql, sqlParameters);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				long result;
				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					result = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				}
				else
				{
					result = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param, prepare.transaction);
				}
				return (result, null);
			}
			catch (Exception ex)
			{
				return (0, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection);
			}

		}
		public async Task<(long, IError?)> Delete([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await Delete(dataSource, sql, sqlParameters);
		}
		internal async Task<(long, IError?)> Delete(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			long rowsAffected;
			var prepare = Prepare(dataSource, sql, sqlParameters);
			try
			{
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}


				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				}
				else
				{
					rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param, prepare.transaction);
				}

				return (rowsAffected, null);
			}
			catch (Exception ex)
			{
				return (0, new SqlError(ex.Message, sql, sqlParameters, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection);
			}
		}

		[Description("Insert or update table(Upsert). Will return affected row count. Choose when user doesn't write result into %variable%")]
		public async Task<(long rowsAffected, IError? error)> InsertOrUpdate([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await Insert(dataSource, sql, sqlParameters);
		}

		[Description("Insert or update table(Upsert). Will return the id/primary key of the affected row. Used when user intends to write into a %id%")]
		public async Task<(object? rowsAffected, IError? error)> InsertOrUpdateAndSelectIdOfRow([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await InsertAndSelectIdOfInsertedRow(dataSource, sql, sqlParameters);
		}

		[Description("Insert into table. Will return affected row count. Choose when user doesn't write result into %variable%")]
		public async Task<(long rowsAffected, IError? error)> Insert([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
			if (error != null) return (0, error);

			return await Insert(dataSource, sql, sqlParameters);
		}

		internal async Task<(long rowsAffected, IError? error)> Insert(DataSource dataSource, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			long rowsAffected = 0;
			var prepare = Prepare(dataSource, sql, sqlParameters, true);
			if (prepare.error != null)
			{
				return (0, prepare.error);
			}
			try
			{

				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction);
				}
				else
				{
					rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param, prepare.transaction);
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
				Done(prepare.connection);
			}
			return (rowsAffected, null);

		}

		[Description("Insert statement that will return the id of the inserted row.  Used when user intends to write into a %id%")]
		public async Task<(object?, IError?)> InsertAndSelectIdOfInsertedRow([HandlesVariable] string dataSourceName, string sql, List<ParameterInfo>? sqlParameters = null)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
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

				if (eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
				{
					var value = await prepare.connection.QuerySingleOrDefaultAsync(prepare.sql, prepare.param, prepare.transaction) as IDictionary<string, object>;
					Done(prepare.connection);

					return (value.FirstOrDefault().Value, null);
				}
				else
				{
					var id = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, prepare.transaction, returnId: true);
					Done(prepare.connection);

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
				Done(prepare.connection);
			}

		}

		private List<string> GetProperties(object obj)
		{
			if (obj is JObject jObject)
			{
				return jObject.Properties().Select(p => p.Name).ToList();
			}
			else if (obj is ExpandoObject eo)
			{
				return ((IDictionary<string, object>)eo).Keys.ToList();
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
				var type = obj.GetType();
				var property = obj.GetType().GetProperty(propertyName);
				if (property != null)
				{
					return property.GetValue(obj);
				}
				else
				{
					return GetValue(JObject.FromObject(obj), propertyName);
				}
			}
			return null;
		}
		[Description("ONLY When inserting list of items(%variables% is plural). Insert a list(bulk) into database, return number of rows inserted. columnMapping maps which variable should match with a column. User will define that he is using bulk insert.")]
		public async Task<(long, IError?)> InsertBulk([HandlesVariable] string dataSourceName, string tableName, List<object> itemsToInsert, [HandlesVariable] Dictionary<string, object>? columnMapping = null, bool ignoreContraintOnInsert = false)
		{
			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceName);
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

			long affectedRows = 0;
			var generator = new IdGenerator(1);
			var id = generator.ElementAt(0);
			IDbTransaction? transaction = goal.GetVariable<IDbTransaction>(string.Format(transactionKey, dataSource.Name));
			if (transaction == null)
			{
				await BeginTransaction();
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
					}
					else if (propertiesInItems.FirstOrDefault(p => p.Equals(cleanedColumnValue, StringComparison.OrdinalIgnoreCase)) != null
						|| memoryStack.Contains(cleanedColumnValue))
					{
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

				}
				if (!rowHasAnyValue) { continue; }

				var insertResult = await Insert(dataSource, sql, param);
				if (insertResult.error != null)
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
				: declared.IndexOf("CHAR", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("CLOB", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(string)
				: declared.IndexOf("BLOB", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(byte[])
				: declared.IndexOf("REAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("FLOA", StringComparison.OrdinalIgnoreCase) >= 0 ||
				  declared.IndexOf("DOUB", StringComparison.OrdinalIgnoreCase) >= 0 ? typeof(double)
				: null;

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
					if (memoryStack.Contains(valueKey))
					{
						if (columns != null) columns += ", ";
						columns += column.Key;

						if (values != null) values += ", ";
						values += $"@{column.Key}";
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
						values += $"@{column.Key}";
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
			(var dataSource, var error) = await dbSettings.GetCurrentDataSource(goalStep);
			if (error != null) return (string.Empty, error);

			List<ParameterInfo> parameters = new();
			parameters.Add(new ParameterInfo("System.String", "Database", dataSource.DbName));

			(var connection, var transaction, var par, _, error) = Prepare(dataSource, "", parameters);
			if (error != null)
			{
				return (string.Empty, error);
			}
			var result = await connection.QueryAsync(dataSource.SelectTablesAndViews, par, transaction);


			return (@$"## tables in database ##
{JsonConvert.SerializeObject(result)}
## tables in database ##
", null);
		}


		private (DynamicParameters DynamicParameters, IError? Error) GetDynamicParameters(string sql, bool isInsert, List<ParameterInfo>? Parameters, bool isSqlite)
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

				if (p.VariableNameOrValue?.ToString() == "auto" && eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
				{
					return (param, new Error("Auto incremental cannot be handled by plang when Event sourcing is disabled"));
				}

				if (isInsert && parameterName == "id" && (p.VariableNameOrValue?.ToString() == "auto" || eventSourceRepository.GetType() != typeof(DisableEventSourceRepository)))
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
					var variableValue = variableName; // variableHelper.LoadVariables(variableName);
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
						if (parameterName == "id" && eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
						{
							var dataSource = goalStep.GetVariable<DataSource>();
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

		private void Done(IDbConnection connection)
		{
			var transaction = goal.GetVariable<IDbTransaction>();
			if (transaction == null && connection != null)
			{
				connection.Close();
			}
		}

		/*
		private async Task<(DataSource?, IError?)> GetRuntimeDataSource(DataSource datasource, List<ObjectValue> variables)
		{
			var parameters = new Dictionary<string, object?>();

			var dataSourceVariables = variableHelper.GetVariables(datasource.Name);
			string localPath = datasource.LocalPath;
			string connectionString = datasource.ConnectionString;

			for (int i = 0; i < variables.Count; i++)
			{
				if (variables[i].Value == null || string.IsNullOrEmpty(variables[i].Value?.ToString()))
				{
					return (null, new StepError($"Variable {variables[i].Name} has not been set.", goalStep, "UndefinedVariable"));
				}

				parameters.Add($"%variable{i}%", variables[i].Value);

				localPath = localPath.Replace($"%variable{i}%", variables[i].Value.ToString());
				connectionString = connectionString.Replace($"%variable{i}%", variables[i].Value.ToString());
			}

			string dirPath = Path.GetDirectoryName(localPath);
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

			var runtimeDataSource = datasource with { LocalPath = localPath, ConnectionString = connectionString };
			if (memoryStack.Contains("GetRuntimeDataSource"))
			{
				return (runtimeDataSource, null);
			}


			parameters.Add("GetRuntimeDataSource", true);

			var setupGoal = prParser.GetAllGoals().FirstOrDefault(p => p.DataSourceName != null && p.DataSourceName.Equals(datasource.Name));
			if (setupGoal != null)
			{
				var storedKey = "%__stepsExecuted__%";

				goalStep.AddVariable(runtimeDataSource);

				var plangRuntime = programFactory.GetProgram<PlangModule.Program>(goalStep);
				var varsRuntime = programFactory.GetProgram<VariableModule.Program>(goalStep);
				var (value, error) = await varsRuntime.Load([storedKey]);
				if (error != null) return (null, error);

				bool hasExecuted = false;
				GroupedErrors groupedErrors = new();
				var stepExecuted = (value as JObject)?.ToObject<Dictionary<string, DateTime>>() ?? new();
				foreach (var step in setupGoal.GoalSteps)
				{
					if (stepExecuted.ContainsKey(step.Hash)) continue;

					hasExecuted = true;
					(var result, error) = await plangRuntime.RunStep(step, parameters);
					if (error != null)
					{
						groupedErrors.Add(error);
					}
					else
					{
						stepExecuted.AddOrReplace(step.Hash, DateTime.UtcNow);
					}
				}
				if (!hasExecuted) return (runtimeDataSource, null);


				object? tmp = null;
				if (memoryStack.Contains(storedKey))
				{
					tmp = memoryStack.Get(storedKey);
				}
				this.goal.AddVariable(datasource);

				memoryStack.Put(storedKey, stepExecuted);
				error = await varsRuntime.Store([storedKey]);
				memoryStack.Remove(storedKey);

				if (tmp != null)
				{
					memoryStack.Put(storedKey, tmp);
				}
				if (error != null) return (null, error);
			}

			return (runtimeDataSource, null);
		}*/

	}
}
