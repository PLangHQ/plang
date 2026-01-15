using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using System.IO.Abstractions;

namespace PLang.Utils
{
	public class StringHelper
	{
		private static JsonSerializerSettings jsonSerializer = new JsonSerializerSettings()
		{
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			Converters = { new JsonObjectValueConverter() }
		};

		public static string? ConvertToString(object? body)
		{
			if (body == null) return "";
			if (body is string str) return str;
			if (body is JToken jToken) return jToken.ToString(Newtonsoft.Json.Formatting.None);// body.ToString();

			if (IsToStringOverridden(body)) return body.ToString();

			try
			{
				var str2 = (body.ToString() ?? "").TrimStart();
				if (str2.StartsWith("{") || str2.StartsWith("[")) {
					JsonConvert.DeserializeObject(str2);
					return str2;
				} else
				{
					return JsonConvert.SerializeObject(body, jsonSerializer);
				}
			}
			catch
			{
				try
				{
					return JsonConvert.SerializeObject(body, jsonSerializer);
				}
				catch
				{
					return body.ToString() ?? "";
				}
			}
		}

		public static bool IsToStringOverridden(object obj)
		{
			var method = obj.GetType().GetMethod("ToString", Type.EmptyTypes);
			return method?.DeclaringType != typeof(object);
		}


		public static string CreateSignatureData(string method, string url, long created, string nonce, string body, string contract = "C0")
		{
			// url, created, body, content type, method, nonce
			var data = new Dictionary<string, object>
					{
						{ "Method", method },
						{ "Url", url },
						{ "Created", created },
						{ "Nonce", nonce },
						{ "Body", body },
						{ "Contract", contract },
					};

			return JsonConvert.SerializeObject(data);
		}


		public static string NormalizeCacheKey(string cacheKey)
		{
			if (string.IsNullOrEmpty(cacheKey))
				return "empty_key";

			// Replace common URL characters with safe alternatives
			var normalized = cacheKey
				.Replace("://", "_")
				.Replace("/", "_")
				.Replace("\\", "_")
				.Replace("?", "_")
				.Replace("&", "_")
				.Replace("=", "_")
				.Replace(":", "_")
				.Replace("*", "_")
				.Replace("\"", "_")
				.Replace("<", "_")
				.Replace(">", "_")
				.Replace("|", "_");

			// Remove consecutive underscores
			while (normalized.Contains("__"))
				normalized = normalized.Replace("__", "_");

			// Trim underscores from start/end
			normalized = normalized.Trim('_');

			// Limit length (Windows max path component is 255)
			if (normalized.Length > 200)
				normalized = normalized.Substring(0, 200);

			return normalized;
		}


	}
}
