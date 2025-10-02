using Jil;
using Nethereum.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Services.SettingsService;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using static PLang.Utils.VariableHelper;

namespace PLang.Utils
{
	public class VariableHelper
	{
		private readonly ISettings settings;
		private readonly ILogger logger;
		private JsonSerializerOptions jsonSerializerOptions;
		private JsonSerializerSettings jsonSerializerSettings;
		public VariableHelper(ISettings settings, ILogger logger)
		{
			this.settings = settings;
			this.logger = logger;

			jsonSerializerOptions = new JsonSerializerOptions
			{
				Converters = { new ObjectValueConverter() },
				ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles, //.Preserve,
																								 //DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
				IgnoreReadOnlyProperties = true
			};

			jsonSerializerSettings = new JsonSerializerSettings
			{
				Converters = { new JsonObjectValueConverter() },
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			};
		}

		public static bool IsEmpty(object? value)
		{
			if (value == null) return true;
			if (value is string str && string.IsNullOrWhiteSpace(str)) return true;
			if (value is JToken token && (
				   token.Type == JTokenType.Null || // JSON null
				   (token.Type == JTokenType.Object && !token.HasValues) ||
				   (token.Type == JTokenType.Array && !token.HasValues) ||
				   (token.Type == JTokenType.String && string.IsNullOrEmpty(token.ToString())) ||
				   (token.Type == JTokenType.Property && ((JProperty)token).Value == null))) return true;
			if (value is IList list && list.Count == 0) return true;
			if (value is IDictionary dict && dict.Count == 0) return true;

			return false;
		}

		public Dictionary<string, object?> LoadVariables(MemoryStack memoryStack, Dictionary<string, object?>? items, bool emptyIfNotFound = true)
		{
			if (items == null) return new Dictionary<string, object?>();

			foreach (var item in items)
			{
				items[item.Key] = LoadVariables(memoryStack, item.Value, emptyIfNotFound);
			}
			return items;
		}



		public object? LoadVariables(MemoryStack memoryStack, object? obj, bool emptyIfNotFound = true, object? defaultValue = null)
		{
			if (obj == null) return null;
			if (obj.GetType().IsPrimitive) return obj;

			Stopwatch stopwatch = Stopwatch.StartNew();
			if (obj is string variableName)
			{
				if (!variableName.Contains("%")) return obj;
				if (IsVariable(variableName))
				{

					logger.LogDebug($"           - Loading {variableName} - {stopwatch.ElapsedMilliseconds}");
					if (variableName.StartsWith("%Settings."))
					{
						var vars = GetVariables(variableName, memoryStack, emptyIfNotFound);
						if (vars.Count == 0) return null;
						return vars[0].Value;
					}
					else
					{
						var value = memoryStack.Get(variableName, false, defaultValue);
						logger.LogDebug($"           - Have variable {variableName} - {stopwatch.ElapsedMilliseconds}");
						return value;
					}
				}
			}


			string? content = obj.ToString();
			if (content == null) return null;

			var variables = GetVariables(content, memoryStack, emptyIfNotFound);
			if (variables.Count == 0) return obj;

			if (TypeHelper.IsRecordType(obj))
			{
				return LoadVariablesToRecord(obj, variables, defaultValue, memoryStack);
			}

			if (obj.ToString().Contains("[*]"))
			{
				obj = JObject.Parse("{}");
			}
			if (obj is JObject jobject)
			{
				return LoadVariablesToJObject(jobject, variables, defaultValue, memoryStack);
			}

			if (obj is JArray array)
			{
				return LoadVariablesToJArray(array, variables, defaultValue);

			}
			if (variables.Count == 1 && IsVariable(content)) return variables[0].Value;

			foreach (var variable in variables)
			{
				string? strValue = null;
				if (variable.Value != null && ShouldSerializeToText(variable.Value))
				{
					strValue = JsonSerialize(variable.Value).ToString();
				}
				else
				{
					strValue = Convert.ToString(variable.Value, CultureInfo.InvariantCulture);
					//strValue = variable.Value?.ToString() ?? null;
				}


				content = content.Replace(variable.PathAsVariable, strValue, StringComparison.OrdinalIgnoreCase);
			}
			return content;

		}

		private object? LoadVariablesToJArray(JArray incomingArray, List<ObjectValue> variables, object? defaultValue)
		{
			var array = incomingArray.DeepClone() as JArray;

			foreach (var variable in variables)
			{
				for (int i = 0; i < array.Count; i++)
				{
					if (array[i].ToString().Equals(variable.PathAsVariable, StringComparison.OrdinalIgnoreCase))
					{
						array[i] = variable.Value?.ToString();
					}
				}
			}
			return array;
		}

		private object? LoadVariablesToJObject(JObject incomingJObject, List<ObjectValue> variables, object? defaultValue, MemoryStack memoryStack)
		{
			var jobject = incomingJObject.DeepClone() as JObject;
			int refillJObjectCounter = 0;
			for (int c=0;c<variables.Count;c++)
			{
				var variable = variables[c];

				var jsonProperty = FindPropertyNameByValue(jobject, variable.PathAsVariable);
				if (jsonProperty == null)
				{
					LoadVariableInTextValue(jobject, variable, memoryStack, defaultValue);
					continue;
				}

				if (jsonProperty.Contains("."))
				{
					var value = (variable.Value == null) ? null : JsonSerialize(variable.Value);
					if (value == null && defaultValue is JToken jToken) value = jToken;

					SetNestedPropertyValue(jobject, jsonProperty, value);

				}
				else if (jsonProperty.Contains("[") && jsonProperty.Contains("]"))
				{
					JArray messages = (JArray)jobject[jsonProperty.Substring(0, jsonProperty.IndexOf("["))];
					for (int i = 0; i < messages.Count; i++)
					{
						if (messages[i].Type == JTokenType.String && messages[i].ToString() == variable.PathAsVariable)
						{
							if (variable.Value is JToken token)
							{
								if (!IsEmpty(token))
								{
									messages[i] = token;
								}
								else if (defaultValue is JToken defaultJValue)
								{
									messages[i] = defaultJValue.SelectToken(token.Path);
								}
							}
							else
							{
								if (!IsEmpty(variable.Value))
								{
									messages[i] = JsonSerialize(variable.Value);
								}
								else if (defaultValue != null)
								{
									messages[i] = JsonSerialize(defaultValue);
								}
							}
							break;
						}
					}

				}
				else if (jobject[jsonProperty] != null)
				{
					if (jobject[jsonProperty] != null && IsVariable(jobject[jsonProperty].ToString()))
					{
						if (IsEmpty(variable.Value) && defaultValue != null && defaultValue is JToken defaultJValue && jobject[jsonProperty] != null)
						{
							var value = defaultJValue.SelectToken(jobject[jsonProperty].Path) as JValue;
							jobject[jsonProperty] = value;
						}
						else
						{
							jobject[jsonProperty] = (variable.Value == null) ? null : JsonSerialize(variable.Value);
						}
					}
					else
					{
						if (IsEmpty(variable.Value) && defaultValue != null && defaultValue is JToken defaultJValue && jobject[jsonProperty] != null)
						{
							var token = defaultJValue.SelectToken(jobject[jsonProperty].Path) as JValue;
							jobject[jsonProperty] = jobject[jsonProperty].ToString().Replace(variable.PathAsVariable, token.ToString());
						}
						else
						{
							jobject[jsonProperty] = jobject[jsonProperty].ToString().Replace(variable.PathAsVariable, variable.Value.ToString());
						}
					}
				}

				// when jobject contains variable multiple time, then repeat the search
				// on the same variable, so do c--, this is not ideal implementation
				if (jobject.ToString().Contains(variable.PathAsVariable))
				{
					refillJObjectCounter++;
					if (refillJObjectCounter < 20)
					{
						c--;
					} else
					{
						throw new Exception($"Could not fill jobject for {variable.PathAsVariable}. Object is: {jobject.ToString()}");
					}
				}

			}
			return jobject;
		}

		private object? LoadVariablesToRecord(object obj, List<ObjectValue> variables, object? defaultValue, MemoryStack memoryStack)
		{
			var type = obj.GetType();
			var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			var values = new object?[props.Length];

			Type? convertValueTo = null;
			for (int i = 0; i < props.Length; i++)
			{
				var value = props[i].GetValue(obj);

				if (value is string str)
				{
					if (convertValueTo == null && str.Contains(".") && props[i].Name.Equals("TypeFullName") || props[i].Name.Equals("Type"))
					{
						convertValueTo = Type.GetType(str, false);
					}

					if (IsVariable(str))
					{
						var variable = variables.FirstOrDefault(p => p.PathAsVariable.Equals(str, StringComparison.OrdinalIgnoreCase));
						var valueToSet = (variable != null) ? variable.Value : memoryStack.LoadVariables(str);
						if (convertValueTo != null)
						{
							values[i] = TypeHelper.ConvertToType(valueToSet, convertValueTo);
						}
						else
						{
							values[i] = valueToSet;
						}
					}
					else if (str.Contains("%"))
					{
						var varValue = memoryStack.LoadVariables(str);
						values[i] = varValue;
					}
					else
					{
						values[i] = str;
					}
				}
				else
				{
					values[i] = value;
				}
			}

			var ctor = type.GetConstructors().First();
			return ctor.Invoke(values);
		}

		private void LoadVariableInTextValue(JToken jobject, ObjectValue variable, MemoryStack memoryStack, object? defaultValue = null)
		{
			var children = jobject.Children();
			for (int b=0;b< children.Count();b++)
			{
				var child = children.ElementAt(b);
			
				if (child is JValue jValue)
				{
					var obj = memoryStack.LoadVariables(jValue.Value);
					if (obj != null)
					{
						try
						{
							var serializer = new Newtonsoft.Json.JsonSerializer
							{
								ReferenceLoopHandling = ReferenceLoopHandling.Ignore
							};

							JToken replacement = JToken.FromObject(obj, serializer);
							jValue.Replace(replacement);
						}
						catch (Exception ex)
						{
							int c = 0;
						}
					}
					else
					{
						jValue.Value = null;
					}

					if (IsEmpty(jValue.Value) && defaultValue is JObject defaultJObj)
					{
						var token = defaultJObj.SelectToken(jValue.Path);
						if (token is JValue defaultJValue)
						{
							jValue.Value = defaultJValue;
						}
					}
				}
				else
				{
					LoadVariableInTextValue(child, variable, memoryStack: memoryStack);
				}
				int i = 0;
			}
		}


		private bool ShouldSerializeToText(object value)
		{
			if (value is string) return false;
			if (value.GetType().IsPrimitive) return false;

			string strValue = value.ToString().TrimEnd(']') ?? "";
			string fullName = value.GetType().FullName ?? "";

			if (value is IDictionary || value is IList) return true;
			if (strValue.Equals(fullName)) return true;
			if (TypeHelper.IsRecordWithToString(value)) return false;

			return false;
		}




		public JToken JsonSerialize(object? obj)
		{
			if (obj == null) return "";

			if (obj is string str)
			{
				if (JsonHelper.IsJson(obj, out object? parsedObj) && parsedObj != null)
				{
					var token = parsedObj as JToken;
					if (token != null) return token;
				}
				return str;
			}

			if (obj is JArray jarray) return jarray;
			if (obj is JObject jobject) return jobject;
			if (obj is JToken jtoken) return jtoken;

			try
			{


				// TODO: Not sure how to solve this ugly code. 

				// doing this bad code, because Newtonsoft give stackoverflow on objects
				// e.g. when getting file info, then when it tries to serialize it crashes app.
				// .net json serializer can survive it, so that is why this is like this :(
				if (obj is Exception ex)
				{
					string message = ex.Message;
					if (JsonHelper.IsJson(message)) return message;
					return JsonConvert.SerializeObject(message, jsonSerializerSettings);
				}
				string json;

				if (obj is System.Collections.IList list)
				{
					if (list.Count > 0 && list[0] is JProperty)
					{
						json = JsonConvert.SerializeObject(new JObject(obj));
					}
					else
					{
						json = JsonConvert.SerializeObject(obj, jsonSerializerSettings);
					}
				}
				else
				{
					try
					{
						json = JsonConvert.SerializeObject(obj, jsonSerializerSettings);
					}
					catch
					{
						json = System.Text.Json.JsonSerializer.Serialize(obj, jsonSerializerOptions);
					}
				}
				return JToken.Parse(json);
			}
			catch (InfiniteRecursionException)
			{
				return "Infinite";

			}
			catch (Exception)
			{
				var settings = new JsonSerializerSettings
				{
					Error = HandleSerializationError,
					Converters = { new JsonObjectValueConverter() }
				};

				return JsonConvert.SerializeObject(obj, settings);


				return "Exception retrieving value";
			}
		}

		private void HandleSerializationError(object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
		{
			e.ErrorContext.Handled = true;
		}


		public class ObjectValueConverter : System.Text.Json.Serialization.JsonConverter<ObjectValue>
		{
			public override ObjectValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				// Implement deserialization if necessary
				throw new NotImplementedException();
			}

			public override void Write(Utf8JsonWriter writer, ObjectValue value, JsonSerializerOptions options)
			{
				writer.WriteStartObject();
				writer.WriteString("Type", value.Type?.Name);
				// Serialize other properties
				writer.WriteBoolean("Initiated", value.Initiated);


				if (value.Value is JArray jarray || value.Value is JObject jobject || value.Value is JToken jtoken)
				{

					writer.WritePropertyName("Value");
					if (value.Value is JValue || value.Value is JProperty)
					{
						writer.WriteStringValue(value.Value.ToString());
					}
					else
					{
						writer.WriteRawValue(value.Value.ToString());
					}


				}
				else
				{
					writer.WritePropertyName("Value");
					if (value.Value is Exception ex)
					{
						throw new Exception(ex.Message, ex);
						System.Text.Json.JsonSerializer.Serialize(writer, ex.Message, options);
					}
					else
					{
						try
						{
							System.Text.Json.JsonSerializer.Serialize(writer, value.Value, options);
						}
						catch (Exception ex2)
						{
							var settings = new JsonSerializerSettings
							{
								Error = HandleSerializationError
							};

							void HandleSerializationError(object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
							{
								e.ErrorContext.Handled = true;
							}
							throw;
							value.Value = JsonConvert.SerializeObject(ex2, settings);
						}
					}
				}
				writer.WriteEndObject();
			}
		}

		public static void SetNestedPropertyValue(JObject jobject, string path, JToken value)
		{
			string[] parts = path.Split('.');
			JToken? current = jobject;

			for (int i = 0; i < parts.Length - 1; i++)
			{
				if (current == null) continue;

				string part = parts[i];
				if (part.EndsWith("]"))
				{
					var arrayMatch = Regex.Match(part, @"(.+)\[(\d+)\]");
					string arrayName = arrayMatch.Groups[1].Value;
					int arrayIndex = int.Parse(arrayMatch.Groups[2].Value);
					if (current[arrayName] != null)
					{
						current = current[arrayName]?[arrayIndex];
					}
				}
				else
				{
					current = current[part];
				}
			}
			if (current == null) return;

			string lastPart = parts.Last();
			if (lastPart.EndsWith("]"))
			{
				var arrayMatch = Regex.Match(lastPart, @"(.+)\[(\d+)\]");
				string arrayName = arrayMatch.Groups[1].Value;
				int arrayIndex = int.Parse(arrayMatch.Groups[2].Value);
				if (current[arrayName] != null)
				{
					var obj = (JArray?)current[arrayName];
					if (obj != null)
					{
						obj[arrayIndex] = value;
					}
				}
			}
			else
			{
				current[lastPart] = value;
			}
		}

		public string? FindPropertyNameByValue(JToken token, string value, string parentPath = "")
		{
			switch (token.Type)
			{
				case JTokenType.Object:
					foreach (var property in token.Children<JProperty>())
					{
						// Construct the path for nested properties
						string currentPath = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}.{property.Name}";
						var result = FindPropertyNameByValue(property.Value, value, currentPath);
						if (result != null)
						{
							return result;
						}
					}
					break;
				case JTokenType.Array:
					int index = 0;
					foreach (var item in token.Children())
					{
						var result = FindPropertyNameByValue(item, value, $"{parentPath}[{index}]");
						if (result != null)
						{
							return result;
						}
						index++;
					}
					break;
				case JTokenType.String:
					if (token.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
					{
						return parentPath;
					}
					else if (token.ToString().Contains(value))
					{
						//return parentPath;
					}
					break;
			}
			return null;
		}
		/*
				public static string FindPropertyNameByValue(JObject jObj, string value)
				{
					foreach (var property in jObj.Properties())
					{
						if (property.Value.Type == JTokenType.String && (string)property.Value == value)
						{
							return property.Name;
						}
					}
					return null;  // or throw an exception if you prefer
				}*/


		public record Variable(string OriginalKey, string Key, object? Value);

		public static List<string> GetVariablesInText(string content)
		{
			if (!content.Contains("%")) return new();

			var pattern = @"(?<!\\)%([^\n\r%]+|Settings\.Get\((""|')+.*?(""|')+, (""|')+.*?(""|')+, (""|')+.*?(""|')+\))%";

			var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var matches = regex.Matches(content);
			List<string> vars = new();
			foreach (Match match in matches)
			{
				vars.Add(match.Value);
			}
			return vars;
		}


		internal List<ObjectValue> GetVariables(string content, MemoryStack memoryStack, bool emptyIfNotFound = true)
		{
			List<ObjectValue> variables = new List<ObjectValue>();

			var varsList = GetVariablesInText(content);
			if (varsList.Count == 0) return variables;

			if (!content.Contains("%")) return variables;

			foreach (string variable in varsList)
			{
				if (variables.FirstOrDefault(p => p.PathAsVariable.Equals(variable, StringComparison.OrdinalIgnoreCase)) != null)
				{
					continue;
				}
				if (variable.StartsWith("%Settings."))
				{
					LoadSetting(variables, variable, content);

					continue;
				}

				var ov = memoryStack.GetObjectValue(variable);

				if (!ov.Initiated)
				{
					AppContext.TryGetSwitch("Builder", out bool isBuilder);
					if (isBuilder) ov = ObjectValue.Nullable(variable);

					//throw new VariableDoesNotExistsException($"Variable {variable} not found");
				}
				variables.Add(ov);
			}

			return variables;
		}
		private void LoadSetting(List<ObjectValue> variables, string variable, string content)
		{
			var settingsObjects = GetSettingObjectsValue(content);
			variables.AddRange(settingsObjects);
		}
		public static bool ContainsVariable(object? variable)
		{
			if (variable is not string str) return false;
			if (variable == null || string.IsNullOrEmpty(str)) return false;
			return Regex.IsMatch(str, @"%[\p{L}\p{N}#+-\[\]_\.\+\(\)\*\<\>\!\s]*%");
		}
		public static bool IsVariable(object? variable)
		{
			if (variable is not string str) return false;

			if (str == null || string.IsNullOrEmpty(str)) return false;
			return Regex.IsMatch(str, @"^%[\p{L}\p{N}#+-\[\]_\.\+\(\)\*\<\>\!\s\""]*%$", RegexOptions.Compiled);
		}

		public static bool IsSetting(string variableName)
		{
			return variableName.StartsWith("Settings.") || variableName.StartsWith("%Settings.");
		}

		public List<ObjectValue> GetSettingObjectsValue(string variableName)
		{
			string[] variableNames = [variableName];
			if (JsonHelper.IsJson(variableName))
			{
				try
				{
					if (variableName.TrimStart().StartsWith("[") && variableName.TrimEnd().EndsWith("]"))
					{
						variableNames = JArray.Parse(variableName).ToObject<string[]>();
					}
				}
				catch (Exception ex)
				{
					throw;
				}
			}

			var list = new List<ObjectValue>();
			foreach (var varName in variableNames)
			{

				var settingsPattern = @"Settings\.Get\(\\?('|"")(?<key>[^\('|"")]*)\\?('|"")\s*,\s*\\?('|"")(?<default>[^\('|"")]*)\\?('|"")\s*,\s*\\?('|"")(?<explain>[^\('|"")]*)\\?('|"")\)";
				var settingsRegex = new Regex(settingsPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
				var settingsMatch = settingsRegex.Match(varName);
				if (settingsMatch.Success)
				{
					var setting = settings.Get<string>(typeof(Settings), settingsMatch.Groups["key"].Value, settingsMatch.Groups["default"].Value, settingsMatch.Groups["explain"].Value);
					list.Add(new ObjectValue(settingsMatch.ToString(), setting, typeof(string)));

				}

				settingsPattern = "%Settings.(?<key>[a-z0-9]*)%|%Setting.(?<key>[a-z0-9]*)%";
				settingsRegex = new Regex(settingsPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
				var settingsMatches = settingsRegex.Matches(varName);
				foreach (Match match in settingsMatches)
				{
					var setting = settings.Get<string>(typeof(Settings), match.Groups["key"].Value, "", match.Groups["key"].Value);
					list.Add(new ObjectValue(match.Value, setting, typeof(string)));
				}
			}
			return list;
		}
		public ObjectValue? GetSettingObjectValue(string variableName)
		{
			var list = GetSettingObjectsValue(variableName);
			return (list.Count > 0) ? list[0] : null;

		}
		/*
		public object? GetValue(string? variableName, Type parameterType)
		{
			if (!IsVariable(variableName)) { return variableName; }

			var objectValue = memoryStack.Get(variableName!, parameterType);
			return objectValue;
		}*/


		public static string Clean(string str)
		{
			if (string.IsNullOrEmpty(str)) return "";

			str = str.Trim();
			bool isVariable = IsVariable(str);

			if (isVariable && str.StartsWith("%")) str = str.Substring(1);
			if (isVariable && str.EndsWith("%")) str = str.Remove(str.Length - 1);
			if (str.StartsWith("@")) str = str.Substring(1);
			if (str.StartsWith("$.")) str = str.Remove(0, 2);
			str = str.TrimEnd('+').TrimEnd('-');
			str = Regex.Replace(str, "α([0-9]+)α", match => $"[{match.Groups[1].Value}]");

			return str.Replace("α", ".");
		}
	}
}
