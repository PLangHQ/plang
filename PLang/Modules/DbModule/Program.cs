using Dapper;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Nethereum.ABI.CompilationMetadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI_API.Moderation;
using OpenQA.Selenium.DevTools.V119.FedCm;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Utils;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text;
using static Dapper.SqlMapper;

namespace PLang.Modules.DbModule
{
	[Description("Database access, select, insert, update, delete and execute raw sql. Handles transactions")]
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

		public Program(IDbConnection dbConnection, IPLangFileSystem fileSystem, ISettings settings, ILlmServiceFactory llmServiceFactory, IEventSourceRepository eventSourceRepository, PLangAppContext context, ILogger logger) : base()
		{
			this.dbConnection = dbConnection;
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
			this.eventSourceRepository = eventSourceRepository;
			this.logger = logger;
			this.context = context;

			this.moduleSettings = new ModuleSettings(fileSystem, settings, context, llmServiceFactory, dbConnection, logger);
		}

		public async Task CreateDataSource(string name, string dbType = "", bool setAsDefaultForApp = false, bool keepHistoryEventSourcing = false)
		{
			await moduleSettings.CreateDataSource(name, dbType, setAsDefaultForApp, keepHistoryEventSourcing);
		}

		public async Task SetDataSouceName(string name)
		{
			var dataSource = await moduleSettings.GetDataSource(name);
			if (dataSource == null)
			{
				throw new ArgumentException($"Datasource with the name '{name}' could not be found. You need to create a datasource first, e.g. \n\n- Create data source {name}\n- Create postgres data source {name}, set as default\n- Create sqlserver data source {name}, keep history");
			}
			context[ReservedKeywords.CurrentDataSourceName] = dataSource;
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

		public async Task LoadExtension(string fileName, string? procName = null)
		{
			fileName = GetPath(fileName);
			if (!fileSystem.File.Exists(fileName))
			{
				throw new RuntimeException("File could not be found.");
			}

			if (dbConnection is SqliteConnection)
			{
				((SqliteConnection)dbConnection).LoadExtension(fileName, procName); return;
			} else
			{
				logger.LogWarning("Loading extension only works for Sqlite");
			}
		}

		public async void Dispose()
		{
			await EndTransaction();
		}

		public record ParameterInfo(string ParameterName, object VariableNameOrValue, string TypeFullName);

		private (IDbConnection connection, DynamicParameters param, string sql) Prepare(string sql, List<object>? Parameters = null, bool isInsert = false)
		{
			IDbConnection connection = context.ContainsKey(DbConnectionContextKey) ? context[DbConnectionContextKey] as IDbConnection : dbConnection;

			var param = new DynamicParameters();
			if (Parameters != null)
			{
				foreach (var parameter in Parameters)
				{
					var p = parameter as ParameterInfo;
					if (parameter is JObject)
					{
						p = ((JObject)parameter).ToObject<ParameterInfo>();
					}
					else if (parameter is string && JsonHelper.IsJson(parameter))
					{
						p = JsonConvert.DeserializeObject<ParameterInfo>(parameter.ToString());
					}

					if (isInsert && (p.ParameterName == "id" || p.ParameterName == "@id") && eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
					{
						var generator = new IdGenerator(1);
						param.Add("@" + p.ParameterName.Replace("@", ""), generator.ElementAt(0), DbType.Int64);
					}
					else if (p.VariableNameOrValue == null)
					{
						param.Add("@" + p.ParameterName.Replace("@", ""), null);
					} else if (p.VariableNameOrValue is JArray)
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
							param.Add($"@category{i}", ConvertObjectToType(jarray[i], p.TypeFullName));
						}
						sql = sql.Replace(p.ParameterName.ToString(), placeholders.ToString());

					}
					else
					{
						object value = ConvertObjectToType(p.VariableNameOrValue, p.TypeFullName);
						param.Add("@" + p.ParameterName.Replace("@", ""), value);
					}
				}
			}
			if (connection.State != ConnectionState.Open) connection.Open();
			return (connection, param, sql);

		}

		private object ConvertObjectToType(object obj, string typeFullName)
		{
			Type targetType = Type.GetType(typeFullName);
			if (targetType == null)
			{
				throw new TypeLoadException($"Could not find {typeFullName}");
			}

			var parseMethod = targetType.GetMethod("Parse", new[] { typeof(string) });
			if (parseMethod != null)
			{
				try
				{
					string value = FormatType(obj.ToString(), targetType);
					return parseMethod.Invoke(null, new object[] { value });
				}
				catch { }
			}
			
			return Convert.ChangeType(obj, targetType);


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


		public async Task<int> Execute(string sql)
		{
			try
			{
				int rowsAffected = 0;
				var prepare = Prepare(sql, null);
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
				return rowsAffected;
			}
			catch (Exception ex)
			{
				if (GoalHelper.IsSetup(goalStep))
				{
					if (ex.ToString().Contains("already exists") || ex.ToString().Contains("duplicate column name"))
					{
						ShowWarning(ex);
						return 1;
					}
				}
				throw;

			}
		}

		private void ShowWarning(Exception ex)
		{
			logger.LogWarning($"Had error running Setup ({goalStep.Text}) but will continue. Error message:{ex.Message}");
		}

		public async Task CreateTable(string sql)
		{
			try
			{
				await Execute(sql);
			}
			catch (Exception ex)
			{
				if (GoalHelper.IsSetup(goalStep) && ex.ToString().Contains("relation") && ex.ToString().Contains("already exists"))
				{
					ShowWarning(ex);
					return;
				}

			}
		}

		public async Task<object?> Select(string sql, List<object>? SqlParameters = null, bool selectOneRow_Top1OrLimit1 = false)
		{
			var prep = Prepare(sql, SqlParameters);
			var rows = (await prep.connection.QueryAsync<dynamic>(prep.sql, prep.param)).ToList();
			Done(prep.connection);

			if (rows.Count == 0 && this.function != null)
			{
				if (this.function.ReturnValue != null)
				{
					if (this.function.ReturnValue.Count == 1) return rows;

					var dict = new ReturnDictionary<string, object?>();
					foreach (var rv in this.function.ReturnValue)
					{						
						dict.Add(rv.VariableName, GetDefaultValue(rv.Type));
					}
					return dict;					
				}
				return new List<object>();
			}
			if (!selectOneRow_Top1OrLimit1 && rows.Count != 1) return rows;

			var rowsAsList = ((IList<object>)rows);
			var columns = ((IDictionary<string, object>)rowsAsList[0]);

			if (this.function == null || this.function.ReturnValue == null) return new List<object>();

			if (columns.Count == 1)
			{
				return columns.FirstOrDefault().Value;
			}

			return (selectOneRow_Top1OrLimit1) ? rows[0] : rows;

		}

		private object? GetDefaultValue(string strType)
		{
			if (strType == "dynamic") return new List<object>();
			if (strType == "string") return null;
			
			var type = Type.GetType(strType);
			if (type == null) return null;

			return type.IsValueType && !type.IsPrimitive ? Activator.CreateInstance(type) : null;
		}

		public async Task<int> Update(string sql, List<object>? SqlParameters = null)
		{
			var prepare = Prepare(sql, SqlParameters);
			int result;
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{
				result = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param);
			}
			else
			{
				result = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param);
			}
			Done(prepare.connection);
			return result;
		}

		public async Task<int> Delete(string sql, List<object>? SqlParameters = null)
		{
			int rowsAffected;
			var prepare = Prepare(sql, SqlParameters);
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{
				rowsAffected = await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param);
			}
			else
			{
				rowsAffected = await prepare.connection.ExecuteAsync(prepare.sql, prepare.param);
			}
			Done(prepare.connection);
			return rowsAffected;
		}

		[Description("Basic insert statement. Will return affected row count")]
		public async Task<int> Insert(string sql, List<object>? SqlParameters = null)
		{

			int rowsAffected = 0;
			var prepare = Prepare(sql, SqlParameters, true);
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
					return rowsAffected;
				}
				throw;
			}
			finally
			{
				Done(prepare.connection);
			}
			return rowsAffected;

		}
		[Description("Insert statement that will return the id of the inserted row. Use only if user requests the id")]
		public async Task<object> InsertAndSelectIdOfInsertedRow(string sql, List<object>? SqlParameters = null)
		{
			var prepare = Prepare(sql, SqlParameters, true);

			if (eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
			{
				var value = await prepare.connection.QuerySingleOrDefaultAsync(prepare.sql, prepare.param) as IDictionary<string, object>;
				Done(prepare.connection);
				return value.FirstOrDefault().Value;
			}
			else
			{
				await eventSourceRepository.Add(prepare.connection, prepare.sql, prepare.param);
				Done(prepare.connection);

				if (prepare.param.ParameterNames.Contains("id"))
				{
					return prepare.param.Get<long>("id");
				}

				return null;
			}

		}

		[Description("Insert a list(bulk) into database, return number of rows inserted")]
		public async Task<int> InsertBulk(string tableName, List<object> items)
		{
			var dataSource = await moduleSettings.GetCurrentDataSource();
			var sqlSelectColumns = await moduleSettings.FormatSelectColumnsStatement(tableName);
			var columnsInTable = await Select(sqlSelectColumns) as List<dynamic>;
			if (columnsInTable == null)
			{
				throw new RuntimeException($"Table {tableName} could not be found");
			}

			string? sql = GetBulkSql(tableName, columnsInTable, items);
			if (sql == null) return 0;

			var param = new List<object>();

			int affectedRows = 0;
			var generator = new IdGenerator(items.Count);
			await BeginTransaction();

			for (int i = 0; i < items.Count; i++)
			{
				var row = (JObject)items[i];

				foreach (var column in columnsInTable)
				{
					if (column.name == "id")
					{
						param.Add(new ParameterInfo("id", generator.ElementAt(i), typeof(Int64).FullName));
					}
					else if (row.ContainsKey(column.name))
					{
						var obj = row[column.name];
						if (obj is ObjectValue ov)
						{
							param.Add(new ParameterInfo(column.name, ov.Value, ov.Type.FullName));
						}
						else
						{
							param.Add(new ParameterInfo(column.name, obj, obj.GetType().FullName));
						}
					}

				}
				affectedRows += await Insert(sql, param);
			}

			await EndTransaction();

			return affectedRows;

		}

		private string? GetBulkSql(string tableName, List<dynamic> columnsInTable, List<object> items)
		{
			if (items.Count == 0) return null;


			var row = (JObject)items[0];
			string? columns = null;
			string? values = null;
			foreach (var column in columnsInTable)
			{
				if (row.ContainsKey(column.name))
				{
					if (columns != null) columns += ", ";
					columns += column.name;

					if (values != null) values += ", ";
					values += $"@{column.name}";
				}
			}

			return $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
		}

		public async override Task<string> GetAdditionalSystemErrorInfo()
		{
			return "You will be provided with tables that already exists in the database";
		}

		public async override Task<string> GetAdditionalAssistantErrorInfo()
		{
			var dataSource = await moduleSettings.GetCurrentDataSource();

			List<object> parameters = new List<object>();
			parameters.Add(new ParameterInfo("Database", dataSource.DbName, "System.String"));

			(var connection, var par, _) = Prepare("", parameters);

			var result = await connection.QueryAsync(dataSource.SelectTablesAndViews, par);


			return @$"## tables in database ##
{JsonConvert.SerializeObject(result)}
## tables in database ##
";
		}



	}
}
