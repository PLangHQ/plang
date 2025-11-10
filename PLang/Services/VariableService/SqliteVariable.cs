
using AngleSharp.Dom.Events;
using IdGen;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using static PLang.Utils.VariableHelper;

namespace PLang.Services.VariableService
{
	public class SqliteVariable
	{
	}

	public enum EventType
	{
		BeforeCreate, AfterCreate,
		BeforeUpdate, AfterUpdate,
		BeforeDelete, AfterDelete
	}

	[Description("When EventType is not defined by user intent, assume he wants the after event")]
	public class Event
	{
		public EventType Type { get; set; }
		public GoalToCallInfo? GoalToCall { get; set; }
	}

	public abstract class Item
	{
		public object Id { get; set; }
		public DateTime Created { get; set; }
		public DateTime Updated { get; set; }
		public DateTime? Expires { get; set; }

		public List<Event>? OnEvents { get; set; }

		[LlmIgnore]
		public SignedMessage? Signature { get; set; }

	}

	public class DatabaseSaver
	{
		private readonly IEngine engine;
		private readonly PLangContext context;
		private string connectionString = "Data Source=system.sqlite;Version=3;";
		private GoalStep step;
		public DatabaseSaver(IEngine engine, PLangContext context, string connectionString)
		{
			this.engine = engine;
			this.context = context;
		}

		public void Init(string connectionString, GoalStep step)
		{
			this.step = step;
			this.connectionString = connectionString;
		}
		public async Task<(List<ObjectValue>?, IError?)> Save<T>(T data) where T : Item
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			if (data.Id is not null && data.Id is not long)
			{
				throw new Exception($"Id not correct type {data.Id.GetType()}. Must be long");
			}

			Type type = typeof(T);
			string tableName = type.Name;
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			// Create table if not exists
			CreateTableIfNotExists(tableName, properties);
			List<ObjectValue> objectValues = new();
			var fixParameters = new Dictionary<string, object?>();
			fixParameters.Add("tableName", tableName);
			fixParameters.Add("data", data);
			fixParameters.Add("properties", properties);

			GoalToCallInfo goalToCall;
			string sql;
			List<ObjectValue> sqlParameters;
			if (data.Id == null)
			{
				var generator = new IdGenerator(4);
				data.Id = generator.CreateId();

				goalToCall = new GoalToCallInfo("/services/VariableService/InsertVariable")
				{
					Path = "/.build/services/VariableService/InsertVariable.goal",
					Parameters = fixParameters
				};

				(sql, sqlParameters) = InsertData(tableName, data, properties, fixParameters);
		
			}
			else
			{
				goalToCall = new GoalToCallInfo("/services/VariableService/UpdateVariable")
				{
					Path = "/.build/services/VariableService/UpdateVariable.goal",
					Parameters = fixParameters
				};

				// Update existing record
				(sql, sqlParameters) = UpdateData(tableName, data, properties);
			}

			goalToCall.Parameters.Add("sql", sql);
			goalToCall.Parameters.Add("sqlParameters", sqlParameters);

			var currentDataSource = context.DataSource;
			context.DataSource = null;
			var result = await engine.RunGoal(goalToCall, context.CallingStep.Goal, context);
			context.DataSource = currentDataSource;

			var objectValue = AppendVariables(result.Variables, step.Instruction.Function.ReturnValues);

			return (objectValue, result.Error);
		}

		private List<ObjectValue> AppendVariables(object? variables, List<Modules.BaseBuilder.ReturnValue>? varibleNames)
		{
			List<ObjectValue> objectValues = new();
			if (variables is List<ObjectValue> li)
			{
				objectValues.AddRange(li);
			}
			else if (variables is ObjectValue ov)
			{
				objectValues.Add(ov);
			}
			else
			{
				var type = TypeHelper.GetType(varibleNames[0].Type);
				objectValues.Add(new ObjectValue(varibleNames[0].VariableName, variables, type));
			}
			return objectValues;
		}

		private void CreateTableIfNotExists(string tableName, PropertyInfo[] properties)
		{
			using (SqliteConnection connection = new SqliteConnection(connectionString))
			{
				connection.Open();

				string createTableSql = GenerateCreateTableSql(tableName, properties);

				using (SqliteCommand command = new SqliteCommand(createTableSql, connection))
				{
					command.ExecuteNonQuery();
				}

				connection.Close();
			}
		}

		private string GenerateCreateTableSql(string tableName, PropertyInfo[] properties)
		{
			StringBuilder sql = new StringBuilder($"CREATE TABLE IF NOT EXISTS {tableName} (");

			List<string> columnDefinitions = new List<string>();

			for (int i = 0; i < properties.Length; i++)
			{
				PropertyInfo prop = properties[i];
				string columnName = prop.Name;
				string sqliteDataType = GetSqliteDataType(prop.PropertyType);

				if (columnName == "Id")
				{
					columnDefinitions.Add($"{columnName} {sqliteDataType} PRIMARY KEY");
				}
				else
				{
					columnDefinitions.Add($"{columnName} {sqliteDataType}");
				}
			}

			sql.Append(string.Join(", ", columnDefinitions));
			sql.Append(");");

			return sql.ToString();
		}

		private string GetSqliteDataType(Type type)
		{
			if (type == typeof(int) || type == typeof(int?))
				return "INTEGER";
			if (type == typeof(long) || type == typeof(long?))
				return "INTEGER";
			if (type == typeof(short) || type == typeof(short?))
				return "INTEGER";
			if (type == typeof(byte) || type == typeof(byte?))
				return "INTEGER";
			if (type == typeof(float) || type == typeof(float?) ||
				type == typeof(double) || type == typeof(double?) ||
				type == typeof(decimal) || type == typeof(decimal?))
				return "REAL";
			if (type == typeof(bool) || type == typeof(bool?))
				return "INTEGER";
			if (type == typeof(DateTime) || type == typeof(DateTime?))
				return "TEXT";
			if (type == typeof(byte[]))
				return "BLOB";

			return "TEXT";
		}

		private (string Sql, List<ObjectValue> Parameters) InsertData<T>(string tableName, T data, PropertyInfo[] properties, Dictionary<string, object?> fixParameters) where T : Item
		{


			List<string> columnNames = new List<string>();
			List<string> parameterNames = new List<string>();
			List<ObjectValue> parameters = new();


			for (int i = 0; i < properties.Length; i++)
			{
				PropertyInfo prop = properties[i];
				object value = properties[i].GetValue(data) ?? DBNull.Value;

				parameters.Add(new ObjectValue(prop.Name, value));
				columnNames.Add(prop.Name);
				parameterNames.Add($"@{prop.Name}");
			}

			string insertSql = $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", parameterNames)});";


			return (insertSql, parameters);

		}

		private (string Sql, List<ObjectValue> Parameters) UpdateData<T>(string tableName, T data, PropertyInfo[] properties) where T : Item
		{

			List<string> setClause = new List<string>();
			List<ObjectValue> parameters = new();

			for (int i = 0; i < properties.Length; i++)
			{
				PropertyInfo prop = properties[i];
				if (prop.Name.Equals("Created", StringComparison.OrdinalIgnoreCase)) continue;

				object value = properties[i].GetValue(data) ?? DBNull.Value;

				if (!prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
				{
					setClause.Add($"{prop.Name} = @{prop.Name}");					
				}
				
				parameters.Add(new ObjectValue(prop.Name, value));
			}

			string updateSql = $"UPDATE {tableName} SET {string.Join(", ", setClause)} WHERE Id = @Id;";

			return (updateSql, parameters);
		}
	}
}
