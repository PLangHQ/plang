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

	public record VariableEvent(VariableEventType EventType, string goalName, Dictionary<string, object>? Parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50);

	public class ObjectValue
	{
		public ObjectValue(object? value, Type? type, bool Initiated = true)
		{
			Value = value;
			Type = type;
			this.Initiated = Initiated;
		}

		public List<VariableEvent> Events = new List<VariableEvent>();

		public object? Value { get; set; }
		public Type? Type { get; set; }
		public bool Initiated { get; set; }
	}
	public class MemoryStack
	{
		Dictionary<string, ObjectValue> variables = new Dictionary<string, ObjectValue>();
		static Dictionary<string, ObjectValue> staticVariables = new Dictionary<string, ObjectValue>();
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly PLangAppContext context;

		public MemoryStack(IPseudoRuntime pseudoRuntime, IEngine engine, PLangAppContext context)
		{
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
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

		public void Init(string key, Type type)
		{
			variables.AddOrReplace(key, new ObjectValue(null, type, false));
		}
		public void InitFakeValue(string key, Type type, string value)
		{
			variables.AddOrReplace(key, new ObjectValue(value, type, false));
		}

		public record VariableExecutionPlan(string VariableName, ObjectValue ObjectValue, List<string> Calls, int Index = 0, string? JsonPath = null);


		public ObjectValue GetObjectValue(string variableName, bool staticVariable)
		{
			if (variableName == null) return new ObjectValue(null, null, false);

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
				return new ObjectValue(contextObject.Value, contextObject.Value.GetType(), true);
			}
			return new ObjectValue(null, null, false);
		}

		public VariableExecutionPlan GetVariableExecutionPlan(string key, bool staticVariable)
		{
			if (key == null) return null;
			key = Clean(key);

			if (variables.ContainsKey(key))
			{
				return new VariableExecutionPlan(key, variables[key], new List<string>());
			}
			if (!key.Contains(".") && !key.Contains("[")) return new VariableExecutionPlan(key, new ObjectValue(null, null, false), new List<string>());

			string[] keySplit = key.Split('.');
			int index = 0;
			string dictKey = "";
			string jsonPath = null;
			string variableName = keySplit[0];
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

			if (objectValue.Value.GetType() == typeof(JObject) || objectValue.Value.GetType() == typeof(JArray))
			{
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
					objectValue = new ObjectValue(item, item.GetType());
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
						objectValue = new ObjectValue(dict[dictKey], dict[dictKey].GetType());
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
							objectValue = new ObjectValue(item, item.GetType());
							break;
						}
					}
				}
			}
			return objectValue;
		}

		public object? Get(string key, bool staticVariable = false)
		{
			if (key == null) return null;
			key = Clean(key);
			var keyLower = key.ToLower();
			if (keyLower == "now" || keyLower.StartsWith("now+") || keyLower.StartsWith("now-") || keyLower.StartsWith("now."))
			{
				return GetNow(key);
			}

			if (ReservedKeywords.IsReserved(key))
			{
				if (keyLower == ReservedKeywords.MemoryStack.ToLower())
				{
					return this.GetMemoryStack();
				}


				var objV = this.variables.FirstOrDefault(p => p.Key.ToLower() == keyLower);
				if (objV.Key != null && objV.Value != null)
				{
					return objV.Value.Value;
				}

				var value = context.FirstOrDefault(p => p.Key.ToLower() == keyLower);
				
				return value.Value;
			}

			var variables = (staticVariable) ? staticVariables : this.variables;
			var varKey = variables.FirstOrDefault(p => p.Key.ToLower() == key.ToLower());
			if (varKey.Key != null && varKey.Value.Initiated)
			{
				return variables[varKey.Key].Value;
			}

			var plan = GetVariableExecutionPlan(key, staticVariable);
			if (!plan.ObjectValue.Initiated)
			{
				return null;
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


			return objectValue.Value;
		}

		private ObjectValue ApplyJsonPath(ObjectValue objectValue, VariableExecutionPlan plan)
		{

			var objType = objectValue.Value.GetType();
			IEnumerable<JToken>? tokens = null;
			if (objType == typeof(JObject))
			{
				tokens = ((JObject)objectValue.Value).SelectTokens(plan.JsonPath);

			}
			else if (objType == typeof(JArray))
			{
				try
				{
					tokens = ((JArray)objectValue.Value).SelectTokens(plan.JsonPath);
				} catch
				{
					if (plan.JsonPath == "$..")
					{
						tokens = ((JArray)objectValue.Value).SelectTokens("$");
					}
				}

			}

			if (tokens == null)
			{
				return new ObjectValue(null, null, false);
			}

			object val;
			if (tokens.Count() > 1)
			{
				val = tokens.ToList();
			}
			else
			{
				val = tokens.FirstOrDefault();
			}
			if (val == null)
			{
				return new ObjectValue(null, null, false);
			}
			return new ObjectValue(val, val.GetType());

		}


		public void PutForBuilder(string key, object value)
		{
			Put(key, value, false, false);
		}

		public void PutStatic(string key, object value)
		{
			Put(key, value, true);
		}

		public void Put(string key, object value, bool staticVariable = false, bool initialize = true, bool convertToJson = true)
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
				AddOrReplace(variables, key, new ObjectValue(null, null, false));
				return;
			}

			string strValue = value.ToString().Trim();
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
			if (strValue.StartsWith("%") && strValue.EndsWith("%"))
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
				AddOrReplace(variables, key, new ObjectValue(value, type, initialize));
				return;
			}

			if (key.Contains("."))
			{
				var keyPlan = GetVariableExecutionPlan(key, staticVariable);

				ObjectValue objectValue = keyPlan.ObjectValue;
				foreach (var call in keyPlan.Calls)
				{
					object obj = keyPlan.ObjectValue.Value;
					if (obj == null) obj = new { };

					if (obj.GetType().Name.StartsWith("<>f__Anonymous"))
					{
						var anomType = obj.GetType();
						var properties = new Dictionary<string, object>();
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
						objectValue = new ObjectValue(anomObject, anomObject.GetType(), initialize);
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
						if (value.GetType() != propInfo.PropertyType)
						{
							value = Convert.ChangeType(value, propInfo.PropertyType);
						}
						propInfo.SetValue(obj, value);
						objectValue = new ObjectValue(obj, obj.GetType(), initialize);
					}

				}
				AddOrReplace(variables, keyPlan.VariableName, objectValue);
			}


		}


		private void AddOrReplace(Dictionary<string, ObjectValue> variables, string key, ObjectValue objectValue)
		{
			VariableEventType eventType;

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
						var goal = (Goal)context[ReservedKeywords.Goal];
						await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, eve.goalName, eve.Parameters);
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
			if (str == null) return null;
			str = str.Trim();
			if (str.StartsWith("%")) str = str.Substring(1);
			if (str.EndsWith("%")) str = str.Remove(str.Length - 1);
			if (str.StartsWith("$.")) str = str.Remove(0, 2);
			return str.Replace("α", ".");
		}
		public T? Get<T>(string key, bool staticVariable = false)
		{
			return (T?)Get(key, staticVariable);
		}

		public object? Get(string key, Type type, bool staticVariable = false)
		{
			return ConvertToType(Get(key, staticVariable), type);
		}

		public static object ConvertToType(object value, Type targetType)
		{
			if (value == null) return null; 
			if (targetType.Name == "Object")
			{
				return value;
			}

			if (targetType.FullName.EndsWith("&"))
			{
				targetType = Type.GetType(targetType.FullName.Substring(0, targetType.FullName.Length - 1));
			}


			if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				// If value is null or empty, return null
				if (value == null || string.IsNullOrEmpty(value.ToString()))
					return null;

				// Get the underlying type of the nullable type
				Type underlyingType = Nullable.GetUnderlyingType(targetType);

				// Convert the value to the underlying type and then convert it to the nullable type
				return Activator.CreateInstance(targetType, Convert.ChangeType(value, underlyingType));
			}
			if (value is IDictionary dictionary)
			{
				return dictionary;

			}
			else if (value is IList list)
			{
				if (value.GetType() == typeof(JArray))
				{
					list = (IList) ((JArray)value).ToObject(targetType);
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
			
			
				// If targetType is not nullable, proceed with the regular conversion
			return Convert.ChangeType(value, targetType);
			
		}

		private object GetNow(string key)
		{

			if (key.Contains("="))
			{
				string[] strings = key.Split("=");
				key = strings[0];
				if (DateTime.TryParse(strings[1].Trim(), out DateTime result))
				{
					return result;
				}

			}
			if (key.Contains("+") && !key.Contains("."))
			{
				string[] strings = key.Split("+");
				string addString = strings[1];
				return CalculateDate("+", addString);
			}
			if (key.Contains("-") && !key.Contains("."))
			{
				string[] strings = key.Split("-");
				string addString = strings[1];
				return CalculateDate("-", addString);
			}
			if (key.Contains("."))
			{
				var objectValue = new ObjectValue(DateTime.UtcNow, typeof(DateTime));
				if (key.Contains("("))
				{
					objectValue = ExecuteMethod(objectValue, key.Substring(key.IndexOf(".") + 1), "Now");
				}
				else
				{
					objectValue = ExecuteProperty(objectValue, key.Substring(key.IndexOf(".") + 1), "Now");
				}

				return objectValue.Value;

			}

			return DateTime.UtcNow;
		}


		private ObjectValue ExecuteProperty(ObjectValue objValue, string propertyDescription, string variableName)
		{
			var obj = objValue.Value;
			if (obj == null) return objValue;

			object? value = null;
			var type = obj.GetType();

			AppContext.TryGetSwitch("builder", out bool isBuilder);

			if (type == typeof(ExpandoObject) || type.Name == "DapperRow")
			{
				var expando = ((IDictionary<string, object>)obj);
				var key = expando.Keys.FirstOrDefault(p => p.ToLower() == propertyDescription.ToLower());
				if (key != null)
				{
					value = expando[key];
				} else
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
				return new ObjectValue(null, null, false);
			}
			var objectValue = new ObjectValue(value, value.GetType());
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

				var objectValue = new ObjectValue(json, json.GetType());
				return objectValue;
			}

			// handle dynamic object Load specially
			if (methodDescription.ToLower() == "load()" && obj is dynamic && obj is IEnumerable enumerable)
			{
				if (enumerable is IDictionary dictionary)
				{
					return new ObjectValue(dictionary, dictionary.GetType());

				}
				else if (enumerable is IList list)
				{
					return new ObjectValue(list, list.GetType());
				}
				else if (enumerable is IEnumerable<KeyValuePair<string, int>> keyValuePairs)
				{
					var dict = keyValuePairs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
					return new ObjectValue(dict, dict.GetType());
				}
				else if (enumerable is IEnumerable<object> objects)
				{
					var list3 = objects.ToList();
					return new ObjectValue(list3, list3.GetType());
				}

				List<object> list2 = new List<object>();
				foreach (var item in enumerable)
				{
					list2.Add(item);
				}
				return new ObjectValue(list2, list2.GetType()); ;
			}

			var methodName = methodDescription.Substring(0, methodDescription.IndexOf("("));
			var paramString = methodDescription.Substring(methodName.Length + 1, methodDescription.Length - methodName.Length - 2);

			var splitParams = paramString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
			List<object> paramValues = new List<object>();
			splitParams.ForEach(p => paramValues.Add(p));

			var type = obj.GetType();
			var methods = GetMethodOnType(type, methodName, paramValues, obj).ToList();


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
					var objectValue = new ObjectValue(obj, obj.GetType());
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

		private IEnumerable<MethodInfo>? GetMethodOnType(Type type, string methodName, List<object> paramValues, object obj)
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
			return methods;

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

		private DateTime CalculateDate(string sign, string command)
		{
			var regex = new Regex(@"^([0-9]+)\s*([a-zA-Z]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
			var matches = regex.Matches(command);
			if (matches.Count == 0 || matches[0].Groups.Count != 3) return DateTime.UtcNow;

			int multiplier = (sign == "+") ? 1 : -1;
			string number = matches[0].Groups[1].Value;
			string function = matches[0].Groups[2].Value;


			if (!int.TryParse(number, out int intValue))
			{
				return DateTime.UtcNow;
			}

			if (function == "micro")
			{
				return DateTime.UtcNow.AddMicroseconds(multiplier * intValue);
			}
			if (function == "ms")
			{
				return DateTime.UtcNow.AddMilliseconds(multiplier * intValue);
			}
			if (function.StartsWith("sec"))
			{
				return DateTime.UtcNow.AddSeconds(multiplier * intValue);
			}
			if (function.StartsWith("min"))
			{
				return DateTime.UtcNow.AddMinutes(multiplier * intValue);
			}
			if (function.StartsWith("hour"))
			{
				return DateTime.UtcNow.AddHours(multiplier * intValue);
			}
			if (function.StartsWith("day"))
			{
				return DateTime.UtcNow.AddDays(multiplier * intValue);
			}
			if (function.StartsWith("month"))
			{
				return DateTime.UtcNow.AddMonths(multiplier * intValue);
			}
			if (function.StartsWith("year"))
			{
				return DateTime.UtcNow.AddYears(multiplier * intValue);
			}


			return DateTime.UtcNow;
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
			var variables = (staticVariable) ? staticVariables : this.variables;
			var objectValue = GetObjectValue(key, staticVariable);
			if (!objectValue.Initiated)
			{
				Put(key, null);
				objectValue = GetObjectValue(key, staticVariable);
			}
			var eve = objectValue.Events.FirstOrDefault(p => p.EventType == VariableEventType.OnCreate && p.goalName == goalName);
			if (eve == null)
			{
				objectValue.Events.Add(new VariableEvent(VariableEventType.OnCreate, goalName, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds));
				variables.AddOrReplace(key, objectValue);
			}
		}
		public void AddOnChangeEvent(string key, string goalName, bool staticVariable = false, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 0)
		{
			var variables = (staticVariable) ? staticVariables : this.variables;
			var objectValue = GetObjectValue(key, staticVariable);
			if (!objectValue.Initiated)
			{
				Put(key, null);
				objectValue = GetObjectValue(key, staticVariable);
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
	}
}
