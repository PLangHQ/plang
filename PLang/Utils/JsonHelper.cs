using Newtonsoft.Json;
using System.Text.RegularExpressions;
using PLang.Building.Model;
using System.Text;
using PLang.Utils;
using LightInject;
using System.IO.Abstractions;
using PLang.Interfaces;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using PLang.Errors;
using PLang.Modules.DbModule;

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

			if (filePath == null || !fileSystem.File.Exists(filePath)) return default;

			string content = fileSystem.File.ReadAllText(filePath);
			if (!IsJson(content))
			{
				if (typeof(T) == typeof(string))
				{
					return (T)(object)content;
				}

				return default;
			}

			try
			{
				return JsonConvert.DeserializeObject<T>(content);
			}
			catch (Exception ex)
			{
				return default;
			}
		}


		public static async Task<(bool IsValid, IError? Error)> ValidateSchemaAsync(string schemaJson)
		{
			try
			{
				var schema = await JsonSchema.FromJsonAsync(schemaJson);
				return (true, null);
			}
			catch (Exception ex)
			{
				return (false, new ExceptionError(ex));
			}
		}

	}
}
