using Newtonsoft.Json;
using System.Text.RegularExpressions;
using PLang.Building.Model;
using System.Text;
using PLang.Utils;
using LightInject;
using System.IO.Abstractions;
using PLang.Interfaces;
using System.Text.Json;

namespace PLang.Utils
{
	public class JsonHelper
	{

		public static T TryParse<T>(string content)
		{
			if (IsJson(content))
			{
				return JsonConvert.DeserializeObject<T>(content);
			} else
			{
				return (T)Convert.ChangeType(content, typeof(T));
			}
		
		}

		public static bool LookAsJsonScheme(string content)
		{
			content = content.Trim();
			return (content.StartsWith("{") && content.EndsWith("}")) || (content.StartsWith("[") && content.EndsWith("]"));
		}
		public static bool IsJson(object? obj)
		{
			return IsJson(obj, out object _);
		}
		public static bool IsJson(object? obj, out object? parsedObject)
		{
			parsedObject = null;

			if (obj == null) return false;
			string content = obj.ToString();

			content = content.Trim();
			var result = (content.StartsWith("{") && content.EndsWith("}")) || (content.StartsWith("[") && content.EndsWith("]"));
			if (!result) return false;

			try
			{
				parsedObject = JsonDocument.Parse(content);
				return true;
			} catch
			{
				return false;
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
			} catch (Exception ex) 
			{
				return default;
			}
		}

	}
}
