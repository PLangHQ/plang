﻿
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
			if (!string.IsNullOrEmpty(this.type))
			{
				return "Do NOT write summary, no extra text to explain, be concise. Only write the raw response in ```" + this.type;
			}
			return "";
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
				if (!content.TrimEnd().EndsWith("```"))
				{
					content += "```";
				}
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
				var ci = new CodeImplementationResponse(cir.Namespace, cir.Name, implementation, cir.InputParameters, cir.OutParameters, cir.Using, cir.Assemblies);

				return ci;
			} else
			{
				var cir = jsonObject as ConditionImplementationResponse;
				var ci = new ConditionImplementationResponse(cir.Namespace, cir.Name, implementation, cir.InputParameters, cir.Using, cir.Assemblies, cir.GoalToCallOnTrue, cir.GoalToCallOnFalse, cir.GoalToCallOnTrueParameters, cir.GoalToCallOnFalseParameters);

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

	
}
