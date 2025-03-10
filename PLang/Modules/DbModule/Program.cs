using Dapper;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Globalization;
using static Dapper.SqlMapper;

namespace PLang.Modules.DbModule
{
	[Description("Database access, select, insert, update, delete and execute raw sql. Handles transactions. Sets and create datasources. Isolated data pattern (idp)")]
	public class Program : BaseProgram, IDisposable
	{
		public static string DbConnectionContextKey = "DbConnection";
		public static string DbTransactionContextKey = "DbTransaction";
		public static string CurrentDataSourceKey = "PLang.Modules.DbModule.CurrentDataSourceKey";

		private record DbConnectionSupported(string Key, string Name, Type Type);

		private ModuleSettings moduleSettings;
		private readonly IDbConnection dbConnection;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IEventSourceRepository eventSourceRepository;
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;

		public Program(IDbConnection dbConnection, IPLangFileSystem fileSystem, ISettings settings, ILlmServiceFactory llmServiceFactory, 
			IEventSourceRepository eventSourceRepository, PLangAppContext context, ILogger logger, ITypeHelper typeHelper) : base()
		{
			this.dbConnection = dbConnection;
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
			this.eventSourceRepository = eventSourceRepository;
			this.logger = logger;
			this.typeHelper = typeHelper;
			this.context = context;

			this.moduleSettings = new ModuleSettings(fileSystem, settings, context, llmServiceFactory, logger, typeHelper);
		}

		[Description("localPath is location of the database on the drive for sqlite. localPath can be string with variables, default is null")]
		public async Task CreateDataSource(string name = "data", [HandlesVariable] string? localPath = null, string databaseType = "sqlite", bool setAsDefaultForApp = false, bool keepHistoryEventSourcing = false)
		{
			localPath = variableHelper.LoadVariables(localPath)?.ToString();

			await moduleSettings.CreateDataSource(name, localPath, databaseType, setAsDefaultForApp, keepHistoryEventSourcing);
		}

		public async Task<IError?> SetDataSourceName(string? name = null, string? localPath = null)
		{
			(var dataSource, var error) = await moduleSettings.GetDataSource(name, localPath, goalStep);
			if (error != null) return error;

			context[ReservedKeywords.CurrentDataSource] = dataSource;
			return null;
		}

		[Description("Create Isolated data pattern (IDP) for the system.")]
		public async Task<(ReturnDictionary<string, object?>? Variables, IError? Error)> CreateIsolatedDataPattern(string id, string setupGoalFile, string? name = null, bool keepHistory = true, bool defaultForApp = false)
		{
			var parameters = new Dictionary<string, object?>();
			parameters.Add("id", id);
			parameters.Add("setupGoalFile", setupGoalFile);
			parameters.Add("name", !string.IsNullOrEmpty(name) ? name : id);
			parameters.Add("keepHistory", keepHistory);
			parameters.Add("defaultForApp", defaultForApp);

			var callModule = GetProgramModule<CallGoalModule.Program>();
			var result = await callModule.RunGoal("/modules/DbModule/CreateIsolatedDataPattern", parameters, isolated: true);
			return result;
		}

		public async Task BeginTransaction()
		{
			if (dbConnection.State != ConnectionState.Open) dbConnection.Open();
			var transaction = dbConnection.BeginTransaction();
			context.AddOrReplace(DbConnectionContextKey, dbConnection);
			context.AddOrReplace(DbTransactionContextKey, transaction);

		}

		public async Task EndTransaction()
		{
			if (context.TryGetValue(DbTransactionContextKey, out object? transaction) && transaction != null)
			{
				((IDbTransaction)transaction).Commit();
				context.Remove(DbTransactionContextKey);
			}

			if (context.TryGetValue(DbConnectionContextKey, out object? connection) && connection != null)
			{
				((IDbConnection)connection).Close();
				context.Remove(DbConnectionContextKey);
			}
		}

		public async Task Rollback()
		{
			if (context.TryGetValue(DbTransactionContextKey, out object? transaction) && transaction != null)
			{
				((IDbTransaction)transaction).Rollback();
				context.Remove(DbTransactionContextKey);
			}

			if (context.TryGetValue(DbConnectionContextKey, out object? connection) && connection != null)
			{
				((IDbConnection)connection).Close();
				context.Remove(DbConnectionContextKey);
			}
		}

		public async Task<IError?> LoadExtension(string fileName, string? procName = null)
		{
			if (dbConnection is not SqliteConnection)
			{
				return new Error("Loading extension only works for Sqlite", "NotSupported");
			}

			fileName = GetPath(fileName);
			if (!fileSystem.File.Exists(fileName))
			{
				return new Error("File could not be found.", "FileNotFound");
			}

			((SqliteConnection)dbConnection).LoadExtension(fileName, procName);
			return null;

		}

		[Description("Returns tables and views in database with the columns description")]
		public async Task<(Dictionary<string, object>?, IError?)> GetDatabaseStructure(string[]? tables = null, string? dataSourceName = null)
		{
			var dataSource = await moduleSettings.GetCurrentDataSource();
			var result = await Select(dataSource.SelectTablesAndViews);
			if (result.error != null)
			{
				return (null, result.error);
			}
			var dict = new Dictionary<string, object>();
			foreach (var item in result.rows)
			{
				var tbl = (dynamic)item;

				if (tables != null)
				{
					if (tables.FirstOrDefault(p => p.Equals(tbl.name, StringComparison.OrdinalIgnoreCase)) == null) {
						continue;
					}
				}
				var sql = await moduleSettings.FormatSelectColumnsStatement(tbl.name);
				var columns = await Select(sql);			
				dict.Add(tbl.name, columns);

			}
			return (dict, null);
		}

		public async void Dispose()
		{
			await EndTransaction();
		}

		public record ParameterInfo(string ParameterName, object VariableNameOrValue, string TypeFullName);

		private (IDbConnection connection, DynamicParameters param, string sql, IError? error) Prepare(string sql, List<object>? Parameters = null, bool isInsert = false)
		{
			IDbConnection connection = context.ContainsKey(DbConnectionContextKey) ? context[DbConnectionContextKey] as IDbConnection : dbConnection;
			var multipleErrors = new GroupedErrors("SqlParameters");
			var param = new DynamicParameters();
			if (Parameters != null)
			{
				foreach (var parameter in Parameters)
				{
					var p = parameter as ParameterInfo;
					if (parameter is JObject)
					{
						if (((JObject)parameter).GetValue("Type") != null)
						{
							var obj = (JObject)parameter;
							p = new ParameterInfo(obj.GetValue("Name").ToString(), obj.GetValue("Value"), obj.GetValue("Type").ToString());
						}
						else
						{
							p = ((JObject)parameter).ToObject<ParameterInfo>();
						}
					}
					else if (parameter is string && JsonHelper.IsJson(parameter))
					{
						p = JsonConvert.DeserializeObject<ParameterInfo>(parameter.ToString());
					}

					var parameterName = p.ParameterName.Replace("@", "");
					if (parameterName.Contains("."))
					{
						var oldParameterName = parameterName;
						parameterName = parameterName.Replace(".", "");
						sql = sql.Replace(oldParameterName, parameterName);
					}
					if (isInsert && parameterName == "id" && eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
					{
						var id = p.VariableNameOrValue;
						if (id.ToString() == "auto" || string.IsNullOrEmpty(p.VariableNameOrValue.ToString()))
						{
							var generator = new IdGenerator(1);
							id = generator.ElementAt(0);
						}
						param.Add("@" + parameterName, id, DbType.Int64);
					}
					else if (p.VariableNameOrValue == null)
					{
						param.Add("@" + parameterName, null);
					}
					/*
					else if (p.VariableNameOrValue is JArray)
					{
						var jarray = (JArray)p.VariableNameOrValue;
						StringBuilder placeholders = new StringBuilder();
						for (int i = 0; i < jarray.Count; i++)
						{
							placeholders.Append($"@category{i}");
							if (i < jarray.Count - 1)
							{
								placeholders.Append(", ");
							}
							param.Add($"@category{i}", ConvertObjectToType(jarray[i], p.TypeFullName, parameterName));
						}
						sql = sql.Replace(p.ParameterName.ToString(), placeholders.ToString());

					}*/
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
						(object? value, Error? error) = ConvertObjectToType(variableValue, p.TypeFullName, parameterName, p.VariableNameOrValue);
						if (error != null) multipleErrors.Add(error);

						param.Add("@" + parameterName, prefix + value + postfix);
					}
					else
					{
						string prefix = "";
						string postfix = "";
						var variableName = p.VariableNameOrValue;
						if (variableName.ToString().StartsWith("\\%"))
						{
							variableName = variableName.ToString().Substring(2);
							prefix = "%";
						}
						if (variableName.ToString().EndsWith("\\%"))
						{
							variableName = variableName.ToString().TrimEnd('%').TrimEnd('\\');
							postfix = "%";
						}


						(object? value, Error? error) = ConvertObjectToType(prefix + variableName + postfix, p.TypeFullName, parameterName, p.VariableNameOrValue);
						if (error != null)
						{
							if (parameterName == "id" && eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
							{
								var dataSource = moduleSettings.GetCurrentDataSource().Result;
								multipleErrors.Add(new ProgramError($"Parameter @id is empty. Are you on the right data source? Current data source is {dataSource.Name}", goalStep, function));
							}
							multipleErrors.Add(error);
						}
						param.Add("@" + parameterName, value);
					}
				}
			}
			if (connection != null && connection.State != ConnectionState.Open) connection.Open();

			var errorToReturn = (multipleErrors.Errors.Count == 0) ? null : multipleErrors;
			return (connection, param, sql, errorToReturn);

		}

		private (object?, Error?) ConvertObjectToType(object obj, string typeFullName, string parameterName, object variableNameOrValue)
		{
			// TODO: because of bad structure in building, can be removed when fix
			if (typeFullName == "String") typeFullName = "System.String";

			Type? targetType = Type.GetType(typeFullName);
			if (targetType == null)
			{
				typeFullName = ConvertFromColumnTypeToCSharpType(typeFullName);
				targetType = Type.GetType(typeFullName);
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

				if (targetType == typeof(string) && (obj is JObject || obj is JArray || obj is JProperty || obj is JValue))
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


				return (Convert.ChangeType(obj, targetType), null);
			}
			catch (Exception ex)
			{
				if (string.IsNullOrWhiteSpace(obj.ToString()) && (targetType == typeof(long) || targetType == typeof(double)))
				{
					var filterModule = GetProgramModule<FilterModule.Program>();
					var task = filterModule.FilterOnPropertyAndValue(function, "ParameterName", "@" + parameterName, retrieveOneItem: "first", propertyToExtract: "parent");
					task.Wait();
					var parameter =  task.Result as JObject;
					if (parameter != null)
					{
						return (0, new Error($"{parameter["VariableNameOrValue"]} is empty. Empty content cannot be inserted into the column {parameterName}", "ConvertFailed", Exception: ex));
					}
					else
					{
						return (0, new Error($"{parameterName} is empty. Empty content cannot be inserted into the column", "ConvertFailed", Exception: ex));
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
			if (!context.ContainsKey(DbConnectionContextKey) && connection != null)
			{
				connection.Close();
			}
		}

		public async Task<(int, IError?)> InsertEventSourceData(long id, string data, string keyHash)
		{
			var transaction = context[DbTransactionContextKey] as IDbTransaction;
			IDbConnection connection = context.ContainsKey(DbConnectionContextKey) ? context[DbConnectionContextKey] as IDbConnection : dbConnection;

			return await eventSourceRepository.AddEventSourceData(connection, id, data, keyHash, transaction);
		}


		public async Task<(long, IError?)> Execute(string sql, string? dataSourceName = null)
		{
			try
			{
				if (!string.IsNullOrEmpty(dataSourceName))
				{
					await SetDataSourceName(dataSourceName);
				}

				long rowsAffected = 0;
				var prepare = Prepare(sql, null);
				if (prepare.error != null)
				{
					return (0, prepare.error);
				}

				var transaction = context[DbTransactionContextKey] as IDbTransaction;
				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, null);
				}
				else
				{
					rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param, transaction);
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
				return (0, new Error(ex.Message, "ExecuteSql", Exception: ex));

			}
		}

		private void ShowWarning(Exception ex)
		{
			logger.LogWarning($"Had error running Setup ({goalStep.Text}) but will continue. Error message:{ex.Message}");
		}

		public async Task<(long, IError?)> CreateTable(string sql, string? dataSourceName = null)
		{

			return await Execute(sql, dataSourceName);

		}

		public async Task<(object?, IError? errors)> SelectOneRow(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				await SetDataSourceName(dataSourceName);
			}

			var result = await Select(sql, SqlParameters);
			if (result.error != null)
			{
				return (null, result.error);
			}

			if (result.rows.Count == 0)
			{
				if (this.function == null || this.function.ReturnValues == null || this.function.ReturnValues.Count == 1) return (null, null);

				var dict = new ReturnDictionary<string, object?>();
				foreach (var rv in this.function.ReturnValues)
				{
					dict.Add(rv.VariableName, GetDefaultValue(rv.Type));
				}
				return (dict, null);
			}

			var rowsAsList = ((IList<object>)result.rows);
			var columns = ((IDictionary<string, object>)rowsAsList[0]);

			if (columns.Count == 1)
			{
				return (columns.FirstOrDefault().Value, null);
			}
			return (result.rows[0], null);

		}

		public async Task<(List<object> rows, IError? error)> Select(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				await SetDataSourceName(dataSourceName);
			}

			var prep = Prepare(sql, SqlParameters);
			if (prep.error != null)
			{
				return (new(), prep.error);
			}
			logger.LogDebug($"Sql: {prep.sql} - Parameters:{prep.param}");
			var rows = (await prep.connection.QueryAsync<dynamic>(prep.sql, prep.param)).ToList();
			logger.LogDebug($"Rows: {rows.Count}");
			Done(prep.connection);

			return (rows == null) ? (new(), null) : (rows, null);
		}

		private object? GetDefaultValue(string strType)
		{
			if (strType == "dynamic") return new List<object>();
			if (strType == "string") return null;

			var type = Type.GetType(strType);
			if (type == null) return null;

			return type.IsValueType && !type.IsPrimitive ? Activator.CreateInstance(type) : null;
		}

		public async Task<(long, IError?)> Update(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				await SetDataSourceName(dataSourceName);
			}
			var prepare = Prepare(sql, SqlParameters);
			if (prepare.error != null)
			{
				return (0, prepare.error);
			}
			long result;
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{
				result = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param);
			}
			else
			{
				result = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param);
			}
			Done(prepare.connection);
			return (result, null);
		}

		public async Task<(long, IError?)> Delete(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				await SetDataSourceName(dataSourceName);
			}
			long rowsAffected;
			var prepare = Prepare(sql, SqlParameters);
			if (prepare.error != null)
			{
				return (0, prepare.error);
			}
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{
				rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param);
			}
			else
			{
				rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param);
			}
			Done(prepare.connection);
			return (rowsAffected, null);
		}

		[Description("Insert or update table(Upsert). Will return affected row count")]
		public async Task<(long rowsAffected, IError? error)> InsertOrUpdate(string sql, List<object>? SqlParameters = null, string? dataSourceName = null) { 
			return await Insert(sql, SqlParameters, dataSourceName);
		}

		[Description("Insert or update table(Upsert). Will return the primary key of the affected row")]
		public async Task<(object? rowsAffected, IError? error)> InsertOrUpdateAndSelectIdOfRow(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			return await InsertAndSelectIdOfInsertedRow(sql, SqlParameters, dataSourceName);
		}



		[Description("Insert into table. Will return affected row count")]
		public async Task<(long rowsAffected, IError? error)> Insert(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				await SetDataSourceName(dataSourceName);
			}
			long rowsAffected = 0;
			var prepare = Prepare(sql, SqlParameters, true);
			if (prepare.error != null)
			{
				return (0, prepare.error);
			}
			try
			{

				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param);
				}
				else
				{
					rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param);
				}
			}
			catch (Exception ex)
			{
				if (GoalHelper.IsSetup(goalStep) && ex.ToString().Contains("duplicate key"))
				{
					ShowWarning(ex);
					return (rowsAffected, null);
				}
				return (0, new ProgramError(ex.Message, goalStep, function, Exception: ex));
			}
			finally
			{
				Done(prepare.connection);
			}
			return (rowsAffected, null);

		}
		[Description("Insert statement that will return the id of the inserted row. Use only if user requests the id")]
		public async Task<(object?, IError?)> InsertAndSelectIdOfInsertedRow(string sql, List<object>? SqlParameters = null, string? dataSourceName = null)
		{
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				await SetDataSourceName(dataSourceName);
			}

			var prepare = Prepare(sql, SqlParameters, true);
			if (prepare.error != null)
			{
				return (0, prepare.error);
			}

			if (eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
			{
				var value = await prepare.connection.QuerySingleOrDefaultAsync(prepare.sql, prepare.param) as IDictionary<string, object>;
				Done(prepare.connection);
				return (value.FirstOrDefault().Value, null);
			}
			else
			{
				var id = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param, returnId: true);
				Done(prepare.connection);
				if (id != 0)
				{
					return (id, null);
				}

				if (prepare.param.ParameterNames.Contains("id"))
				{
					return (prepare.param.Get<long>("id"), null);
				}

				return (null, null);
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
				} else
				{
					return GetValue(JObject.FromObject(obj), propertyName);
				}
			}
			return null;
		}

		[Description("Insert a list(bulk) into database, return number of rows inserted. columnMapping maps which variable should match with a column. User will define that he is using bulk insert.")]
		public async Task<(long, IError?)> InsertBulk(string tableName, List<object> itemsToInsert, [HandlesVariable] Dictionary<string, object>? columnMapping = null, bool ignoreContraintOnInsert = false)
		{
			if (itemsToInsert.Count == 0) return (0, null);

			var dataSource = await moduleSettings.GetCurrentDataSource();

			var propertiesInItems = GetProperties(itemsToInsert[0]);
			if (columnMapping == null)
			{
				var sqlSelectColumns = await moduleSettings.FormatSelectColumnsStatement(tableName);
				var result = await Select(sqlSelectColumns);
				if (result.error != null)
				{
					return (0, result.error);
				}
				var columnsInTable = result.rows as List<dynamic>;
				if (columnsInTable == null)
				{
					return (0, new Error($"Table {tableName} could not be found", "TableNotFound"));
				}

				columnMapping = new();
				foreach (var column in columnsInTable)
				{
					if (propertiesInItems.FirstOrDefault(p => p.Equals(column.name, StringComparison.OrdinalIgnoreCase)) != null)
					{
						columnMapping.Add(column.name, column.name);
					}
				}
			}

			string? sql = GetBulkSql(tableName, columnMapping, itemsToInsert, ignoreContraintOnInsert, dataSource);
			if (sql == null) return (0, null);

			long affectedRows = 0;
			var generator = new IdGenerator(1);
			var id = generator.ElementAt(0);
			IDbTransaction? transaction = context.ContainsKey(DbTransactionContextKey) ? context[DbTransactionContextKey] as IDbTransaction : null;
			if (transaction == null)
			{
				await BeginTransaction();
			}

			// TODO: This is actually not the most optimized bulk insert, it's inserting each row at a time
			for (int i = 0; i < itemsToInsert.Count; i++)
			{
				var param = new List<object>();
				bool rowHasAnyValue = false;
				foreach (var column in columnMapping)
				{
					string cleanedColumnValue = column.Value.ToString().Replace("%", "").Replace("item.", "");
					bool isListItem = column.Value.ToString().Contains("item.");
					if (column.Key == "id")
					{
						param.Add(new ParameterInfo("id", id + i, typeof(Int64).FullName));
					}
					else if (propertiesInItems.FirstOrDefault(p => p.Equals(cleanedColumnValue, StringComparison.OrdinalIgnoreCase)) != null
						|| memoryStack.Contains(cleanedColumnValue))
					{
						var obj = (isListItem) ? GetValue(itemsToInsert[i], cleanedColumnValue) : memoryStack.Get(cleanedColumnValue);
						if (obj == null)
						{
							param.Add(new ParameterInfo(column.Key, obj, typeof(DBNull).FullName));
							continue;
						}
						else if (obj is ObjectValue ov)
						{
							param.Add(new ParameterInfo(column.Key, ov.Value, ov.Type.FullName));
						}
						else if (obj is JValue value)
						{
							param.Add(new ParameterInfo(column.Key, value.Value, value.Value.GetType().FullName));
						}
						else
						{
							param.Add(new ParameterInfo(column.Key, obj, obj.GetType().FullName));
						}
						rowHasAnyValue = true;
					}

				}
				if (!rowHasAnyValue) { continue; }

				var insertResult = await Insert(sql, param);
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

		private string ConvertFromColumnTypeToCSharpType(string type)
		{
			if (type == "TEXT" || type.Equals("string", StringComparison.OrdinalIgnoreCase)) return typeof(String).FullName;
			if (type == "INTEGER" || type.Equals("int", StringComparison.OrdinalIgnoreCase)) return typeof(long).FullName;
			if (type == "REAL") return typeof(double).FullName;
			if (type == "BLOB") return typeof(byte[]).FullName;
			if (type == "NUMERIC") return typeof(double).FullName;
			if (type == "BOOLEAN" || type.Equals("bool", StringComparison.OrdinalIgnoreCase)) return typeof(bool).FullName;
			if (type == "NULL") return typeof(DBNull).FullName;
			if (type == "BIGINT" || type.Equals("int64", StringComparison.OrdinalIgnoreCase)) return typeof(Int64).FullName;

			throw new Exception($"Could not map type: {type} to C# object");
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
				} else
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
			var dataSource = await moduleSettings.GetCurrentDataSource();

			List<object> parameters = new List<object>();
			parameters.Add(new ParameterInfo("Database", dataSource.DbName, "System.String"));

			(var connection, var par, _, var error) = Prepare("", parameters);
			if (error != null)
			{
				return (string.Empty, error);
			}
			var result = await connection.QueryAsync(dataSource.SelectTablesAndViews, par);


			return (@$"## tables in database ##
{JsonConvert.SerializeObject(result)}
## tables in database ##
", null);
		}



	}
}
