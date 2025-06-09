using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using PLang.Errors;
using PLang.Models.ObjectTypes;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	public class HtmlExtractor : IExtractor
	{
		private object value;
		private ObjectValue objectValue;

		public HtmlExtractor(object value, ObjectValue objectValue)
		{
			this.value = value;
			this.objectValue = objectValue;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			string xpath = PatternToXPath(segment.Value);
			if (value is HtmlType htmlType)
			{
				var doc = new HtmlDocument();
				doc.LoadHtml(htmlType.ToString());
				var nodes = doc.DocumentNode.SelectNodes(xpath);
				return new HtmlObjectValue(segment.Value, nodes);
			}
			else if (value is HtmlNodeCollection nodes)
			{
				var nextNodes = nodes
					.SelectMany(n => n.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>())
					.ToList();
				return new HtmlObjectValue(segment.Value, nextNodes);
			}
			else if (value is IList<HtmlNode> htmlNodes)
			{
				var nextNodes = htmlNodes
					.SelectMany(n => n.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>())
					.ToList();
				return new HtmlObjectValue(segment.Value, nextNodes);
			}

			throw new NotImplementedException($"{value.GetType()} {ErrorReporting.CreateIssueNotImplemented}");
		}


		public string PatternToXPath(string pattern)
		{
			var segments = pattern.Split('.', StringSplitOptions.RemoveEmptyEntries);
			var xpath = new StringBuilder();

			foreach (var seg in segments)
			{
				string node = seg, id = null, attrPart = null;

				// Attribute parsing
				var attrStart = seg.IndexOf('[');
				if (attrStart >= 0)
				{
					node = seg.Substring(0, attrStart);
					attrPart = seg.Substring(attrStart + 1, seg.Length - attrStart - 2); // Remove '[' and ']'
				}

				// Id parsing
				var idSplit = node.Split('#', 2);
				node = idSplit[0];
				if (idSplit.Length > 1)
					id = idSplit[1];

				// Start XPath segment
				if (xpath.Length == 0)
					xpath.Append("//" + node);
				else
					xpath.Append("/" + node);

				// Id
				if (id != null)
					xpath.Append($"[@id='{id}']");

				// Attributes
				if (!string.IsNullOrEmpty(attrPart))
				{
					var attrs = attrPart.Split(',', StringSplitOptions.RemoveEmptyEntries)
						.Select(a => a.Trim());

					foreach (var attr in attrs)
					{
						var kv = attr.Split('=', 2);
						var key = kv[0].Trim();
						var val = kv.Length > 1 ? kv[1].Trim().Trim('"') : null;
						if (val != null)
							xpath.Append($"[@{key}='{val}']");
						else
							xpath.Append($"[@{key}]");
					}
				}
			}
			return xpath.ToString();
		}

	}
}
