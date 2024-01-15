using Dapper;
using IdGen;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Interfaces;
using PLang.Services.EventSourceService;
using PLang.Utils;
using System.ComponentModel;
using System.Data;
using System.Globalization;
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
		private readonly ISettings settings;
		private readonly IEventSourceRepository eventSourceRepository;
		private readonly PLangAppContext context;

		public Program(IDbConnection dbConnection, IPLangFileSystem fileSystem, ISettings settings, ILlmService aiService, IEventSourceRepository eventSourceRepository, PLangAppContext context, ILogger logger) : base()
		{
			this.dbConnection = dbConnection;
			this.settings = settings;
			this.eventSourceRepository = eventSourceRepository;
			this.context = context;

			this.moduleSettings = new ModuleSettings(fileSystem, settings, context, aiService, dbConnection, logger);
		}

		public async Task CreateDataSource(string name)
		{
			await moduleSettings.CreateDataSource(name);
		}

		public async Task SetDataSouceName(string name)
		{
			var dataSource = await moduleSettings.GetDataSource(name);
			if (dataSource == null)
			{
				throw new ArgumentException($"Datasource with the name '{name}' could not be found");
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

		public async void Dispose()
		{
			await EndTransaction();
		}

		public record ParameterInfo(string ParameterName, object VariableNameOrValue, string TypeFullName);

		private (IDbConnection connection, DynamicParameters param) Prepare(List<object>? Parameters = null, bool isInsert = false)
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
					} else if (p.VariableNameOrValue == null)
					{
						param.Add("@" + p.ParameterName.Replace("@", ""), null);
					}
					else
					{
						object value = ConvertObjectToType(p.VariableNameOrValue, p.TypeFullName); 
						param.Add("@" + p.ParameterName.Replace("@", ""), value);
					}
				}
			}
			if (connection.State != ConnectionState.Open) connection.Open();
			return (connection, param);

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
				var prepare = Prepare(null);
				if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
				{
					rowsAffected = await eventSourceRepository.Add(prepare.connection, sql, null);
				}
				else
				{
					rowsAffected = await prepare.connection.ExecuteAsync(sql, prepare.param);
				}

				Done(prepare.connection);
				return rowsAffected;
			}
			catch (Exception ex)
			{
				if (ex.ToString().Contains("relation") && ex.ToString().Contains("already exists"))
				{
					return 0;
				}
				throw;

			}
		}

		public async Task CreateTable(string sql)
		{
			try
			{
				await Execute(sql);
			}
			catch (Exception ex)
			{
				if (ex.ToString().Contains("relation") && ex.ToString().Contains("already exists"))
				{
					return;
				}

			}
		}

		public async Task<dynamic?> Select(string sql, List<object>? Parameters = null, bool selectOneRow_Top1OrLimit1 = false)
		{
			var prep = Prepare(Parameters);
			var rows = (await prep.connection.QueryAsync<dynamic>(sql, prep.param)).ToList();
			Done(prep.connection);

			if (rows.Count == 0) return null;
			if (!selectOneRow_Top1OrLimit1 || rows.Count != 1) return rows;

			var rowsAsList = ((IList<object>)rows);
			var columns = ((IDictionary<string, object>)rowsAsList[0]);

			if (this.function == null || this.function.ReturnValue == null) return null;

			if (columns.Count == 1)
			{
				return columns.FirstOrDefault().Value;
			}

			return (selectOneRow_Top1OrLimit1) ? rows[0] : rows;

		}

		public async Task<int> Update(string sql, List<object>? Parameters = null)
		{
			var prepare = Prepare(Parameters);
			int result;
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{
				result = await eventSourceRepository.Add(prepare.connection, sql, prepare.param);
			}
			else
			{
				result = await prepare.connection.ExecuteAsync(sql, prepare.param);
			}
			Done(prepare.connection);
			return result;
		}

		public async Task<int> Delete(string sql, List<object>? Parameters = null)
		{
			int rowsAffected;
			var prepare = Prepare(Parameters);
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{
				rowsAffected = await eventSourceRepository.Add(prepare.connection, sql, prepare.param);
			}
			else
			{
				rowsAffected = await prepare.connection.ExecuteAsync(sql, prepare.param);
			}
			Done(prepare.connection);
			return rowsAffected;
		}

		[Description("Basic insert statement. Will return affected row count")]
		public async Task<int> Insert(string sql, List<object>? Parameters = null)
		{

			int rowsAffected;
			var prepare = Prepare(Parameters, true);
			if (eventSourceRepository.GetType() != typeof(DisableEventSourceRepository))
			{


				rowsAffected = await eventSourceRepository.Add(prepare.connection, sql, prepare.param);
			}
			else
			{
				rowsAffected = await prepare.connection.ExecuteAsync(sql, prepare.param);
			}
			Done(prepare.connection);
			return rowsAffected;

		}
		[Description("Insert statement that will return the id of the inserted row. Use only if user requests the id")]
		public async Task<object> InsertAndSelectIdOfInsertedRow(string sql, List<object>? Parameters = null)
		{
			var prepare = Prepare(Parameters, true);

			if (eventSourceRepository.GetType() == typeof(DisableEventSourceRepository))
			{
				var value = await prepare.connection.QuerySingleOrDefaultAsync(sql, prepare.param) as IDictionary<string, object>;
				Done(prepare.connection);
				return value.FirstOrDefault().Value;
			}
			else
			{
				await eventSourceRepository.Add(prepare.connection, sql, prepare.param);
				Done(prepare.connection);

				if (prepare.param.ParameterNames.Contains("id"))
				{
					return prepare.param.Get<long>("id");
				}

				return null;
			}

		}

		public async override Task<string> GetAdditionalSystemErrorInfo()
		{
			return "You will be provided with tables that already exists in the database";
		}

		public async override Task<string> GetAdditionalAssistantErrorInfo()
		{
			var dataSource = await moduleSettings.GetCurrentDatasource();

			List<object> parameters = new List<object>();
			parameters.Add(new ParameterInfo("Database", dataSource.DbName, "System.String"));

			(var connection, var par) = Prepare(parameters);

			var result = await connection.QueryAsync(dataSource.SelectTablesAndViews, par);


			return @$"## tables in database ##
{JsonConvert.SerializeObject(result)}
## tables in database ##
";
		}



	}
}
