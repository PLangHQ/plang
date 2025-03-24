using Jil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using System.Collections;
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
		private readonly PLangAppContext context;
		private readonly ISettings settings;
		private readonly MemoryStack memoryStack;
		private JsonSerializerOptions jsonSerializerOptions;
		public VariableHelper(PLangAppContext context, MemoryStack memoryStack, ISettings settings)
		{
			this.context = context;
			this.settings = settings;
			this.memoryStack = memoryStack;

			jsonSerializerOptions = new JsonSerializerOptions
			{
				Converters = { new ObjectValueConverter() },
				ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles, //.Preserve,
				//DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
				IgnoreReadOnlyProperties = true
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

		public Dictionary<string, object?> LoadVariables(Dictionary<string, object?>? items, bool emptyIfNotFound = true)
		{
			if (items == null) return new Dictionary<string, object?>();

			foreach (var item in items)
			{
				items[item.Key] = LoadVariables(item.Value, emptyIfNotFound);
			}
			return items;
		}



		public object? LoadVariables(object? obj, bool emptyIfNotFound = true, object? defaultValue = null)
		{
			if (obj == null) return null;

			string? content = obj.ToString();
			if (content == null) return null;

			if (IsVariable(content))
			{
				if (content.StartsWith("%Settings."))
				{
					var vars = GetVariables(content, emptyIfNotFound);
					if (vars.Count == 0) return null;
					return vars[0].Value;
				}
				else
				{
					var value = memoryStack.Get(content, false, defaultValue);
					if (value != null)
					{
						if (value is JValue jValue) return jValue.Value;
						return value;
					}
                }
			}

			var variables = GetVariables(content, emptyIfNotFound);
			if (variables.Count == 0) return obj;
			if (obj.ToString().Contains("[*]"))
			{
				obj = JObject.Parse("{}");
			}
			if (obj is JObject jobject)
			{
				foreach (var variable in variables)
				{
					var jsonProperty = FindPropertyNameByValue(jobject, variable.OriginalKey);
					if (jsonProperty == null) {
						LoadVariableInTextValue(jobject, variable, defaultValue);
						continue;
					};

					if (jsonProperty.Contains("."))
					{
						var value = (variable.Value == null) ? null : JsonSerialize(variable.Value);
						if (value != null)
						{
							SetNestedPropertyValue(jobject, jsonProperty, value);
						} else if (defaultValue != null && defaultValue is JToken jToken)
						{
							SetNestedPropertyValue(jobject, jsonProperty, jToken);
						}
					}
					else if (jsonProperty.Contains("[") && jsonProperty.Contains("]"))
					{
						JArray messages = (JArray)jobject[jsonProperty.Substring(0, jsonProperty.IndexOf("["))];
						for (int i = 0; i < messages.Count; i++)
						{
							if (messages[i].Type == JTokenType.String && messages[i].ToString() == variable.OriginalKey)
							{
								if (variable.Value is JToken token) {
									if (!IsEmpty(token))
									{
										messages[i] = token;
									} else if (defaultValue is JToken defaultJValue)
									{
										messages[i] = defaultJValue.SelectToken(token.Path);
									}
								} else
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
						} else
						{
							if (IsEmpty(variable.Value) && defaultValue != null && defaultValue is JToken defaultJValue && jobject[jsonProperty] != null)
							{
								var token = defaultJValue.SelectToken(jobject[jsonProperty].Path) as JValue;
								jobject[jsonProperty] = jobject[jsonProperty].ToString().Replace(variable.OriginalKey, token.ToString());
							}
							else
							{
								jobject[jsonProperty] = jobject[jsonProperty].ToString().Replace(variable.OriginalKey, variable.Value.ToString());
							}
						}
					}

				}
				return jobject;
			}

			if (obj is JArray array)
			{
				foreach (var variable in variables)
				{
					for (int i=0;i<array.Count; i++)
					{
						if (array[i].ToString().Equals(variable.Key, StringComparison.OrdinalIgnoreCase))
						{
							array[i] = variable.Value?.ToString();
						}
					}
				}
				return array;
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
					strValue = variable.Value?.ToString() ?? null;
				}
				

				content = content.Replace(variable.Key, strValue);
			}
			return content;

		}

		private void LoadVariableInTextValue(JToken jobject, Variable variable, object? defaultValue = null)
		{
			var children = jobject.Children();
			foreach (var child in children)
			{
				if (child is JValue jValue)
				{
					jValue.Value = LoadVariables(jValue.Value);
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
					LoadVariableInTextValue(child, variable);
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

			return false;
		}

		public static bool IsRecordType(Type type)
		{
			// Check if the type has the CompilerGeneratedAttribute and a method named "<Clone>$"
			var isCompilerGenerated = type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
			var hasCloneMethod = type.GetMethod("<Clone>$") != null;

			// Optionally, you might also want to check for the existence of EqualityContract
			var hasEqualityContract = type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) != null;

			return isCompilerGenerated && hasCloneMethod && hasEqualityContract;
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
					return JsonConvert.SerializeObject(message);
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
						json = JsonConvert.SerializeObject(obj);
					}
				}
				else
				{
					json = System.Text.Json.JsonSerializer.Serialize(obj, jsonSerializerOptions);
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
					Error = HandleSerializationError
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
					} else
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
						} catch (Exception ex2)
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
					if ((string?)token == value)
					{
						return parentPath;
					} else if (token.ToString().Contains(value))
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


		//%method%%urlPath%%salt%%timestamp%%Settings.RapydApiKey%%Settings.RapydSecretApiKey%%body.ToString().Replace("{}", "").ClearWhitespace()%
		internal List<Variable> GetVariables(string content, bool emptyIfNotFound = true)
		{
			List<Variable> variables = new List<Variable>();

			if (!content.Contains("%")) return variables;

			var pattern = @"(?<!\\)%([^\n\r%]+|Settings\.Get\((""|')+.*?(""|')+, (""|')+.*?(""|')+, (""|')+.*?(""|')+\))%";

			var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var matches = regex.Matches(content);

			foreach (Match match in matches)
			{
				var variable = match.Value;
				if (variables.FirstOrDefault(p => p.Key.ToLower() == variable.ToLower()) != null)
				{
					continue;
				}
				if (variable.StartsWith("%Settings."))
				{
					LoadSetting(variables, variable, content);

					continue;
				}

				var variableValue = memoryStack.Get(variable);

				if (!emptyIfNotFound && variableValue == null)
				{
					throw new VariableDoesNotExistsException($"Variable {variable} not found");
				}
				var content2 = content;
				if (false && JsonHelper.IsJson(content) && variableValue != null && variableValue.GetType() != typeof(string) && !variableValue.GetType().IsPrimitive)
				{
					var json = JsonConvert.SerializeObject(variableValue).Trim();
					// Create the outer JSON structure and inject the serialized JSON
					JObject outerJson = new JObject();
					outerJson["innerJson"] = JValue.FromObject(json);

					// Serialize the outer JSON structure
					string finalJson = JsonConvert.SerializeObject(outerJson["innerJson"]).TrimStart('"').TrimEnd('"');
					variables.Add(new Variable(variable, variable, finalJson));
				}
				else
				{
					variables.Add(new Variable(variable, variable, variableValue ?? null));
				}

			}

			return variables;
		}
		private void LoadSetting(List<Variable> variables, string variable, string content)
		{
			
			var settingsObjects = GetSettingObjectsValue(content);
			foreach (var settingObject in settingsObjects)
			{
				variables.Add(new Variable(settingObject.Name.AsVar(), settingObject.Name.AsVar(), settingObject.Value));
			}
			
		}
		public static bool ContainsVariable(object? variable)
		{
			if (variable == null || string.IsNullOrEmpty(variable.ToString())) return false;
			return Regex.IsMatch(variable.ToString()!, @"%[\p{L}\p{N}#+-\[\]_\.\+\(\)\*\<\>\!\s]*%");
		}
		public static bool IsVariable(object? variable)
		{
			if (variable == null || string.IsNullOrEmpty(variable.ToString())) return false;
			return Regex.IsMatch(variable.ToString()!, @"^%[\p{L}\p{N}#+-\[\]_\.\+\(\)\*\<\>\!\s\""]*%$");
		}

		public static bool IsSetting(string variableName)
		{
			return variableName.StartsWith("Settings.") || variableName.StartsWith("%Settings.");
		}

		internal ObjectValue GetObjectValue(string? variableName, bool staticVariable)
		{
			if (variableName == null) return null;
			if (IsSetting(variableName))
			{
				return GetSettingObjectValue(variableName);
			}
			return memoryStack.GetObjectValue(variableName, staticVariable);
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
				} catch (Exception ex)
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
			return (list.Count > 0) ? list[0]  : null;

		}

	}
}
