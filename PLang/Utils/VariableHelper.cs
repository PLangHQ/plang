using LightInject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using System.Dynamic;
using System.IO;
using System.Text.RegularExpressions;
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

		public Dictionary<string, object> LoadVariables(Dictionary<string, object>? items, bool emptyIfNotFound = true)
		{
			if (items == null) return new Dictionary<string, object>();

			foreach (var item in items)
			{
				items[item.Key] = LoadVariables(item.Value, emptyIfNotFound);
			}
			return items;
		}



		public object? LoadVariables(object obj, bool emptyIfNotFound = true)
		{
			if (obj == null) return null;

			string content = obj.ToString();
			if (Regex.IsMatch(content, @"^%[a-zA-Z0-9\[\]_\.\+]*%$"))
			{
				if (content.StartsWith("%Settings."))
				{
					var vars = GetVariables(content, emptyIfNotFound);
					if (vars.Count == 0) return null;
					return vars[0].Value;
				}
				else
				{
					return memoryStack.Get(content);					
				}
			}

			var variables = GetVariables(content, emptyIfNotFound);
			if (variables.Count == 0) return obj;
			if (obj is JObject jobject)
			{
				foreach (var variable in variables)
				{
					var jsonProperty = FindPropertyNameByValue(jobject, variable.OriginalKey);
					if (jsonProperty != null)
					{
						if (jsonProperty.Contains("."))
						{
							var value = (variable.Value == null) ? null : JToken.FromObject(variable.Value);
							SetNestedPropertyValue(jobject, jsonProperty, value);
						}
						else
						{
							jobject[jsonProperty] = (variable.Value == null) ? "" : JToken.FromObject(variable.Value);
						}
					}
				}
				return jobject.ToString();
			}

			foreach (var variable in variables)
			{
				string strValue = "";
				if (variable.Value != null && variable.Value?.GetType() != typeof(string) && !variable.Value.GetType().IsPrimitive)
				{
					strValue = JsonConvert.SerializeObject(variable.Value);
				} else
				{
					strValue = variable.Value?.ToString() ?? "";
				}
				content = content.Replace(variable.Key, strValue.ToString());
			}
			return content;

		}


		// ... other code ...

		public static void SetNestedPropertyValue(JObject jobject, string path, JToken value)
		{
			string[] parts = path.Split('.');
			JToken current = jobject;

			for (int i = 0; i < parts.Length - 1; i++)
			{
				string part = parts[i];
				if (part.EndsWith("]"))
				{
					var arrayMatch = Regex.Match(part, @"(.+)\[(\d+)\]");
					string arrayName = arrayMatch.Groups[1].Value;
					int arrayIndex = int.Parse(arrayMatch.Groups[2].Value);
					current = current[arrayName][arrayIndex];
				}
				else
				{
					current = current[part];
				}
			}

			string lastPart = parts.Last();
			if (lastPart.EndsWith("]"))
			{
				var arrayMatch = Regex.Match(lastPart, @"(.+)\[(\d+)\]");
				string arrayName = arrayMatch.Groups[1].Value;
				int arrayIndex = int.Parse(arrayMatch.Groups[2].Value);
				((JArray)current[arrayName])[arrayIndex] = value;
			}
			else
			{
				current[lastPart] = value;
			}
		}

		public static string FindPropertyNameByValue(JToken token, string value, string parentPath = "")
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
					if ((string)token == value)
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
		internal List<Variable> GetVariables(string content, bool emptyIfNotFound = true)
		{
			List<Variable> variables = new List<Variable>();

			if (!content.Contains("%")) return variables;

			var pattern = @"(?<!\\)%([^\s%]+|Settings\.Get\((""|')+.*?(""|')+, (""|')+.*?(""|')+, (""|')+.*?(""|')+\))%";

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
					LoadSettings(variables, content);

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

		private void LoadSettings(List<Variable> variables, string content)
		{
			var settingsPattern = @"Settings\.Get\(\\?('|"")(?<key>[^\('|"")]*)\\?('|"")\s*,\s*\\?('|"")(?<default>[^\('|"")]*)\\?('|"")\s*,\s*\\?('|"")(?<explain>[^\('|"")]*)\\?('|"")\)";
			var settingsRegex = new Regex(settingsPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var settingsMatch = settingsRegex.Match(content);
			if (settingsMatch.Success)
			{
				var setting = settings.Get<string>(typeof(Settings), settingsMatch.Groups["key"].Value, settingsMatch.Groups["default"].Value, settingsMatch.Groups["explain"].Value);

				variables.Add(new Variable("%" + settingsMatch.Value + "%", "%" + settingsMatch.Value + "%", setting));
			}

			settingsPattern = "%Settings.(?<key>[a-z0-9]*)%";
			settingsRegex = new Regex(settingsPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var settingsMatches = settingsRegex.Matches(content);
			foreach (Match match in settingsMatches)
			{
				var setting = settings.Get<string>(typeof(Settings), match.Groups["key"].Value, "", match.Groups["key"].Value);

				variables.Add(new Variable("%" + match.Value + "%", "%" + match.Value + "%", setting));
			}
		}

		internal bool IsVariable(object variable)
		{
			return Regex.IsMatch(variable.ToString(), @"^%[a-zA-Z0-9\[\]_\.\+]*%$");
		}
	}
}
