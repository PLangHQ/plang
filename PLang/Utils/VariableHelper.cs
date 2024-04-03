using Jil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using System.Text.Json;
using System.Text.RegularExpressions;
using static PLang.Services.LlmService.PLangLlmService;
using static PLang.Utils.VariableHelper;

namespace PLang.Utils
{
    public class VariableHelper
	{
		private readonly PLangAppContext context;
		private readonly ISettings settings;
		private readonly MemoryStack memoryStack;
		public VariableHelper(PLangAppContext context, MemoryStack memoryStack, ISettings settings)
		{
			this.context = context;
			this.settings = settings;
			this.memoryStack = memoryStack;
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



		public object? LoadVariables(object? obj, bool emptyIfNotFound = true)
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
					var value = memoryStack.Get(content);
					if (value != null) return value;
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
					if (jsonProperty == null) continue;

					if (jsonProperty.Contains("."))
					{
						var value = (variable.Value == null) ? null : JsonSerialize(variable.Value);
						if (value != null)
						{
							SetNestedPropertyValue(jobject, jsonProperty, value);
						}
					}
					else
					{
						jobject[jsonProperty] = (variable.Value == null) ? "" : JsonSerialize(variable.Value);
					}

				}
				return jobject.ToString();
			}

			foreach (var variable in variables)
			{
				string strValue = "";
				if (variable.Value != null && variable.Value?.GetType() != typeof(string) && !variable.Value!.GetType().IsPrimitive)
				{
					strValue = JsonSerialize(variable.Value).ToString();
				}
				else
				{
					strValue = variable.Value?.ToString() ?? "";
				}
				content = content.Replace(variable.Key, strValue.ToString());
			}
			return content;

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

				var options = new JsonSerializerOptions
				{
					Converters = { new ObjectValueConverter() },

				};
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

				var json = System.Text.Json.JsonSerializer.Serialize(obj, options);
				return JToken.Parse(json);
			}
			catch (InfiniteRecursionException)
			{
				return "Infinite";

			}
			catch (Exception)
			{
				return "[Exception retrieving value]";
			}
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
						System.Text.Json.JsonSerializer.Serialize(writer, ex.Message, options);
					}
					else
					{
						System.Text.Json.JsonSerializer.Serialize(writer, value.Value, options);
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

		public static string? FindPropertyNameByValue(JToken token, string value, string parentPath = "")
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
					throw new Exception($"Variable {variable} not found");
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

		public static bool IsVariable(object variable)
		{
			if (variable == null || string.IsNullOrEmpty(variable.ToString())) return false;
			return Regex.IsMatch(variable.ToString()!, @"^%[a-zA-Z0-9\[\]_\.\+\(\)\*\<\>]*%$");
		}

		public static bool IsSetting(string variableName)
		{
			return variableName.StartsWith("Setting.") || variableName.StartsWith("%Setting.");
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
			var list = new List<ObjectValue>();
			var settingsPattern = @"Settings\.Get\(\\?('|"")(?<key>[^\('|"")]*)\\?('|"")\s*,\s*\\?('|"")(?<default>[^\('|"")]*)\\?('|"")\s*,\s*\\?('|"")(?<explain>[^\('|"")]*)\\?('|"")\)";
			var settingsRegex = new Regex(settingsPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var settingsMatch = settingsRegex.Match(variableName);
			if (settingsMatch.Success)
			{
				var setting = settings.Get<string>(typeof(Settings), settingsMatch.Groups["key"].Value, settingsMatch.Groups["default"].Value, settingsMatch.Groups["explain"].Value);
				list.Add(new ObjectValue(settingsMatch.ToString(), setting, typeof(string)));

			}

			settingsPattern = "%Settings.(?<key>[a-z0-9]*)%|%Setting.(?<key>[a-z0-9]*)%";
			settingsRegex = new Regex(settingsPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var settingsMatches = settingsRegex.Matches(variableName);
			foreach (Match match in settingsMatches)
			{
				var setting = settings.Get<string>(typeof(Settings), match.Groups["key"].Value, "", match.Groups["key"].Value);
				list.Add(new ObjectValue(match.Value, setting, typeof(string)));
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
