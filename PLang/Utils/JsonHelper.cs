using Jil;
using LightInject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules.DbModule;
using PLang.Utils;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PLang.Utils
{
	public class JsonHelper
	{

		public static T TryParse<T>(string content)
		{
			if (IsJson(content))
			{
				var obj = JsonConvert.DeserializeObject<T>(content);
				if (obj != null) return obj;
			}

			return (T)Convert.ChangeType(content, typeof(T));


		}

		public static bool LookAsJsonScheme(string content)
		{
			if (string.IsNullOrEmpty(content)) return false;
			content = content.Trim();
			return (content.StartsWith("{") && content.EndsWith("}")) || (content.StartsWith("[") && content.EndsWith("]"));
		}
		public static bool IsJson(object? obj)
		{
			return IsJson(obj, out object? _);
		}
		public static bool IsJson(object? obj, out object? parsedObject)
		{
			parsedObject = null;

			if (obj == null) return false;
			if (obj is not string) return false;
			if (obj is Table || obj is Row) return false;
			if (obj.GetType().Name.StartsWith("<>f__Anonymous")) return false;


			string content = obj.ToString()!;

			content = content.Trim();
			var result = (content.StartsWith("{") && content.EndsWith("}")) || (content.StartsWith("[") && content.EndsWith("]"));
			if (!result) return false;


			try
			{
				parsedObject = JsonConvert.DeserializeObject(content);
				return true;
			}
			catch
			{
				try
				{
					string json = content.Replace("\n", " ").Replace("\r", " ");
					parsedObject = JsonConvert.DeserializeObject(json);
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		public static T? ParseFilePath<T>(IPLangFileSystem fileSystem, string? filePath)
		{

			if (string.IsNullOrEmpty(filePath) || !fileSystem.File.Exists(filePath)) return default;

			string content = fileSystem.File.ReadAllText(filePath);

			try
			{
				return JsonConvert.DeserializeObject<T>(content);
			}
			catch (Exception ex)
			{
				throw;
			}
		}

		public static string ToStringIgnoreError(object? obj)
		{
			if (obj == null) return "";
			var customSettings = new JsonSerializerSettings
			{
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
				MaxDepth = 5,
				Error = HandleSerializationError,
				Converters = new List<JsonConverter>(),
				NullValueHandling = NullValueHandling.Include,
				Formatting = Formatting.None
			};
			return JsonConvert.SerializeObject(obj, customSettings);
		}
		private static void HandleSerializationError(object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
		{
			e.ErrorContext.Handled = true;
		}


		public static (bool IsValid, IError? Error) ValidateJson(string schemaJson)
		{
			try
			{
				JsonDocument.Parse(schemaJson);
				//var schema = await JsonSchema.FromJsonAsync(schemaJson);
				return (true, null);
			}
			catch (Exception ex)
			{
				return (false, new ExceptionError(ex));
			}
		}

		public static IEnumerable<JToken> FindTokens(JToken root, string propertyName, string valueToMatch, bool returnParent = false)
		{
			switch (root.Type)
			{
				case JTokenType.Object:
					{
						var obj = (JObject)root;

						if (obj.TryGetValue(propertyName, out var prop) &&
							prop.Type == JTokenType.String &&
							prop.Value<string>() == valueToMatch)
							yield return returnParent ? obj : prop;

						foreach (var child in obj.Properties())
							foreach (var hit in FindTokens(child.Value, propertyName, valueToMatch, returnParent))
								yield return hit;

						break;
					}

				case JTokenType.Array:
					{
						foreach (var item in root.Children())
							foreach (var hit in FindTokens(item, propertyName, valueToMatch, returnParent))
								yield return hit;
						break;
					}
			}
		}

	}
}
