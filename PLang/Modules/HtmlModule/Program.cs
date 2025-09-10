using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;

namespace PLang.Modules.HtmlModule
{
	[Description("Parse html text, extract from css selectors")]
	public class Program : BaseProgram
	{

		public Program()
		{
		}

		[Description("Extracts html content based on css selector")]
		public async Task<ObjectValue> Extract(string html, string cssSelector)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var node = doc.DocumentNode.QuerySelector(cssSelector);
			return new HtmlObjectValue("html", node?.InnerHtml?.Trim() ?? string.Empty);
		}

	}
}
