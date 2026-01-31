using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nostr.Client.Json;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using static PLang.Modules.ConditionalModule.ConditionEvaluator;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;

namespace PLang.Modules.VariableModule
{
	[Description("Set, Get & Return variable(s). Set(NOT if statement) on variable includes condition such as empty or null. Bind onCreate, onChange, onRemove events to variable. Trim variable for llm. Use this module on `- return %variable`. Modify datetime (%now%, etc.)")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;
		private new readonly VariableHelper variableHelper;

		public Program(ISettings settings, VariableHelper variableHelper) : base()
		{
			this.settings = settings;
			this.variableHelper = variableHelper;
		}

		private async Task<(DataSource?, IError?)> GetDataSource(string? dataSourceName)
		{
			var (db, dbError) = Module<DbModule.Program>();
			if (dbError != null) return (null, dbError);
			return await db!.GetDataSource(dataSourceName);

		}
		[Example("load %step%", @"variables=[""%step%""]")]
		public async Task<(object?, IError?)> Load([HandlesVariable] List<string> variables, string? dataSourceName = null)
		{
			Dictionary<string, object?> varsWithValue = new();
			foreach (var variable in variables)
			{
				varsWithValue.Add(variable, null);
			}

			var (dataSource, error) = await GetDataSource(dataSourceName);
			if (error != null) return (null, error);

			return await LoadWithDefaultValueInternal(varsWithValue, dataSource);
		}

		[Description(@"Loads(not setting a variable) a variable with a default value, key: variable name, value: default value. example: load %age%, default 10 => key:%age% value:10")]
		[Example("load %step%, set as 1 if empty", @"variablesWithDefaultValue=[""%step%"", 1]")]
		public async Task<(object?, IError?)> LoadWithDefaultValue([HandlesVariable] Dictionary<string, object?> variablesWithDefaultValue, string? dataSourceName = null)
		{
			var (dataSource, error) = await GetDataSource(dataSourceName);
			if (error != null) return (null, error);

			return await LoadWithDefaultValueInternal(variablesWithDefaultValue, dataSource);
		}

		private async Task<(object?, IError?)> LoadWithDefaultValueInternal([HandlesVariable] Dictionary<string, object?> variablesWithDefaultValue, DataSource dataSource)
		{
			var db = Module<DbModule.Program>().Module!;


			List<object?> objects = new();
			foreach (var variable in variablesWithDefaultValue)
			{
				List<ParameterInfo> parameters = new();
				parameters.Add(new ParameterInfo("System.String", "variable", variable.Key));
				try
				{
					var result = await db.Select(dataSource, "SELECT * FROM __Variables__ WHERE variable=@variable", parameters);
					if (result.Error != null && result.Error.Message.Contains("no such table"))
					{
						var createTableResult = await CreateVariablesTable(db, dataSource);
						if (createTableResult.Error != null) return (null, createTableResult.Error);
						return await LoadWithDefaultValue(variablesWithDefaultValue);
					}


					object defaultValue = variable.Value;
					if (VariableHelper.IsVariable(variable.Value))
					{
						defaultValue = memoryStack.Get(variable.Value.ToString());
					}

					if (result.Table == null || result.Table.Count == 0)
					{


						objects.Add(new ObjectValue(variable.Key, defaultValue));
						continue;
					}

					var row = (Row)result.Table[0];
					if (row == null)
					{
						objects.Add(new ObjectValue(variable.Key, defaultValue));
						continue;
					}

					var json = row["value"]?.ToString();
					if (json == null)
					{
						objects.Add(new ObjectValue(variable.Key, defaultValue));
						continue;
					}

					var value = JsonConvert.DeserializeObject(json);

					if (function?.ReturnValues == null || function?.ReturnValues?.Count == 0)
					{
						objects.Add(value);
						memoryStack.Put(variable.Key, value, goalStep: goalStep);
					}
					else
					{
						objects.Add(value);
					}
				}
				catch (Exception ex)
				{
					if (ex.Message.Contains("no such table"))
					{
						return (null, null);
					}
					throw;
				}

			}
			if (objects.Count == 1)
			{
				return (objects[0], null);
			}
			if (objects.Count == 0) return (null, null);

			return (objects, null);
		}

		private async Task<(long, IError? Error)> CreateVariablesTable(DbModule.Program db, DataSource dataSource)
		{
			return await db.Execute(dataSource, @"CREATE TABLE __Variables__ (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    variable TEXT NOT NULL UNIQUE,
    value TEXT,
    created DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated DATETIME DEFAULT CURRENT_TIMESTAMP,
    expires DATETIME
);", ["__Variables__"]);

		}

		[Description("Set value to a variable then Store/Save variable(s) in a persistant storage. The storing of the variables must be defined by user.")]
		public async Task<IError?> SetValueAndStore([HandlesVariable] Dictionary<string, object> variables, string? dataSourceName = null)
		{
			var (dataSource, error) = await GetDataSource(dataSourceName);
			if (error != null) return error;

			List<string> vars = new();
			foreach (var variable in variables)
			{
				object value = variable.Value;
				if (VariableHelper.IsVariable(variable.Value))
				{
					value = memoryStack.Get(variable.Value.ToString());
				}
				else if (VariableHelper.ContainsVariable(variable.Value))
				{
					value = memoryStack.LoadVariables(variable.Value);
				}

				memoryStack.Put(variable.Key, value);
				vars.Add(variable.Key);
			}
			return await Store(vars, dataSource!);
		}
		[Description("Store/Save variable(s) in a persistant storage")]
		public async Task<IError?> Store([HandlesVariable] List<string> variables, string? dataSourceName = null)
		{
			var (dataSource, error) = await GetDataSource(dataSourceName);
			if (error != null) return error;

			return await Store(variables, dataSource!);
		}


		internal async Task<IError?> Store([HandlesVariable] List<string> variables, DataSource dataSource)
		{
			if (dataSource!.TypeFullName != typeof(SqliteConnection).FullName)
			{
				return new ProgramError("Only sqlite is supported");
			}

			var db = Module<DbModule.Program>().Module!;
			foreach (var variable in variables)
			{
				var value = memoryStack.Get(variable);
				if (value == null) continue;

				List<ParameterInfo> parameters = new();
				parameters.Add(new ParameterInfo("System.String", "variable", variable));
				parameters.Add(new ParameterInfo("System.String", "value", JsonConvert.SerializeObject(value)));

				var (rowsAffected, error) = await db.Insert(dataSource, "INSERT INTO __Variables__ (variable, value) VALUES (@variable, @value) ON CONFLICT(variable) DO UPDATE SET value = excluded.value;", parameters);

				if (error != null)
				{
					if (error.Message.Contains("no such table"))
					{
						var createTableResult = await CreateVariablesTable(db, dataSource);
						if (createTableResult.Error != null) return createTableResult.Error;

						return await Store(variables, dataSource);
					}
					return error;
				}

				if (rowsAffected == 0) return new ProgramError($"Variable was not updated. It should always be updated. Something is wrong. {ErrorReporting.CreateIssueShouldNotHappen}", goalStep);
			}
			return null;
		}

		[Description("One or more variables to return. Variable can contain !, e.g. !callback=%callback%. When key is undefined, it is same as value, e.g. return %name% => then variables dictionary has key and value as name=%name%")]
		public async Task<IError?> Return([HandlesVariable] Dictionary<string, object> variables)
		{
			if (variables == null) return new EndGoal(false, goal, goalStep, "", Levels: 0);

			var returnValues = new List<ObjectValue>();
			var properties = new Properties();

			var propertyVars = variables.Where(p => p.Key.StartsWith("!"));
			foreach (var property in propertyVars)
			{
				var value = memoryStack.LoadVariables(property.Value);
				if (value != null)
				{
					var ov = new ObjectValue(property.Key, value, value.GetType());
					properties.Add(ov);
				}
			}

			var vars = variables.Where(p => !p.Key.StartsWith("!"));
			foreach (var variable in vars)
			{
				if (variable.Value is string str && VariableHelper.IsVariable(str))
				{
					var objectValue = memoryStack.GetObjectValue(str);
					if (objectValue != null && !objectValue.Initiated) return new ProgramError($"Variable '{str}' does not exist. Is there a typo?");

					if (objectValue == null) objectValue = ObjectValue.Nullable(variable.Key);
					objectValue.Properties.AddRange(properties);

					returnValues.Add(objectValue);

				}
				else
				{
					var value = memoryStack.LoadVariables(variable.Value);
					if (value != null)
					{
						returnValues.Add(new ObjectValue(variable.Key, value, properties: properties));
					}
				}
			}


			return new Return(returnValues);
		}


		public async Task OnCreateVariablesListener([HandlesVariable] List<string> keys, [HandlesVariable] GoalToCallInfo goalName, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			foreach (var key in keys)
			{
				memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, false, waitForResponse, delayWhenNotWaitingInMilliseconds);
			}
		}

		public async Task OnChangeVariablesListener([HandlesVariable] List<string> keys, [HandlesVariable] GoalToCallInfo goalName, bool notifyWhenCreated = true, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			foreach (var key in keys)
			{
				memoryStack.AddOnChangeEvent(key, goalName, goal.Hash, false, notifyWhenCreated, waitForResponse, delayWhenNotWaitingInMilliseconds);
				if (notifyWhenCreated)
				{
					memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, false, waitForResponse, delayWhenNotWaitingInMilliseconds);

				}
			}
		}


		public async Task OnRemoveVariablesListener([HandlesVariable] List<string> keys, [HandlesVariable] GoalToCallInfo goalName, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			foreach (var key in keys)
			{
				memoryStack.AddOnRemoveEvent(key, goalName, goal.Hash, false, waitForResponse, delayWhenNotWaitingInMilliseconds);
			}
		}

		public async Task<string?> GetEnvironmentVariable(string key)
		{
			return Environment.GetEnvironmentVariable(key);
		}
		public async Task<object?> LoadVariables([HandlesVariable] string key)
		{
			var content = memoryStack.Get(key);
			if (content == null) return null;

			return memoryStack.LoadVariables(content);
		}



		[Description(@"Set string variable. Developer might use single/double quote to indicate the string value, the wrapped quote should not be included in the value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetStringVariable([HandlesVariable] string key, [HandlesVariable] string? value = null, bool urlDecode = false, bool htmlDecode = false, bool doNotLoadVariablesInValue = false, [HandlesVariable] string? defaultValue = null)
		{
			if (value == null) value = defaultValue;

			if (urlDecode) value = HttpUtility.UrlDecode(value);
			if (htmlDecode) value = HttpUtility.HtmlDecode(value);

			object? content = (doNotLoadVariablesInValue) ? value : memoryStack.LoadVariables(value);
			memoryStack.Put(key, content, goalStep: goalStep);
		}

		[Description(@"Set json variable. Make sure value is valid json")]
		public async Task SetJsonObjectVariable([HandlesVariable] string key, [HandlesVariable] object? value = null, bool doNotLoadVariablesInValue = false, [HandlesVariable] object? defaultValue = null)
		{
			if (value == null) value = defaultValue;

			object? content = (doNotLoadVariablesInValue) ? value : memoryStack.LoadVariables(value);
			if (content == null)
			{
				memoryStack.Put(key, content, goalStep: goalStep);
				return;
			}

			if (content is JToken)
			{
				memoryStack.Put(key, content, goalStep: goalStep);
				return;
			}

			try
			{
				var str = content.ToString().TrimStart();
				if (str.StartsWith("["))
				{
					var jobject = JArray.Parse(content.ToString());
					memoryStack.Put(key, jobject, goalStep: goalStep);
					return;
				}
				else if (str.StartsWith("{"))
				{
					JObject jobject = JObject.Parse(content.ToString());
					memoryStack.Put(key, jobject, goalStep: goalStep);
					return;
				}

				str = JsonConvert.SerializeObject(str);
				var jobj = JsonConvert.DeserializeObject(str);

				memoryStack.Put(key, jobj, goalStep: goalStep);
			}
			catch
			{
				var str = JsonConvert.SerializeObject(content.ToString());
				var jobj = JsonConvert.DeserializeObject(str);

				memoryStack.Put(key, jobj, goalStep: goalStep);
			}

		}
		[Description(@"Set default value of int/long variable. If value already exists it wont be set.")]
		public async Task SetDefaultNumberVariable([HandlesVariable] string key, long? value = null, long? defaultValue = null, long? maxValue = null, long? minValue = null)
		{
			if (minValue != null && value < minValue) value = minValue;
			if (maxValue != null && value > maxValue) value = maxValue;

			var objectValue = memoryStack.GetObjectValue(key, false);
			if (objectValue.Initiated) return;

			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);

		}
		[Description(@"Set date time variable. Example: set %lastUpdate% = ""2020-01-01 12:20"", or set %yesterday% = %now.AddDays(-1)%. When date/time being set, format it to ISO 8601.")]
		public async Task<IError?> SetDateTimeVariable([HandlesVariable] string key, object? value = null)
		{
			DateTime? dt = null;
			if (value is DateTime dt2)
			{
				dt = dt2;
			}
			else if (DateTime.TryParse(value.ToString(), out var result))
			{
				dt = result;
			}

			if (dt == null)
			{
				return new ProgramError($"Could not parse '{value?.ToString()}' to date time");
			}

			memoryStack.Put(key, dt, goalStep: goalStep);
			return null;
		}
		[Description(@"Set int/long variable.")]
		public async Task SetNumberVariable([HandlesVariable] string key, long? value = null, long? defaultValue = null, long? maxValue = null, long? minValue = null)
		{
			if (minValue != null && value < minValue) value = minValue;
			if (maxValue != null && value > maxValue) value = maxValue;
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}
		[Description(@"Set double variable.")]
		public async Task SetDoubleVariable([HandlesVariable] string key, double value, double? maxValue = null, double? minValue = null)
		{
			if (minValue != null && value < minValue) value = minValue.Value;
			if (maxValue != null && value > maxValue) value = maxValue.Value;
			memoryStack.Put(key, value, goalStep: goalStep);
		}
		[Description(@"Set multiple double variables.")]
		public async Task SetDoubleVariables([HandlesVariable] Dictionary<string, double> values)
		{
			foreach (var value in values)
			{
				memoryStack.Put(value.Key, value.Value, goalStep: goalStep);
			}
		}
		[Description(@"Set float variable.")]
		public async Task SetFloatVariable([HandlesVariable] string key, float? value = null, float? defaultValue = null)
		{
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}
		[Description(@"Set multiple float variables.")]
		public async Task SetFloatVariable([HandlesVariable] Dictionary<string, float> values)
		{
			foreach (var value in values)
			{
				memoryStack.Put(value.Key, value.Value, goalStep: goalStep);
			}
		}
		[Description(@"Set bool variable width condition.")]
		public async Task<IError?> SetBoolVariableWithCondition([HandlesVariable] string key, SimpleCondition simpleCondition, bool? defaultValue = null)
		{
			var program = engine.GetProgram<ConditionalModule.Program>();
			var result = await program.SimpleCondition(simpleCondition);
			if (result.Item2 != null) return result.Error;
			
			memoryStack.Put(key, result.ReturnValue ?? defaultValue, goalStep: goalStep);
			return null;
		}

		[Description(@"Set bool variable.")]
		public async Task SetBoolVariable([HandlesVariable] string key, bool? value = null, bool? defaultValue = null)
		{
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}

		[Description(@"Set variable by calcuation, e.g. `set %total% = %quantity% * %price%`. Uses NCalc.Expression method, make sure it is valid NCalc expression. Capitalize any functions called like sqrt() into Sqrt()")]
		[Example("set %total% = 5 * %item.amount%, round 4", @"key=""%total%"", expression=""5 * %item.amount%, decimalRound=4")]
		public async Task<IError?> SetVariableWithCalculation([HandlesVariable] string key, string expression, int decimalRound = 2, MidpointRounding? midpointRounding = null)
		{
			var program = GetProgramModule<MathModule.Program>();
			var (result, error) = await program.SolveExpression(expression, decimalRound, midpointRounding);
			if (error != null) return error;

			await SetVariable(key, result);
			return error;
		}

		[Description(@"Set variable. Developer might use single/double quote to indicate the string value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetVariable([HandlesVariable] string key, [HandlesVariable] object? value = null, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null, [HandlesVariable] object? defaultValue = null, string? FullTypeName = null)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"         - Start SetVariable (key:{key} | value:{value}) - {stopwatch.ElapsedMilliseconds}");
			if (value == null) value = defaultValue;
			object? content = (doNotLoadVariablesInValue) ? value : memoryStack.LoadVariables(value, true, defaultValue);
			logger.LogDebug($"         - loaded variable - {stopwatch.ElapsedMilliseconds}");
			if (onlyIfValueIsNot?.ToString() == "null" && value == null) return;
			if (onlyIfValueIsNot?.ToString() == "empty" && (value == null || VariableHelper.IsEmpty(value))) return;
			if (onlyIfValueIsNot != null && onlyIfValueIsNot == value) return;

			if (key.Contains("%") && keyIsDynamic)
			{
				var newKey = memoryStack.LoadVariables(key);
				if (!string.IsNullOrWhiteSpace(newKey?.ToString()))
				{
					key = newKey.ToString();
				}
			}

			if (FullTypeName != null && FullTypeName != "System.Object")
			{
				content = TypeHelper.ConvertToType(content, Type.GetType(FullTypeName));
			}

			logger.LogDebug($"         - loaded % - {stopwatch.ElapsedMilliseconds}");
			memoryStack.Put(key, content, goalStep: goalStep);
			logger.LogDebug($"         - Done put into memorystack - {stopwatch.ElapsedMilliseconds}");
		}

		[Description(@"
Converts 2 dates to a number or timespan, such as days ago.
pattern=days|hours|minutes|seconds|milliseconds|nanoseconds|ticks|totaldays|totalhours|totalminutes|totalseconds|totalmilliseconds|totalnanoseconds")]
		[Example("set %days% = total days between %fromDate% and %toDate%", @"pattern=""totaldays""")]
		public async Task<(object, IError?)> GetTimeSpanRelated(DateTime fromDate, DateTime toDate, string pattern)
		{
			var ts = new TimeSpan(toDate.Ticks - fromDate.Ticks);

			if (pattern.Equals("ticks", StringComparison.OrdinalIgnoreCase)) return (ts.Ticks, null);
			if (pattern.Equals("days", StringComparison.OrdinalIgnoreCase)) return (ts.Days, null);
			if (pattern.Equals("hours", StringComparison.OrdinalIgnoreCase)) return (ts.Hours, null);
			if (pattern.Equals("minutes", StringComparison.OrdinalIgnoreCase)) return (ts.Minutes, null);
			if (pattern.Equals("seconds", StringComparison.OrdinalIgnoreCase)) return (ts.Seconds, null);
			if (pattern.Equals("milliseconds", StringComparison.OrdinalIgnoreCase)) return (ts.Milliseconds, null);
			if (pattern.Equals("nanoseconds", StringComparison.OrdinalIgnoreCase)) return (ts.Nanoseconds, null);
			if (pattern.Equals("totaldays", StringComparison.OrdinalIgnoreCase)) return (ts.TotalDays, null);
			if (pattern.Equals("totalhours", StringComparison.OrdinalIgnoreCase)) return (ts.TotalHours, null);
			if (pattern.Equals("totalminutes", StringComparison.OrdinalIgnoreCase)) return (ts.TotalMinutes, null);
			if (pattern.Equals("totalseconds", StringComparison.OrdinalIgnoreCase)) return (ts.TotalSeconds, null);
			if (pattern.Equals("totalmilliseconds", StringComparison.OrdinalIgnoreCase)) return (ts.TotalMilliseconds, null);
			if (pattern.Equals("totalnanoseconds", StringComparison.OrdinalIgnoreCase)) return (ts.TotalNanoseconds, null);

			return (ts, null);
		}

		[Description(@"Set multiple variables with possible default values used with variable(such as %request.query.*% and fix default. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote("") by escaping it. onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetVariables([HandlesVariableAttribute] Dictionary<string, Tuple<object?, object?>?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				await SetVariable(key.Key, key.Value?.Item1 ?? key.Value?.Item2, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}
		}
		[Description(@"Set value on variables. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetValuesOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				await SetVariable(key.Key, key.Value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}

		}



		[Description(@"Set default value on variables if not set, good for setting value if variable is empty. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.
Example:
`set default value %name% = %request.body.name%, %zip% = %request.body.zip%` => [{key=%name%, value=%request.body.zip%}, {key=%zip%, value=%request.body.zip%}]
`set default value %target% = ""#body"" => {key=%target%, value=""#body""}

Bad (dont use for):
`set default %page% = %request.query.page% ?? 1` => use:SetValueOnVariablesOrDefaultIfValueIsEmpty
")]
		public async Task SetDefaultValueOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				var objectValue = memoryStack.GetObjectValue(key.Key, false);
				if (objectValue.Initiated && objectValue.Value != null) continue;

				await SetVariable(key.Key, key.Value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}

		}

		public async Task<IError?> SetVariableWithCondition([HandlesVariableAttribute] string variableName, object value, object valueIfTrue, object valueIfFalse)
		{
			if (value is string str)
			{
				return await SetVariableWithCalculation(variableName, str);
			}
			if (value is bool boolValue)
			{
				if (boolValue)
				{
					await SetVariable(variableName, valueIfTrue);
				}
				else
				{
					await SetVariable(variableName, valueIfFalse);
				}
			}

			return new ProgramError($"Dont know what to do with {value} in SetVariableWithCondition");
		}

		[Description("Type should be c# object type, e.g. System.String, System.DateTime, etc. When undefined, set to System.Object")]
		public record VariableIfEmpty(string Name, object FirstValue, object ValueIfFirstIsEmpty, string Type);

		[Description(@"Set value on variables or a default value is value is empty. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		[Example(@"set %q% = %request.query.q%, or ""hello"" if empty", @"keyValues.key=""%q%"", value=[""%request.query.q%"", ""hello""]")]
		[Example(@"set %q% = %request.query.page% ?? 1", @"keyValues.key=""%page%"", value=[""%request.query.page%"", 1, ""System.Int64""]")]
		public async Task SetValueOnVariablesOrDefaultIfValueIsEmpty([HandlesVariableAttribute] List<VariableIfEmpty> variables, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var variable in variables)
			{



				var value = (variable.FirstValue is string str && str.Contains("%")) ? memoryStack.Get(str) : variable.FirstValue;
				var value2 = (variable.ValueIfFirstIsEmpty is string str2 && str2.Contains("%")) ? memoryStack.Get(str2) : variable.ValueIfFirstIsEmpty;

				var objectValue = memoryStack.GetObjectValue(variable.Name, false);
				object? val = !VariableHelper.IsEmpty(value) ? value : value2;

				if (variable.Type != "System.Object")
				{
					val = TypeHelper.ConvertToType(val, Type.GetType(variable.Type));
				}

				await SetVariable(variable.Name, val, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}
		}

		[Description("Append to variable. valueLocation=postfix|prefix seperatorLocation=end|start")]
		public async Task<object?> AppendToVariable([HandlesVariableAttribute] string key, [HandlesVariable] object? value = null, char seperator = '\n',
		string valueLocation = "postfix", string seperatorLocation = "end", bool shouldBeUnique = false, bool doNotLoadVariablesInValue = false)
		{
			if (value == null) return value;

			value = (doNotLoadVariablesInValue) ? value : memoryStack.LoadVariables(value);

			object? val = memoryStack.Get(key);
			if (val != null && val is string && (value is JObject || value is JProperty || value is JValue))
			{
				value = value.ToString();
			}

			if (val is string str && str.TrimStart().StartsWith("[") && str.TrimEnd().EndsWith("]"))
			{
				try
				{
					val = JArray.Parse(str.Trim());
				}
				catch { }
			}


			if ((val == null || val is string) && value is string)
			{
				if (val == null) val = "";

				string appendingValue = (seperatorLocation == "start") ? seperator.ToString() + value.ToString() : value.ToString() + seperator.ToString();
				val = (valueLocation == "postfix") ? val + appendingValue : appendingValue + val;
			}
			else if (val is JArray jArray)
			{
				if (value is not JArray)
				{
					if (value is string strValue)
					{
						jArray.Add(strValue);
					}
					else if (value is JObject jobj)
					{
						jArray.Merge(jobj);
					}
					else
					{
						jArray.Merge(JArray.FromObject(value));
					}
				}
				else
				{
					jArray.Add(value);
				}

				val = jArray;
			}
			else if (val is System.Collections.IList list)
			{
				if (!shouldBeUnique || (shouldBeUnique && !list.Contains(value)))
				{
					list.Add(value);
				}
			}
			else
			{
				if (val == null && value is IList)
				{
					val = value;
				}
				else
				{
					val = new List<object>();
					((List<object>)val).Add(value);
				}
				//throw new Exception("Cannot append to an object");
			}
			memoryStack.Put(key, val, goalStep: goalStep);
			return val;
		}

		public async Task<object> GetVariable([HandlesVariableAttribute] string key)
		{
			return memoryStack.Get(key);
		}


		public async Task RemoveVariables([HandlesVariableAttribute] string[] keys)
		{
			foreach (var key in keys)
			{
				memoryStack.Remove(key);
			}
		}


		[Description("Sets a value to %Settings.XXXX% variable")]
		public async Task SetSettingValue([HandlesVariableAttribute] string key, object value)
		{
			var settingKey = key.Substring(key.IndexOf('.') + 1).Replace("%", "");
			settings.Set(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
		}

		[Description("Sets a value to %Settings.XXXX% variable but only if it is not set before")]
		public async Task SetDefaultSettingValue([HandlesVariableAttribute] string key, object value)
		{
			var settingKey = key.Substring(key.IndexOf('.') + 1).Replace("%", "");
			var settingValue = settings.GetOrDefault(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
			if (value == settingValue || settingValue == null || string.IsNullOrEmpty(settingValue.ToString()))
			{
				settings.Set(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
			}
		}

		public async Task<string> ConvertToBase64([HandlesVariableAttribute] string key)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(memoryStack.Get(key).ToString()));
		}

		public record ModifyDateTimeOperation(
	[Description("AddDays, AddMonths, AddYears, AddHours, AddMinutes, AddSeconds, AddMilliseconds, AddTicks")]
	string Method,

	[Description("For Add methods, use positive to add, negative to subtract")]
	object[] Parameters
);

		[Description("Applies one or more modifications to a DateTime.")]
		public record ModifyDateTimeParameters(
			DateTime DateTime,
			ModifyDateTimeOperation[] Operations,

			[Description("e.g. 'yyyy-MM-dd', 'HH:mm:ss'")]
	string? Format = null,

			[Description("e.g. 'en-US', 'is-IS'. Defaults to current culture")]
	string? CultureInfo = null
		);

		[Description("Modifies a DateTime by applying one or more operations.")]
		[Example("remove 1 day from %fromDate%, format 'yyyy-MM-dd'", "Operations=[{Method='AddDays', Parameters=[-1]}], Format='yyyy-MM-dd'")]
		[Example("add 3 months to %startDate%", "Operations=[{Method='AddMonths', Parameters=[3]}]")]
		[Example("subtract 2 hours from %timestamp%", "Operations=[{Method='AddHours', Parameters=[-2]}]")]
		[Example("remove 26 hours from %now%", "Operations=[{Method='AddHours', Parameters=[-26]}]")]
		[Example("set %date% as %now% but add one month then subtract 2 days", "Operations=[{Method='AddMonths', Parameters=[1]}, {Method='AddDays', Parameters=[-2]}]")]
		[Example("take %startDate%, add 1 year, add 6 months, subtract 1 day", "Operations=[{Method='AddYears', Parameters=[1]}, {Method='AddMonths', Parameters=[6]}, {Method='AddDays', Parameters=[-1]}]")]
		[Example("add %months% months then %days% days to %date%, format 'yyyy-MM-dd'", "Operations=[{Method='AddMonths', Parameters=[%months%]}, {Method='AddDays', Parameters=[%days%]}], Format='yyyy-MM-dd'")]
		public async Task<(object?, IError?)> ModifyDateTime(ModifyDateTimeParameters p)
		{
			var result = p.DateTime;

			foreach (var op in p.Operations)
			{
				var methods = typeof(DateTime).GetMethods().Where(m => m.Name == op.Method).ToArray();

				if (methods.Length == 0)
					return (null, new ProgramError($"DateTime method '{op.Method}' not found"));

				System.Reflection.MethodInfo? matchedMethod = null;
				object[]? convertedParams = null;

				foreach (var method in methods)
				{
					var methodParams = method.GetParameters();
					if (methodParams.Length != op.Parameters.Length)
						continue;

					convertedParams = new object[op.Parameters.Length];
					var allConverted = true;

					for (int i = 0; i < op.Parameters.Length; i++)
					{
						var converted = TypeHelper.ConvertToType(op.Parameters[i], methodParams[i].ParameterType);
						if (converted == null)
						{
							allConverted = false;
							break;
						}
						convertedParams[i] = converted;
					}

					if (allConverted)
					{
						matchedMethod = method;
						break;
					}
				}

				if (matchedMethod == null)
					return (null, new ProgramError($"Could not match parameters for DateTime method '{op.Method}'"));

				result = (DateTime)matchedMethod.Invoke(result, convertedParams);
			}

			if (p.Format == null)
				return (result, null);

			var culture = p.CultureInfo != null
				? System.Globalization.CultureInfo.GetCultureInfo(p.CultureInfo)
				: System.Globalization.CultureInfo.CurrentCulture;

			return (result.ToString(p.Format, culture), null);
		}


		public async Task<object?> TrimForLlm(object? obj, int maxItemCount = 30, int? maxItemLength = null, string? groupOn = null,
			int samplesPerGroup = 5, int listLimit = 50, int totalCharsLimit = 2000, bool formatJson = false)
		{
			if (obj == null) return null;

			JToken? json = obj as JToken;
			if (json == null)
			{
				try
				{
					json = JToken.FromObject(obj);
				}
				catch
				{
					return null;
				}
			}


			var resultList = new List<string>();
			var totalChars = 0;
			var formatting = (formatJson) ? Formatting.Indented : Formatting.None;

			if (json is JArray arr && !string.IsNullOrWhiteSpace(groupOn))
			{
				var groups = arr
					.OfType<JObject>()
					.Where(x => x.Properties().Any(p => string.Equals(p.Name, groupOn, StringComparison.OrdinalIgnoreCase)))
					.GroupBy(x =>
						x.Properties()
						 .FirstOrDefault(p => string.Equals(p.Name, groupOn, StringComparison.OrdinalIgnoreCase))?
						 .Value.ToString().Trim().ToLowerInvariant()
					);

				foreach (var group in groups)
				{
					foreach (var item in group.Take(samplesPerGroup))
					{
						var trimmed = TrimObject(item);
						var jsonStr = trimmed.ToString(Formatting.None);
						totalChars += Encoding.UTF8.GetByteCount(jsonStr);
						if (totalChars > totalCharsLimit)
							break;

						resultList.Add(trimmed.ToString());
					}

					if (totalChars > totalCharsLimit)
						break;
				}

				return resultList;
			}

			// fallback to old logic if not a group
			if (json is JArray flatArr)
			{
				foreach (var item in flatArr)
				{
					if (resultList.Count >= maxItemCount) break;

					if (item is JObject objItem)
					{
						var trimmed = TrimObject(objItem);
						totalChars += Encoding.UTF8.GetByteCount(trimmed.ToString(formatting));
						if (totalChars > totalCharsLimit) break;
						resultList.Add(trimmed.ToString());
					}
					else if (item is JValue objValue)
					{

						string value = objValue.ToString();
						if (value.Length > maxItemLength) value = value.MaxLength(maxItemLength.Value);
						resultList.Add(value);
					}
				}

				return resultList;
			}

			if (json is JObject singleObj)
			{
				var trimmed = TrimObject(singleObj);
				var jsonStr = trimmed.ToString(formatting);
				return jsonStr.Length > totalCharsLimit ? jsonStr.Substring(0, totalCharsLimit) + "..." : jsonStr;
			}

			return json;
		}

		private JObject TrimObject(JObject input)
		{
			var trimmed = new JObject();

			foreach (var prop in input.Properties())
			{
				var value = prop.Value;

				if (value.Type is JTokenType.String or JTokenType.Object or JTokenType.Array)
				{
					string strVal = value.ToString();
					trimmed[prop.Name] = strVal.Length > 100 ? strVal.Substring(0, 100) + "..." : strVal;
				}

				else
				{
					trimmed[prop.Name] = value;
				}
			}

			return trimmed;
		}

		public async Task<T?> GetSettings<T>(string key)
		{
			return settings.Get<T>(typeof(T), key, default(T), "");
		}
	}



}

