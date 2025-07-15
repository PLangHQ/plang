using Microsoft.Data.Sqlite;
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
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using static PLang.Modules.DbModule.Program;

namespace PLang.Modules.VariableModule
{
	[Description("Set, Get & Return variables. Set on variable includes condition such as empty or null. Bind onCreate, onChange, onRemove events to variable. Trim variable for llm")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;
		private readonly ProgramFactory programFactory;
		private new readonly VariableHelper variableHelper;

		public Program(ISettings settings, ProgramFactory programFactory, VariableHelper variableHelper) : base()
		{
			this.settings = settings;
			this.programFactory = programFactory;
			this.variableHelper = variableHelper;
		}
		public async Task<(object?, IError?)> Load([HandlesVariable] List<string> variables)
		{
			Dictionary<string, object?> varsWithValue = new();
			foreach (var variable in variables)
			{
				varsWithValue.Add(variable, null);
			}

			return await LoadWithDefaultValue(varsWithValue);
		}

		[Description(@"Loads a variable with a default value, key: variable name, value: default value. example: load %age%, default 10 => key:%age% value:10")]
		public async Task<(object?, IError?)> LoadWithDefaultValue([HandlesVariable] Dictionary<string, object?> variablesWithDefaultValue)
		{
			var db = programFactory.GetProgram<DbModule.Program>(goalStep);

			List<object?> objects = new();
			foreach (var variable in variablesWithDefaultValue)
			{
				List<ParameterInfo> parameters = new();
				parameters.Add(new ParameterInfo("variable", variable.Key, "System.String"));
				try
				{
					var result = await db.Select("SELECT * FROM __Variables__ WHERE variable=@variable", parameters);
					if (result.Error != null && result.Error.Message.Contains("no such table"))
					{
						var createTableResult = await CreateVariablesTable(db);
						if (createTableResult.Error != null) return (null, createTableResult.Error);
						return await LoadWithDefaultValue(variablesWithDefaultValue);
					}

					if (result.Table == null || result.Table.Count == 0)
					{
						memoryStack.Put(variable.Key, variable.Value, goalStep: goalStep);
						continue;
					}

					var row = (Row) result.Table[0];
					if (row == null)
					{
						memoryStack.Put(variable.Key, variable.Value, goalStep: goalStep);
						continue;
					}

					var json = row["value"]?.ToString();
					if (json == null)
					{
						memoryStack.Put(variable.Key, variable.Value, goalStep: goalStep);
						continue;
					}

					var value = JsonConvert.DeserializeObject(json);

					if (function?.ReturnValues == null || function?.ReturnValues?.Count == 0)
					{
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
			return (objects, null);
		}

		private async Task<(long, IError? Error)> CreateVariablesTable(DbModule.Program db)
		{
			return await db.Execute(@"CREATE TABLE __Variables__ (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    variable TEXT NOT NULL UNIQUE,
    value TEXT,
    created DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated DATETIME DEFAULT CURRENT_TIMESTAMP,
    expires DATETIME
);", ["__Variables__"]);

		}

		public async Task<IError?> Store([HandlesVariable] List<string> variables)
		{
			var db = programFactory.GetProgram<DbModule.Program>(goalStep);
			var datasource = await db.GetDataSource();
			if (datasource.TypeFullName != typeof(SqliteConnection).FullName)
			{
				return new ProgramError("Only sqlite is supported");
			}

			foreach (var variable in variables)
			{
				var value = variableHelper.LoadVariables(variable);
				if (value == null) continue;

				List<ParameterInfo> parameters = new();
				parameters.Add(new ParameterInfo("variable", variable, "System.String"));
				parameters.Add(new ParameterInfo("value", JsonConvert.SerializeObject(value), "System.String"));

				var result = await db.Select("INSERT INTO __Variables__ (variable, value) VALUES (@variable, @value) ON CONFLICT(variable) DO UPDATE SET value = excluded.value;", parameters);
				if (result.Error != null)
				{
					if (result.Error.Message.Contains("no such table"))
					{
						var createTableResult = await CreateVariablesTable(db);
						if (createTableResult.Error != null) return createTableResult.Error;

						return await Store(variables);
					}
					return result.Error;
				}
			}
			return null;
		}

		[Description("One or more variables to return. Variable can contain !, e.g. !callback=%callback%. When key is undefined, it is same as value, e.g. return %name% => then variables dictionary has key and value as name=%name%")]
		public async Task<IError?> Return([HandlesVariable] Dictionary<string, object> variables)
		{
			var returnValues = new List<ObjectValue>();
			var properties = new Properties();

			var propertyVars = variables.Where(p => p.Key.StartsWith("!"));
			foreach (var property in propertyVars)
			{
				var value = variableHelper.LoadVariables(property.Value);
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

				} else { 
					var value = variableHelper.LoadVariables(variable.Value);
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

			return variableHelper.LoadVariables(content);
		}
		[Description(@"Set string variable. Developer might use single/double quote to indicate the string value, the wrapped quote should not be included in the value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetStringVariable([HandlesVariable] string key, [HandlesVariable] string? value = null, bool urlDecode = false, bool htmlDecode = false, bool doNotLoadVariablesInValue = false, [HandlesVariable] string? defaultValue = null)
		{
			if (value == null) value = defaultValue;

			if (urlDecode) value = HttpUtility.UrlDecode(value);
			if (htmlDecode) value = HttpUtility.HtmlDecode(value);

			object? content = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);
			memoryStack.Put(key, content, goalStep: goalStep);
		}

		[Description(@"Set json variable. Make sure value is valid json")]
		public async Task SetJsonObjectVariable([HandlesVariable] string key, [HandlesVariable] object? value = null, bool doNotLoadVariablesInValue = false, [HandlesVariable] object? defaultValue = null)
		{
			if (value == null) value = defaultValue;

			object? content = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);
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
		public async Task SetDefaultNumberVariable([HandlesVariable] string key, long? value = null, long? defaultValue = null)
		{
			var objectValue = memoryStack.GetObjectValue(key, false);
			if (objectValue.Initiated) return;

			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);

		}

		[Description(@"Set int/long variable.")]
		public async Task SetNumberVariable([HandlesVariable] string key, long? value = null, long? defaultValue = null)
		{
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}
		[Description(@"Set double variable.")]
		public async Task SetDoubleVariable([HandlesVariable] string key, double? value = null, double? defaultValue = null)
		{
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}
		[Description(@"Set float variable.")]
		public async Task SetFloatVariable([HandlesVariable] string key, float? value = null, float? defaultValue = null)
		{
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}
		[Description(@"Set bool variable.")]
		public async Task SetBoolVariable([HandlesVariable] string key, bool? value = null, bool? defaultValue = null)
		{
			memoryStack.Put(key, value ?? defaultValue, goalStep: goalStep);
		}

		[Description(@"Set variable. Developer might use single/double quote to indicate the string value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetVariable([HandlesVariable] string key, [HandlesVariable] object? value = null, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null, [HandlesVariable] object? defaultValue = null)
		{
			if (value == null) value = defaultValue;
			object? content = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value, true, defaultValue);

			if (onlyIfValueIsNot?.ToString() == "null" && value == null) return;
			if (onlyIfValueIsNot?.ToString() == "empty" && (value == null || VariableHelper.IsEmpty(value))) return;
			if (onlyIfValueIsNot != null && onlyIfValueIsNot == value) return;

			if (key.Contains("%") && keyIsDynamic)
			{
				var newKey = variableHelper.LoadVariables(key);
				if (!string.IsNullOrWhiteSpace(newKey.ToString()))
				{
					key = newKey.ToString();
				}
			}
			memoryStack.Put(key, content, goalStep: goalStep);
		}



		[Description(@"Set multiple variables with possible default values. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote("") by escaping it. onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
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



		[Description(@"Set default value on variables if not set, good for setting value if variable is empty. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetDefaultValueOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				var objectValue = memoryStack.GetObjectValue(key.Key, false);
				if (objectValue.Initiated) continue;

				await SetVariable(key.Key, key.Value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}

		}

		public async Task SetVariableWithCondition([HandlesVariableAttribute] string variableName, bool boolValue, object valueIfTrue, object valueIfFalse) {
			if (boolValue)
			{
				await SetVariable(variableName, valueIfTrue);
			} else
			{
				await SetVariable(variableName, valueIfFalse);
			}
		}

		[Description(@"Set value on variables or a default value is value is empty. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetValueOnVariablesOrDefaultIfValueIsEmpty([HandlesVariableAttribute] Dictionary<string, Tuple<object?, object?>> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				var objectValue = memoryStack.GetObjectValue(key.Key, false);
				object? value = !VariableHelper.IsEmpty(key.Value.Item1) ? key.Value.Item1 : key.Value.Item2;

				await SetVariable(key.Key, value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}
		}

		[Description("Append to variable. valueLocation=postfix|prefix seperatorLocation=end|start")]
		public async Task<object?> AppendToVariable([HandlesVariableAttribute] string key, [HandlesVariable] object? value = null, char seperator = '\n',
		string valueLocation = "postfix", string seperatorLocation = "end", bool shouldBeUnique = false, bool doNotLoadVariablesInValue = false)
		{
			if (value == null) return value;

			value = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);

			object? val = memoryStack.Get(key);
			if (val != null && val is string && (value is JObject || value is JProperty || value is JValue))
			{
				value = value.ToString();
			}

			if ((val == null || val is string) && value is string)
			{
				if (val == null) val = "";

				string appendingValue = (seperatorLocation == "start") ? seperator.ToString() + value.ToString() : value.ToString() + seperator.ToString();
				val = (valueLocation == "postfix") ? val + appendingValue : appendingValue + val;
			}
			else if (val is System.Collections.IList list)
			{
				if (!shouldBeUnique || (shouldBeUnique && !list.Contains(val)))
				{
					list.Add(value);
				}
			}
			else
			{
				val = new List<object>();
				((List<object>)val).Add(value);
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

