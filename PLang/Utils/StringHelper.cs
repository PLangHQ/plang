using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Abstractions;

namespace PLang.Utils
{
	public class StringHelper
	{
		public static string? ConvertToString(object? body)
		{
			if (body == null) return "";
			if (body is string str) return str;
			if (body is JToken jToken) return jToken.ToString(Newtonsoft.Json.Formatting.None);// body.ToString();

			if (IsToStringOverridden(body)) return body.ToString();

			try
			{
				JsonConvert.DeserializeObject(body.ToString()!);
				return body.ToString()!;
			}
			catch
			{
				try
				{
					return JsonConvert.SerializeObject(body);
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

		
	}
}
