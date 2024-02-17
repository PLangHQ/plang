using NBitcoin;
using Nethereum.ABI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static PLang.Utils.VariableHelper;

namespace PLang.Runtime
{
	public class MemoryStackPrimativeException : Exception
	{
		public MemoryStackPrimativeException(string message) : base(message) { }
	}
	public class MemoryStackPathNotFoundException : Exception
	{
		public MemoryStackPathNotFoundException(string message) : base(message) { }
	}

	public record VariableEvent(VariableEventType EventType, string goalName, Dictionary<string, object?>? Parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50);

	public class ObjectValue
	{
		public ObjectValue(string name, object? value, Type? type, ObjectValue? parent = null, bool Initiated = true)
		{
			Name = name;
			Value = value;
			Type = type;
			this.Initiated = Initiated;
			Parent = parent;
		}

		public List<VariableEvent> Events = new List<VariableEvent>();
		public string Name { get; set; }
		public object? Value { get; set; }
		public Type? Type { get; set; }
		public bool Initiated { get; set; }
		public ObjectValue? Parent { get; set; }
	}
	public class MemoryStack
	{
		Dictionary<string, ObjectValue> variables = new Dictionary<string, ObjectValue>();
		static Dictionary<string, ObjectValue> staticVariables = new Dictionary<string, ObjectValue>();
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		public Goal Goal { get; set; }
		public MemoryStack(IPseudoRuntime pseudoRuntime, IEngine engine, ISettings settings, PLangAppContext context)
		{
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.settings = settings;
			this.context = context;
		}

		public Dictionary<string, ObjectValue> GetMemoryStack()
		{
			return variables;
		}
		public Dictionary<string, ObjectValue> GetStaticMemoryStack()
		{
			return staticVariables;
		}



		public record VariableExecutionPlan(string VariableName, ObjectValue ObjectValue, List<string> Calls, int Index = 0, string? JsonPath = null);


		public ObjectValue GetObjectValue(string variableName, bool staticVariable, bool initiate = false)
		{
			if (variableName == null) return new ObjectValue("", null, null, null, initiate);

			variableName = Clean(variableName).ToLower();
			KeyValuePair<string, ObjectValue> variable;
			if (staticVariable)
			{
				variable = staticVariables.FirstOrDefault(p => p.Key.ToLower() == variableName);
			}
			else
			{
				variable = variables.FirstOrDefault(p => p.Key.ToLower() == variableName);
			}

			if (variable.Key != null)
			{
				return variable.Value;
			}

			var contextObject = context.FirstOrDefault(p => p.Key.ToLower() == variableName);
			if (contextObject.Key != null)
			{
				var type = (contextObject.Value != null) ? contextObject.Value.GetType() : typeof(Nullable);
				return new ObjectValue(variableName, contextObject.Value, type, null, true);
			}
			return new ObjectValue(variableName, null, typeof(Nullable), null, initiate);
		}

		public VariableExecutionPlan GetVariableExecutionPlan(string key, bool staticVariable)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));

			key = Clean(key);

			// position%. %item.title
			if (variables.ContainsKey(key))
			{
				return new VariableExecutionPlan(key, variables[key], new List<string>());
			}
			if (!key.Contains(".") && !key.Contains("[")) return new VariableExecutionPlan(key, new ObjectValue(key, null, null, null, false), new List<string>());

			string[] keySplit = key.Split('.');
			int index = 0;
			string dictKey = "";
			string? jsonPath = null;
			string variableName = keySplit[0].TrimStart('%').TrimEnd('%');
			if (keySplit.Length > 1 && Regex.IsMatch(keySplit[1], "^[0-9]+$"))
			{
				variableName += $"[{keySplit[1]}]";
				keySplit = keySplit.Where((source, index) => index != 1).ToArray();

			}
			ObjectValue? objectValue = null;
			if (variableName.Contains("[") && variableName.Contains("]"))
			{
				var numberData = variableName.Remove(0, variableName.IndexOf("[") + 1);
				variableName = variableName.Substring(0, variableName.IndexOf("["));
				objectValue = GetObjectValue(variableName, staticVariable);

				numberData = numberData.Substring(0, numberData.IndexOf("]")).Trim();
				if (objectValue.Initiated && objectValue.Value.GetType().Name.StartsWith("Dictionary"))
				{
					if (numberData.StartsWith("%") && numberData.EndsWith("%"))
					{
						dictKey = Get(numberData)?.ToString() ?? "";
					}
					else
					{
						dictKey = numberData.Replace("\"", "");
					}
				}
				else
				{
					if (!int.TryParse(numberData, out index))
					{
						if (variables.TryGetValue(numberData, out var indexObjectValue))
						{
							index = (indexObjectValue.Value as int? ?? 0);
						}
					}
				}
			}

			List<string> calls;

			if (objectValue == null) objectValue = GetObjectValue(variableName, staticVariable);
			if (objectValue.Value == null)
			{
				calls = GetCalls(keySplit, jsonPath);
				return new VariableExecutionPlan(variableName, objectValue, calls, index, jsonPath);
			}

			var valueType = objectValue.Value.GetType();
			if (index != 0 || dictKey != "")
			{
				objectValue = GetItemFromListOrDictionary(objectValue, index, dictKey, variableName);
			}

			if ((index == 0 && dictKey == "" && key.Contains("[") && key.Contains("]")) || (objectValue.Value.GetType() == typeof(JObject) || objectValue.Value.GetType() == typeof(JArray)))
			{
				if (objectValue.Type is IDictionary)
				{

				}

				jsonPath = "$" + ((valueType == typeof(JObject)) ? "" : "..");
				for (int i = 1; i < keySplit.Length; i++)
				{
					if (!keySplit[i].Contains("("))
					{
						if (!jsonPath.EndsWith(".")) jsonPath += ".";
						jsonPath += keySplit[i];
					}
				}
			}
			calls = GetCalls(keySplit, jsonPath);

			return new VariableExecutionPlan(variableName, objectValue, calls, index, jsonPath);
		}

		private List<string> GetCalls(string[] keySplit, string jsonPath)
		{
			var calls = new List<string>();
			for (int i = 1; i < keySplit.Length; i++)
			{
				var section = keySplit[i];
				if (!section.Contains("(") && jsonPath == null) //property
				{
					calls.Add(section);
				}
				else if (section.Contains("("))
				{
					calls.Add(section);
				}
			}
			return calls;
		}

		private ObjectValue GetItemFromListOrDictionary(ObjectValue objectValue, int index, string dictKey, string variableName)
		{
			if (objectValue.Value == null) return objectValue;

			if (objectValue.Value is IList list)
			{
				if (list.Count >= index)
				{
					var item = list[index - 1];

					objectValue = new ObjectValue(variableName, item, item.GetType(), objectValue);
				}
				else
				{
					throw new IndexOutOfRangeException($"Item count in {variableName} is {list.Count} but you are trying to get nr {index} which is higher then the count");
				}
			}
			else if (objectValue.Value is IEnumerable enumerator)
			{
				if (dictKey != "")
				{
					var dict = objectValue.Value as IDictionary;
					if (dict.Contains(dictKey))
					{
						objectValue = new ObjectValue(variableName, dict[dictKey], dict[dictKey].GetType(), objectValue);
					}
					else
					{
						throw new KeyNotFoundException($"Could not find {dictKey} in {variableName}");
					}
				}
				else
				{
					int counter = 1;
					foreach (var item in enumerator)
					{
						if (counter++ == index)
						{
							objectValue = new ObjectValue(variableName, item, item.GetType(), objectValue);
							break;
						}
					}
				}
			}
			return objectValue;
		}

		public object? Get(string key, bool staticVariable = false)
		{
			return GetObjectValue2(key, staticVariable).Value;
		}

		public ObjectValue GetObjectValue2(string key, bool staticVariable = false)
		{
			if (key == null) return new ObjectValue("", null, typeof(Nullable), null, false);
			key = Clean(key);

			var keyLower = key.ToLower();
			if (IsNow(keyLower))
			{
				return GetNow(key);
			}

			if (ReservedKeywords.IsReserved(key))
			{
				if (keyLower == ReservedKeywords.MemoryStack.ToLower())
				{
					return new ObjectValue(key, this.GetMemoryStack(), typeof(Dictionary<string, ObjectValue>), null);
				}


				var objV = this.variables.FirstOrDefault(p => p.Key.ToLower() == keyLower);
				if (objV.Key != null && objV.Value != null)
				{
					return objV.Value;
				}

				var value = context.FirstOrDefault(p => p.Key.ToLower() == keyLower);
				if (value.Value != null)
				{
					return new ObjectValue(key, value.Value, value.Value.GetType(), null);
				}

			}

			var variables = (staticVariable) ? staticVariables : this.variables;
			var varKey = variables.FirstOrDefault(p => p.Key.ToLower() == key.ToLower());
			if (varKey.Key != null && varKey.Value.Initiated)
			{
				return variables[varKey.Key];
			}

			var plan = GetVariableExecutionPlan(key, staticVariable);
			if (!plan.ObjectValue.Initiated)
			{
				return plan.ObjectValue;
				//return new ObjectValue(key, null, typeof(Nullable), null, false);
			}

			var objectValue = plan.ObjectValue;
			if (plan.JsonPath != null)
			{
				objectValue = ApplyJsonPath(objectValue, plan);
			}

			foreach (var call in plan.Calls)
			{
				if (call.Contains("("))
				{
					objectValue = ExecuteMethod(objectValue, call, plan.VariableName);
				}
				else
				{
					objectValue = ExecuteProperty(objectValue, call, plan.VariableName);
				}
			}

			return objectValue;
		}



		private ObjectValue ApplyJsonPath(ObjectValue objectValue, VariableExecutionPlan plan)
		{
			if (objectValue.Value == null) return objectValue;

			var objType = objectValue.Value.GetType();
			if (objType != typeof(JObject) || objType != typeof(JArray))
			{
				var json = JsonConvert.SerializeObject(objectValue.Value);
				var jsonObject = JsonConvert.DeserializeObject(json);

				var newObjectValue = new ObjectValue(objectValue.Name, jsonObject, jsonObject.GetType(), objectValue.Parent, objectValue.Initiated);
				newObjectValue.Events = objectValue.Events;
				objectValue = newObjectValue;
				objType = objectValue.Value.GetType();
			}
			IEnumerable<JToken>? tokens = null;
			if (objType == typeof(JObject) && plan.JsonPath != null)
			{
				tokens = ((JObject)objectValue.Value).SelectTokens(plan.JsonPath);

			}
			else if (objType == typeof(JArray) && plan.JsonPath != null)
			{
				try
				{
					tokens = ((JArray)objectValue.Value).SelectTokens(plan.JsonPath);
				}
				catch
				{
					if (plan.JsonPath == "$..")
					{
						tokens = ((JArray)objectValue.Value).SelectTokens("$");
					}
				}

			}

			if (tokens == null)
			{
				return new ObjectValue(objectValue.Name, null, null, null, false);
			}

			object? val = null;
			if (tokens != null && tokens.Count() > 1)
			{
				val = tokens.ToList();
			}
			else if (tokens != null)
			{
				val = tokens.FirstOrDefault();
			}

			if (val == null)
			{
				return new ObjectValue(objectValue.Name, null, null, null, false);
			}

			string objName = objectValue.Name;
			if (plan.JsonPath != null && plan.JsonPath != "$.." && plan.JsonPath.StartsWith("$."))
			{
				var jsonPath = plan.JsonPath.Substring(1);
				if (jsonPath.StartsWith("..")) jsonPath = jsonPath.Substring(1);
				objName += jsonPath;
			}

			return new ObjectValue(objName, val, val.GetType(), objectValue);

		}


		public void PutForBuilder(string key, object? value)
		{
			if (string.IsNullOrEmpty(key)) return;
			//Put(key, value, false, false);
			var objectValue = new ObjectValue(key, value, null, null, false);
			variables.AddOrReplace(key, objectValue);
		}

		public void PutStatic(string key, object? value)
		{
			Put(key, value, true);
		}

		public void Put(string key, object? value, bool staticVariable = false, bool initialize = true, bool convertToJson = true)
		{
			if (key == null) return;
			key = Clean(key);

			if (key.ToLower() == "__memorystack__")
			{
				throw new Exception($"{key} is reserved. You must choose another variable name");
			}
			var variables = (staticVariable) ? staticVariables : this.variables;
			if (value == null)
			{
				AddOrReplace(variables, key, new ObjectValue(key, null, null, null, initialize));
				return;
			}

			if (key.StartsWith("Settings."))
			{
				string settingKey = key.ToLower().Replace("%", "").Replace("settings.", "");
				settings.Set(typeof(Settings), settingKey, value);
				return;
			}

			string strValue = value.ToString()!.Trim();
			if (convertToJson && !value.GetType().Name.StartsWith("<>f__Anonymous") && JsonHelper.IsJson(strValue))
			{
				if (strValue.StartsWith("["))
				{
					value = JArray.Parse(strValue);
				}
				else
				{
					value = JObject.Parse(strValue);
				}
			}
			if (VariableHelper.IsVariable(strValue))
			{
				var plan = GetVariableExecutionPlan(strValue, staticVariable);
				ObjectValue variableValue = plan.ObjectValue;
				foreach (var call in plan.Calls)
				{
					if (call.Contains("("))
					{
						variableValue = ExecuteMethod(variableValue, call, plan.VariableName);
					}
					else
					{
						variableValue = ExecuteProperty(variableValue, call, plan.VariableName);
					}
				}
				value = variableValue.Value;
			}

			if (!key.Contains("."))
			{
				var type = (value == null) ? null : value.GetType();
				AddOrReplace(variables, key, new ObjectValue(key, value, type, null, initialize));
				return;
			}

			if (key.Contains("."))
			{
				var keyPlan = GetVariableExecutionPlan(key, staticVariable);

				ObjectValue objectValue = keyPlan.ObjectValue;
				foreach (var call in keyPlan.Calls)
				{
					object? obj = keyPlan.ObjectValue.Value;
					if (obj == null) obj = new { };

					if (obj.GetType().Name.StartsWith("<>f__Anonymous"))
					{
						var anomType = obj.GetType();
						var properties = new Dictionary<string, object?>();
						foreach (var prop in anomType.GetProperties())
						{
							if (prop.Name.ToLower() == call.ToLower())
							{
								properties[prop.Name] = value;
							}
							else
							{
								properties[prop.Name] = prop.GetValue(obj);
							}
						}

						var anomObject = Activator.CreateInstance(anomType, properties.Values.ToArray());
						if (anomObject == null) throw new NullReferenceException($"{nameof(anomObject)} is null, it should not be null");

						objectValue = new ObjectValue(objectValue.Name, anomObject, anomObject.GetType(), objectValue, initialize);
					}
					else if (call.Contains("("))
					{
						objectValue = ExecuteMethod(objectValue, call, keyPlan.VariableName);
					}
					else
					{
						Type type = obj.GetType();
						PropertyInfo? propInfo = type.GetProperties().FirstOrDefault(p => p.Name.ToLower() == call.ToLower());
						if (propInfo == null)
						{
							throw new VariableDoesNotExistsException($"{call} does not exist on variable {keyPlan.VariableName}, there for I cannot set {key}");
						}
						if (value != null && value.GetType() != propInfo.PropertyType)
						{
							value = Convert.ChangeType(value, propInfo.PropertyType);
						}
						propInfo.SetValue(obj, value);
						objectValue = new ObjectValue(objectValue.Name, obj, obj.GetType(), null, initialize);
					}

				}
				AddOrReplace(variables, keyPlan.VariableName, objectValue);
			}


		}


		private void AddOrReplace(Dictionary<string, ObjectValue> variables, string key, ObjectValue objectValue)
		{
			VariableEventType eventType;
			key = Clean(key);

			if (variables.TryGetValue(key, out ObjectValue? prevValue) && prevValue != null && prevValue.Initiated)
			{
				eventType = VariableEventType.OnChange;
			}
			else
			{
				eventType = VariableEventType.OnCreate;
			}

			if (prevValue != null)
			{
				objectValue.Events = prevValue.Events;
			}

			variables.AddOrReplace(key, objectValue);
			CallEvent(eventType, objectValue);
		}

		private void CallEvent(VariableEventType eventType, ObjectValue objectValue)
		{
			var events = objectValue.Events.Where(p => p.EventType == eventType);
			foreach (var eve in events)
			{
				var task = Task.Run(async () =>
				{
					var context = engine.GetContext();
					if (context != null && context.ContainsKey(ReservedKeywords.Goal))
					{
						var goal = context[ReservedKeywords.Goal] as Goal;
						if (goal != null)
						{
							await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, eve.goalName, eve.Parameters);
						}
					}
				});
				task.Wait();
			}
		}

		public bool Contains(string? key)
		{
			if (key == null) return false;
			key = Clean(key);
			if (variables.ContainsKey(key))
			{
				return true;
			}
			return staticVariables.ContainsKey(key);

		}

		public string Clean(string str)
		{
			str = str.Trim();
			bool isVariable = IsVariable(str);

			if (isVariable && str.StartsWith("%")) str = str.Substring(1);
			if (isVariable && str.EndsWith("%")) str = str.Remove(str.Length - 1);

			if (str.StartsWith("$.")) str = str.Remove(0, 2);
			return str.Replace("α", ".");
		}
		public T? Get<T>(string key, bool staticVariable = false)
		{
			return (T?)Get(key, staticVariable);
		}

		public object? Get(string key, Type type, bool staticVariable = false)
		{
			var obj = Get(key, staticVariable);
			return ConvertToType(obj, key, type);
		}

		public static object? ConvertToType(object? value, string key, Type targetType)
		{
			if (value == null) return null;
			if (targetType.Name == "Object")
			{
				return value;
			}

			if (targetType.FullName.EndsWith("&"))
			{
				targetType = Type.GetType(targetType.FullName.Substring(0, targetType.FullName.Length - 1));
				if (targetType == null) return null;
			}



			if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				// If value is null or empty, return null
				if (value == null || string.IsNullOrEmpty(value.ToString()))
					return null;

				// Get the underlying type of the nullable type
				Type underlyingType = Nullable.GetUnderlyingType(targetType);

				// Convert the value to the underlying type and then convert it to the nullable type
				try
				{
					return GetInstance(value, targetType, underlyingType);
				}
				catch (Exception ex)
				{
					throw new RuntimeException($"Could not convert %{key}% to {underlyingType.Name} because it is a type of {value.GetType().Name}");
				}
			}
			if (value is IDictionary dictionary)
			{
				return dictionary;

			}
			else if (value is IList list)
			{
				if (value.GetType() == typeof(JArray))
				{
					list = (IList)((JArray)value).ToObject(targetType);
				}
				return list;
			}
			else if (value is IEnumerable<KeyValuePair<string, int>> keyValuePairs)
			{
				return keyValuePairs;
			}
			else if (value is IEnumerable<object> objects && value is not JValue)
			{
				//return objects;
			}

			try
			{
				return Convert.ChangeType(value, targetType);
			}
			catch (Exception ex)
			{
				throw new RuntimeException($"Could not convert %{key}% to {targetType.Name} because it is a type of {value.GetType().Name}");
			}

		}

		private static object? GetInstance(object? value, Type targetType, Type underlyingType)
		{
			return Activator.CreateInstance(targetType, Convert.ChangeType(value, underlyingType));
		}

		private bool IsNow(string keyLower)
		{
			keyLower = keyLower.Replace("%", "");
			return keyLower == "now" || keyLower.StartsWith("now+") || keyLower.StartsWith("now-") || keyLower.StartsWith("now.") ||
				keyLower == "nowutc" || keyLower.StartsWith("nowutc+") || keyLower.StartsWith("nowutc-") || keyLower.StartsWith("nowutc.")
				;
		}

		private ObjectValue GetNow(string key)
		{
			var nowFunc = (key.StartsWith("nowutc")) ? SystemTime.UtcNow : SystemTime.Now;

			if (key.Contains("="))
			{
				string[] strings = key.Split("=");
				key = strings[0];
				if (DateTime.TryParse(strings[1].Trim(), out DateTime result))
				{
					return new ObjectValue(key, result, typeof(DateTime), null);
				}

			}
			if (key.Contains("+") && !key.Contains("."))
			{
				string[] strings = key.Split("+");
				string addString = strings[1];
				return new ObjectValue(key, CalculateDate(nowFunc, "+", addString), typeof(DateTime), null);
			}
			if (key.Contains("-") && !key.Contains("."))
			{
				string[] strings = key.Split("-");
				string addString = strings[1];
				return new ObjectValue(key, CalculateDate(nowFunc, "-", addString), typeof(DateTime), null);
			}
			if (key.Contains("."))
			{
				var objectValue = new ObjectValue(key, nowFunc(), typeof(DateTime), null);
				if (key.Contains("("))
				{
					objectValue = ExecuteMethod(objectValue, key.Substring(key.IndexOf(".") + 1), "Now");
				}
				else
				{
					objectValue = ExecuteProperty(objectValue, key.Substring(key.IndexOf(".") + 1), "Now");
				}

				if (objectValue.Value != null) return objectValue;

			}

			return new ObjectValue(key, nowFunc(), typeof(DateTime), null);
		}


		private ObjectValue ExecuteProperty(ObjectValue objValue, string propertyDescription, string variableName)
		{
			var obj = objValue.Value;
			if (obj == null) return objValue;

			object? value = null;
			var type = obj.GetType();

			propertyDescription = propertyDescription.Trim();

			AppContext.TryGetSwitch("builder", out bool isBuilder);

			if (type == typeof(ExpandoObject) || type.Name == "DapperRow")
			{
				var expando = ((IDictionary<string, object>)obj);
				var key = expando.Keys.FirstOrDefault(p => p.ToLower() == propertyDescription.ToLower());
				if (key != null)
				{
					value = expando[key];
				}
				else
				{
					value = null;
				}
			}
			else
			{
				var property = type.GetProperties().Where(p => p.Name.ToLower() == propertyDescription.ToLower()).FirstOrDefault();
				if (property == null)
				{
					// Not to throw exception on build if property is not found.
					if (!isBuilder)
					{
						throw new PropertyNotFoundException($"Property '{propertyDescription}' was not found on %{variableName}%. The %{variableName}% value is {obj}");
					}
				}
				else
				{
					value = property.GetValue(obj);
				}
			}

			if (value == null)
			{
				return new ObjectValue(objValue.Name, null, null, null, false);
			}
			var objectValue = new ObjectValue(objValue.Name, value, value.GetType(), objValue);
			return objectValue;

		}
		private ObjectValue ExecuteMethod(ObjectValue objValue, string methodDescription, string variableName)
		{
			var obj = objValue.Value;
			if (obj == null) return objValue;

			AppContext.TryGetSwitch("builder", out bool isBuilder);

			if (methodDescription.ToLower().StartsWith("tojson("))
			{
				string json;
				if (JsonHelper.IsJson(obj))
				{
					json = obj.ToString();
				}
				else
				{
					if (methodDescription.ToLower().StartsWith("tojson(true)"))
					{
						json = JsonConvert.SerializeObject(obj, Formatting.None);
					}
					else
					{
						json = JsonConvert.SerializeObject(obj);
					}
				}

				var objectValue = new ObjectValue(objValue.Name, json, json.GetType(), objValue);
				return objectValue;
			}

			// handle dynamic object Load specially
			if (methodDescription.ToLower() == "load()" && obj is dynamic && obj is IEnumerable enumerable)
			{
				if (enumerable is IDictionary dictionary)
				{
					return new ObjectValue(objValue.Name, dictionary, dictionary.GetType(), objValue);

				}
				else if (enumerable is IList list)
				{
					return new ObjectValue(objValue.Name, list, list.GetType(), objValue);
				}
				else if (enumerable is IEnumerable<KeyValuePair<string, int>> keyValuePairs)
				{
					var dict = keyValuePairs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
					return new ObjectValue(objValue.Name, dict, dict.GetType(), objValue);
				}
				else if (enumerable is IEnumerable<object> objects)
				{
					var list3 = objects.ToList();
					return new ObjectValue(objValue.Name, list3, list3.GetType(), objValue);
				}

				List<object> list2 = new List<object>();
				foreach (var item in enumerable)
				{
					list2.Add(item);
				}
				return new ObjectValue(objValue.Name, list2, list2.GetType(), objValue); ;
			}

			var methodName = methodDescription.Substring(0, methodDescription.IndexOf("("));
			var paramString = methodDescription.Substring(methodName.Length + 1, methodDescription.Length - methodName.Length - 2).TrimEnd(')');

			var splitParams = paramString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
			List<object> paramValues = new List<object>();
			splitParams.ForEach(p => paramValues.Add(p));

			var type = obj.GetType();
			var methods = GetMethodsOnType(type, methodName, paramValues, obj).ToList();

			if (methods.Count == 0 && !isBuilder)
			{
				throw new MethodNotFoundException($"Method {methodName} not found on {variableName}");
			}


			for (int b = 0; b < methods.Count; b++)
			{
				var method = methods[b];
				try
				{
					var parameters = method.GetParameters();
					var convertedParams = new object[paramValues.Count];
					for (int i = 0; i < paramValues.Count; i++)
					{
						var paramType = parameters[i].ParameterType;
						if (paramType == typeof(string))
						{
							var paramValue = paramValues[i].ToString().Trim()
									.Replace("\\\\", "\\").Replace("\\\"", "\"");
							if (paramValue.StartsWith("\"") && paramValue.EndsWith("\""))
							{
								paramValue = paramValue.Substring(1, paramValue.Length - 2);
							}
							convertedParams[i] = paramValue;
						}
						else if (paramValues[i].GetType() == paramType)
						{
							convertedParams[i] = paramValues[i];
						}
						else if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IEnumerable<>) && paramValues[i] is IEnumerable)
						{
							convertedParams[i] = paramValues[i];
							method = method.MakeGenericMethod(paramValues[i].GetType().GetGenericArguments()[0]);
						}
						else
						{
							convertedParams[i] = Convert.ChangeType(paramValues[i], paramType);
						}
					}

					obj = method.Invoke(obj, convertedParams);
					if (obj == null)
					{
						return new ObjectValue(objValue.Name, null, typeof(Nullable), null, false);
					}

					var objectValue = new ObjectValue(objValue.Name, obj, obj.GetType(), null);
					return objectValue;
				}
				catch
				{
					//if method signature doesnt match, we ignore it. We will write out to log if method cant be found
					continue;
				}
			}


			return objValue;
		}

		private IEnumerable<MethodInfo> GetMethodsOnType(Type type, string methodName, List<object> paramValues, object obj)
		{


			var methods = type.GetMethods().Where(p => p.Name.ToLower() == methodName.ToLower() && p.GetParameters().Length == paramValues.Count);
			if (methods.Count() == 0)
			{
				methods = GetExtensionMethods(type, methodName, obj);
				if (methods != null && methods.Count() > 0)
				{
					paramValues.Insert(0, obj);
				}
			}
			return methods ?? new List<MethodInfo>();

		}

		private IEnumerable<MethodInfo>? GetExtensionMethods(Type extendedType, string methodName, object obj)
		{
			var type = this.GetType().Assembly.GetTypes().FirstOrDefault(p => p.Name == extendedType.Name + "Extension");
			if (type != null)
			{
				var query = from method in type.GetMethods(BindingFlags.Static | BindingFlags.Public)
							where method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)
							where method.GetParameters()[0].ParameterType == extendedType
							where method.Name == methodName
							select method;
				return query;
			}

			if (obj is IEnumerable)
			{
				var query2 = from t in Assembly.GetAssembly(typeof(Enumerable)).GetTypes()
							 where t.IsClass && t.Namespace == "System.Linq"
							 select t;

				// Get all the methods within these types
				var methods = query2.SelectMany(t => t.GetMethods()).Distinct();

				// Filter out the methods that match the given name
				return methods.Where(m => m.Name.ToLower() == methodName.ToLower());
			}
			return null;

		}

		private DateTime CalculateDate(Func<DateTime> nowFunc, string sign, string command)
		{
			var regex = new Regex(@"^([0-9]+)\s*([a-zA-Z]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
			var matches = regex.Matches(command);
			if (matches.Count == 0 || matches[0].Groups.Count != 3) return nowFunc();

			int multiplier = (sign == "+") ? 1 : -1;
			string number = matches[0].Groups[1].Value;
			string function = matches[0].Groups[2].Value;


			if (!int.TryParse(number, out int intValue))
			{
				return nowFunc();
			}

			if (function == "micro")
			{
				return nowFunc().AddMicroseconds(multiplier * intValue);
			}
			if (function == "ms")
			{
				return nowFunc().AddMilliseconds(multiplier * intValue);
			}
			if (function.StartsWith("sec"))
			{
				return nowFunc().AddSeconds(multiplier * intValue);
			}
			if (function.StartsWith("min"))
			{
				return nowFunc().AddMinutes(multiplier * intValue);
			}
			if (function.StartsWith("hour"))
			{
				return nowFunc().AddHours(multiplier * intValue);
			}
			if (function.StartsWith("day"))
			{
				return nowFunc().AddDays(multiplier * intValue);
			}
			if (function.StartsWith("month"))
			{
				return nowFunc().AddMonths(multiplier * intValue);
			}
			if (function.StartsWith("year"))
			{
				return nowFunc().AddYears(multiplier * intValue);
			}


			return nowFunc();
		}


		public void Remove(string key)
		{
			key = Clean(key);
			if (key.Contains("."))
			{
				throw new ArgumentException("When remove item from memory it cannot be a partial of the item. That means you cannot use dot(.)");
			}

			if (variables.TryGetValue(key, out ObjectValue? objectValue))
			{
				variables.Remove(key);
				CallEvent(VariableEventType.OnRemove, objectValue);
			}
		}

		public void RemoveStatic(string key)
		{
			key = Clean(key);
			if (key.Contains("."))
			{
				throw new ArgumentException("When remove item from memory it cannot be a partial of the item. That means you cannot use dot(.)");
			}

			if (staticVariables.TryGetValue(key, out ObjectValue? objectValue))
			{
				staticVariables.Remove(key);
				CallEvent(VariableEventType.OnRemove, objectValue);
			}
		}


		public object? GetStatic(string key)
		{
			return Get(key, true);
		}

		public void AddOnCreateEvent(string key, string goalName, bool staticVariable = false, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 0)
		{
			key = Clean(key);
			var variables = (staticVariable) ? staticVariables : this.variables;
			var objectValue = GetObjectValue(key, staticVariable);
			if (!objectValue.Initiated)
			{
				Put(key, null, staticVariable, false);
				objectValue = GetObjectValue(key, staticVariable);
			}
			var eve = objectValue.Events.FirstOrDefault(p => p.EventType == VariableEventType.OnCreate && p.goalName == goalName);
			if (eve == null)
			{
				objectValue.Events.Add(new VariableEvent(VariableEventType.OnCreate, goalName, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds));
				variables.AddOrReplace(key, objectValue);
			}
		}
		public void AddOnChangeEvent(string key, string goalName, bool staticVariable = false, bool notifyWhenCreated = true, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 0)
		{
			key = Clean(key);
			var variables = (staticVariable) ? staticVariables : this.variables;
			var objectValue = GetObjectValue(key, staticVariable, notifyWhenCreated);
			if (!objectValue.Initiated)
			{
				Put(key, null, staticVariable, notifyWhenCreated);
				objectValue = GetObjectValue(key, staticVariable, notifyWhenCreated);
			};
			var eve = objectValue.Events.FirstOrDefault(p => p.EventType == VariableEventType.OnChange && p.goalName == goalName);
			if (eve == null)
			{
				objectValue.Events.Add(new VariableEvent(VariableEventType.OnChange, goalName, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds));
				variables.AddOrReplace(key, objectValue);
			}
		}


		public void AddOnRemoveEvent(string key, string goalName, bool staticVariable = false, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 0)
		{
			key = Clean(key);
			var variables = (staticVariable) ? staticVariables : this.variables;

			var objectValue = GetObjectValue(key, staticVariable);
			if (objectValue == null) return;

			var eve = objectValue.Events.FirstOrDefault(p => p.EventType == VariableEventType.OnRemove && p.goalName == goalName);
			if (eve == null)
			{
				objectValue.Events.Add(new VariableEvent(VariableEventType.OnRemove, goalName, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds));
				variables.AddOrReplace(key, objectValue);

			}
		}

		internal void Clear()
		{
			this.variables.Clear();
			staticVariables.Clear();
		}


	}
}
