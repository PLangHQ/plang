
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Modules.UiModule;
using PLang.Services.CompilerService;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;
using static PLang.Services.CompilerService.CSharpCompiler;

namespace PLang.Utils.Extractors
{
    public interface IContentExtractor
	{
		public string LlmResponseType { get; set; }
		public object Extract(string content, Type responseType);
		public T Extract<T>(string content);
		string GetRequiredResponse(Type scheme);
	}

	public class TextExtractor : IContentExtractor
	{
		public string LlmResponseType { get { return "text"; } set { } }

		public object Extract(string content, Type responseType)
		{
			return content;
		}

		public T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}

		public string GetRequiredResponse(Type scheme)
		{
			return "";
		}
	}
	public class GenericExtractor : IContentExtractor
	{
		string? type;
		public string LlmResponseType { get { return type; } set { type = value; } }
		public GenericExtractor(string? type)
		{
			this.type = type;
		}

		public T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}
		public object Extract(string content, Type responseType)
		{
			return ExtractByType(content, type);
		}
		public object ExtractByType(string content, string? contentType = null)
		{
			if (contentType == null && content.StartsWith("```"))
			{
				contentType = content.Substring(0, content.IndexOf("\n")).Replace("```", "").Trim();
			}
			if (contentType == null) return content;

			int idx = content.IndexOf($"```{contentType}");
			if (idx != -1)
			{
				var newContent = content.Substring(idx + $"```{contentType}".Length);
				newContent = newContent.Substring(0, newContent.LastIndexOf("```"));
				return newContent;
			}
			return content;
		}

		public string GetRequiredResponse(Type scheme)
		{
			return "Only write the raw " + this.type + " no summary, no extra text to explain, be concise";
		}
	}

	public class HtmlExtractor : GenericExtractor, IContentExtractor
	{
		public HtmlExtractor() : base("html") { }
		public new object Extract(string content, Type responseType)
		{
			var css = ExtractByType(content, "css", true).ToString()?.Trim();
			if (!string.IsNullOrEmpty(css))
			{
				if (css.ToLower().Contains("no css needed"))
				{
					css = "";
				}
				else
				{
					css = "<style>" + css + "</style>\n";
				}
			}
			var html = ExtractByType(content, "html", true).ToString()?.Trim();
			
			var javascript = ExtractByType(content, "javascript", true).ToString()?.Trim();
			if (!string.IsNullOrEmpty(javascript))
			{
				if (javascript.ToLower().Contains("no javascript needed"))
				{
					javascript = "";
				}
				else if (javascript.Contains("function callGoal"))
				{
					javascript = javascript.Replace("function callGoal", "function notcalled_callGoal");
				}
				else
				{
					javascript = "<script>" + javascript + "</script>\n";
				}
			}
			var result = new UiResponse(html, javascript, css);
			return result;
		}

		public object ExtractByType(string content, string contentType = "html", bool returnEmpty = false)
		{
			if (content.Contains($"```{contentType}"))
			{
				var regex = new Regex($"\\`\\`\\`{contentType}([^\\`\\`\\`]*)\\`\\`\\`");
				var match = regex.Match(content);
				if (match.Groups.Count > 1)
				{
					return match.Groups[1].Value ?? "";
				}
			}
			return (returnEmpty) ? "" : content;
		}


		public new string GetRequiredResponse(Type scheme)
		{
			return "Only write the raw ```html, ```css and ```javascript. No summary, no extra text to explain, be concise";
		}
	}

	public class CSharpExtractor : IContentExtractor
	{
		public string LlmResponseType { get => "csharp"; set { } }

		public object Extract(string content, Type responseType)
		{
			var htmlExtractor = new HtmlExtractor();
			var implementation = htmlExtractor.ExtractByType(content, "csharp") as string;
			var json = htmlExtractor.ExtractByType(content, "json");

			var jsonExtractor = new JsonExtractor();
			var jsonObject = jsonExtractor.Extract(json.ToString()!, responseType);

			if (implementation != null && implementation.Contains("System.IO."))
			{
				implementation = implementation.Replace("System.IO.", "PLang.SafeFileSystem.");
			}

			if (responseType == typeof(CodeImplementationResponse))
			{
				var cir = jsonObject as CodeImplementationResponse;
				var ci = new CodeImplementationResponse(cir.Name, implementation, cir.OutParameterDefinition, cir.Using, cir.Assemblies);

				return ci;
			} else
			{
				var cir = jsonObject as ConditionImplementationResponse;
				var ci = new ConditionImplementationResponse(cir.Name, implementation, cir.Using, cir.Assemblies, cir.GoalToCallOnTrue, cir.GoalToCallOnFalse);

				return ci;
			}

			throw new BuilderException($"Response type '{responseType}' is not valid");
		}

			

		public T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}
		
		public string GetRequiredResponse(Type scheme)
		{
			return @$"Only write the raw c# code and json scheme, no summary, no extra text to explain, be concise.
	YOU MUST implement all code needed and valid c# code. 
	You must return ```csharp for the code implementation and ```json scheme: {TypeHelper.GetJsonSchema(scheme)}";
		}
	}

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

		public new object Extract(string content, Type responseType)
		{
			if (responseType == typeof(string)) return content;
			try
			{
				try
				{
					if (content.Trim().Contains("```" + this.LlmResponseType))
					{
						content = ExtractByType(content, "json").ToString()!;
					}
					return JsonConvert.DeserializeObject(content, responseType) ?? "";
				}
				catch
				{
					
					var newContent = FixMalformedJson(content);
					var obj = JsonConvert.DeserializeObject(newContent, responseType);

					//var newJson = JsonConvert.SerializeObject(obj).Replace("[newline]", "\\n").Replace("[carreturn]", "\\r");
					return obj ?? "";
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
							catch
							{
								throw;
							}

						}
					}

					return "";
				}
				catch
				{
					throw;
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
			return $"You MUST respond in JSON, scheme:\r\n {scheme}";
		}
	}
}
