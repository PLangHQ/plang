using Newtonsoft.Json;
using PLang.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PLang.Utils.Extractors
{
	public class JsonExtractor : GenericExtractor, IContentExtractor
	{
		public JsonExtractor() : base("json") { }

		public new T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}

		public static string FixMalformedJson(string json)
		{

			var verbatimStringRegex = new Regex(@"@?""([^""\\]|\\.)*""", RegexOptions.Multiline);

			var newJson = verbatimStringRegex.Replace(json, match =>
			{
				string unescaped = match.Value.Trim();
				if (unescaped.StartsWith("@")) unescaped = unescaped.Substring(1);
				string pattern = @"\\(?!"")(.)";
				unescaped = Regex.Replace(unescaped, pattern, @"\\$1");

				unescaped = unescaped //.Substring(2, match.Value.Length - 3) // Remove leading @ and trailing "
									  //.Replace(@"\", @"\\")
											.Replace("\"\"", "\\\"") // Replace double quotes
											.Replace("\r\n", "\\n")   // Replace newlines
											.Replace("\n", "\\n");     // Replace newlines (alternative format)
				return unescaped; // Add enclosing quotes
			});
			return newJson;
		}

		public new object? Extract(string? content, Type responseType)
		{
			if (string.IsNullOrEmpty(content)) return content;

			try
			{
				try
				{
					if (content.Trim().Contains("```" + this.LlmResponseType))
					{
						content = ExtractByType(content, "json").ToString()!;
					}

					if (responseType == typeof(string)) return content;

					return JsonConvert.DeserializeObject(content, responseType) ?? "";
				}
				catch (Exception ex)
				{

					var newContent = FixMalformedJson(content);
					var obj = JsonConvert.DeserializeObject(newContent, responseType, new JsonSerializerSettings() { });
					if (obj != null) return obj;

					throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", ex);
				}
			}
			catch
			{
				try
				{
					// Use a regular expression to match JSON-like objects
					Regex regex = new Regex(@"\{(?:[^{}]|(?<Level>\{)|(?<-Level>\}))+\}", RegexOptions.Multiline | RegexOptions.Compiled);
					//Regex regex = new Regex(@"(\{.*?\}|\[.*?\])", RegexOptions.Singleline | RegexOptions.Compiled);
					var newContent = FixMalformedJson(content);
					MatchCollection matches = regex.Matches(newContent);
					if (responseType.IsArray)
					{
						StringBuilder sb = new StringBuilder("[");
						foreach (Match match in matches)
						{
							if (match.Success)
							{
								if (sb.Length > 1) sb.Append(",");
								sb.Append(match.Value.ToString());
							}

						}
						sb.Append("]");
						return JsonConvert.DeserializeObject(sb.ToString(), responseType) ?? "";
					}

					foreach (Match match in matches)
					{
						if (match.Success)
						{
							try
							{
								return JsonConvert.DeserializeObject(match.Value.ToString(), responseType) ?? "";
							}
							catch (Exception ex)
							{
								throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", ex);
							}

						}
					}

					throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", new Exception("Error parsing content to json"));
				}
				catch (Exception ex2)
				{
					throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", ex2);
				}
			}
		}

		public new string GetRequiredResponse(Type type)
		{
			string strScheme = TypeHelper.GetJsonSchema(type);
			return GetRequiredResponse(strScheme);
		}

		public new string GetRequiredResponse(string scheme)
		{
			return $"You MUST respond in JSON, scheme:\r\n {scheme.Replace("\n", " ")}";
		}
	}
}
