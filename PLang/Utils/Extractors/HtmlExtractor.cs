using PLang.Modules.UiModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PLang.Utils.Extractors
{
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
					if (!javascript.Contains("<script>"))
					{
						javascript = "<script>\n" + javascript + "\n</script>\n";
					}
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
			return "Only write the raw ```code_gen_plan, ```html(optional), ```css(optional) and ```javascript(optional). No summary, no extra text to explain, be concise";
		}
	}
}
