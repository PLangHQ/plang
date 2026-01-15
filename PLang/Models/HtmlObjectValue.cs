using AngleSharp.Dom;
using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using PLang.Runtime;
using System.Linq;

namespace PLang.Models;

public class HtmlObjectValue : ObjectValue, IObjectValue
{
	public DynamicObjectValue? Text;
	public DynamicObjectValue? Md;
	public DynamicObjectValue? Json;
	public DynamicObjectValue? Html;
	public DynamicObjectValue? OuterHtml;
	public HtmlObjectValue(string name, object? value, Type? type = null, ObjectValue? parent = null, bool Initiated = true, Properties? properties = null, bool isProperty = false)
		: base(name, value, type, parent, Initiated, properties, isProperty)
	{
	}

	public static HtmlObjectValue FromElement(IEngine engine, string name, IElement? element, ObjectValue? parent = null, object? value = null, bool trimHtml = false)
	{
		var properties = new Properties();
		if (value == null && element != null)
		{
			value = (trimHtml) ? element.TextContent : element.InnerHtml;
		}

		var ov = new HtmlObjectValue(name, value, typeof(string), parent, true, properties);
		if (element != null)
		{
			ov.Text = new DynamicObjectValue("text", () =>
			{
				return element.TextContent;
			}, typeof(string), parent: ov, isProperty: true);

			ov.Md = new DynamicObjectValue("md", () =>
			{
				var converter = engine.GetProgram<Modules.ConvertModule.Program>();
				return converter.ConvertToMd(element.InnerHtml).GetAwaiter().GetResult();
			}, typeof(string), parent: ov, isProperty: true);

			ov.Html = new DynamicObjectValue("html", () => element.InnerHtml, typeof(string), parent: ov, isProperty: true);
			ov.OuterHtml = new DynamicObjectValue("outerHtml", () => element.OuterHtml, typeof(string), parent: ov, isProperty: true);
			ov.Json = new DynamicObjectValue("json", () => JsonConvert.SerializeObject(value), typeof(string), parent: ov, isProperty: true);

			foreach (var attr in element.Attributes)
			{
				properties.Add(new ObjectValue(attr.Name, attr.Value, typeof(string), isProperty: true, parent: ov));
			}

			AddNestedElementAttributes(engine, element, properties, ov);
		}

		return ov;
	}

	private static void AddNestedElementAttributes(IEngine engine, IElement element, Properties properties, HtmlObjectValue parent)
	{
		var childElements = element.QuerySelectorAll("*");
	
		foreach (var child in childElements)
		{
			var tagName = child.TagName.ToLowerInvariant();

			Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
			values.Add("html", new DynamicObjectValue("value", () => child.InnerHtml));
			values.Add("text", new DynamicObjectValue("value", () => child.GetInnerText()));
			values.Add("md", new DynamicObjectValue("value", () =>
			{
				var converter = engine.GetProgram<Modules.ConvertModule.Program>();
				var value = converter.ConvertToMd(child.InnerHtml).GetAwaiter().GetResult();
				return value;
			}));
			var tagObjectValue = new ObjectValue(tagName, values, typeof(string), isProperty: true, parent: parent);

			foreach (var attr in child.Attributes)
			{
				if (!values.ContainsKey(attr.Name))
				{
					values.Add(attr.Name, new ObjectValue(attr.Name, attr.Value, typeof(string), isProperty: true, parent: tagObjectValue));
				}
			}
			properties.Add(tagObjectValue);
		}
	}

	/// <summary>
	/// Gets the inner text of the node
	/// </summary>
	public string? InnerText => (Value as HtmlNode)?.InnerText?.Trim();


	public override object? ValueAs(ObjectValue objectValue, Type convertToType)
	{
		if (Value is IElement element)
		{
			if (convertToType == typeof(string))
			{
				return element.TextContent?.Trim();
			}
			return element.InnerHtml;
		}
		else if (Value is List<HtmlNode> nodes)
		{
			if (nodes.Count == 1 && convertToType == typeof(string))
			{
				return nodes[0].InnerText?.Trim();
			}
			List<string> strings = new();
			strings.AddRange(nodes.Select(p => p.OuterHtml));
			return strings;
		}
		else if (Value is HtmlNodeCollection col)
		{
			if (col.Count == 1 && convertToType == typeof(string))
			{
				return col[0].InnerText?.Trim();
			}
			List<string> strings = new();
			strings.AddRange(col.Select(p => p.OuterHtml));
			return strings;
		}
		throw new NotImplementedException($"{convertToType} - {Value.GetType()}");
	}

}
