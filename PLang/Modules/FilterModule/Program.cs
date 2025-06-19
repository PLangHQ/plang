using HtmlAgilityPack;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Methods;
using PLang.Errors.Runtime;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Text.RegularExpressions;
using Fizzler.Systems.HtmlAgilityPack;
using Ganss.Xss;
using PLang.Building.Model;
using Sprache;
using System.Collections.Generic;
using System.Text;

namespace PLang.Modules.FilterModule
{
	[Description("Allow user to find text, filter, select, query from a variable and get specific item from that variable.")]
	public class Program : BaseProgram
	{
		[Description("Parses an input that is wrapped with markdown code format and return text inside those code blocks")]
		public async Task<object?> ExtractMarkdownWrapping(string input, string[]? format = null)
		{
			if (string.IsNullOrEmpty(input)) return null;

			string pattern = @"```(\w+)?\s*([\s\S]*?)\s*```";
			Regex regex = new Regex(pattern, RegexOptions.Multiline);

			List<(string Language, string Code)> extractedBlocks = new List<(string, string)>();

			foreach (Match match in regex.Matches(input))
			{
				string language = match.Groups[1].Success ? match.Groups[1].Value : "plaintext";
				string code = match.Groups[2].Value;
				extractedBlocks.Add((language, code));
			}

			if (format != null)
			{
				extractedBlocks = extractedBlocks.Where(p => format.Contains(p.Code)).ToList();
			}


			return (extractedBlocks.Count == 1) ? extractedBlocks[0].Code : extractedBlocks;

		}

		[Description("Extracts all html element matches elementName, e.g. if user want to extract link, elementNames=[\"a\"]")]
		public async Task<List<HtmlNode>?> ExtractElementsFromHtml(string html, List<string> elementNames)
		{
			if (string.IsNullOrEmpty(html)) return null;

			HtmlDocument document = new();
			document.LoadHtml(html);

			List<HtmlNode> htmlElements = new();
			foreach (var elementName in elementNames)
			{
				var elements = document.DocumentNode.Descendants(elementName);
				htmlElements.AddRange(MapHtmlNodes(elements));
			}
			return htmlElements;
		}


		[Description("matching: contains|startwith|endwith|equals. retrieveOneItem: first|last|number (retrieveOneItem can also be a number representing the index.)")]
		public async Task<object?> FindTextInContent(string content, string textToFind, string matching = "contains", string? retrieveOneItem = null)
		{
			if (string.IsNullOrEmpty(content)) return null;

			string[] lines = content.Split(new char[] { '\r', '\n' });
			IEnumerable<string> matchedLines = null;

			if (matching == "startwith")
			{
				matchedLines = lines.Where(p => p.TrimStart().StartsWith(textToFind, StringComparison.OrdinalIgnoreCase));
			}
			else if (matching == "endwith")
			{
				matchedLines = lines.Where(p => p.TrimEnd().EndsWith(textToFind, StringComparison.OrdinalIgnoreCase));
			}
			else if (matching == "equals")
			{
				matchedLines = lines.Where(p => p.Equals(textToFind, StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				matchedLines = lines.Where(p => p.Contains(textToFind, StringComparison.OrdinalIgnoreCase));
			}

			if (retrieveOneItem == "first") return matchedLines.FirstOrDefault();
			if (retrieveOneItem == "last") return matchedLines.FirstOrDefault();
			if (int.TryParse(retrieveOneItem, out var idx)) return matchedLines.ElementAtOrDefault(idx);

			return matchedLines.ToList();

		}

		[Description("Joins a list of items into one string")]
		public async Task<string?> Join(List<object> list, string seperator = ", ", string[]? exclude = null)
		{
			string? str = null;
			foreach (var item in list)
			{
				if (str != null) str += seperator;
				if (exclude == null || exclude.FirstOrDefault(p => p.Equals(exclude.ToString(), StringComparison.OrdinalIgnoreCase)) == null)
				{
					str += item.ToString();
				}
			}
			return str;
		}

		[Description("Gets an item from list, giving the first, last or by index according to user definition. retrieveOneItem: first|last|number (retrieveOneItem can also be a number representing the index.)")]
		public async Task<object?> GetItem(object variableToExtractFrom, string retrieveOneItem)
		{
			if (variableToExtractFrom is not JArray && !variableToExtractFrom.GetType().Name.StartsWith("List") &&
				!variableToExtractFrom.GetType().Name.StartsWith("Dictionary")) return variableToExtractFrom;

			if (variableToExtractFrom is JArray jarray)
			{
				if (retrieveOneItem == "first") return jarray.FirstOrDefault();
				if (retrieveOneItem == "last") return jarray.LastOrDefault();
				return jarray[int.Parse(retrieveOneItem)];
			}

			if (variableToExtractFrom.GetType().Name.StartsWith("List"))
			{
				var list = (IList)variableToExtractFrom;
				if (retrieveOneItem == "first") return list[0];
				if (retrieveOneItem == "last") return list[list.Count - 1];
				return list[int.Parse(retrieveOneItem)];
			}

			if (variableToExtractFrom.GetType().Name.StartsWith("Dictionary"))
			{
				var dict = (IDictionary)variableToExtractFrom;
				int counter = 0;
				object? outItem = null;
				foreach (var item in dict)
				{
					if (retrieveOneItem == "first") return item;
					if (counter++ == int.Parse(retrieveOneItem)) return item;
					outItem = item;
				}
				if (retrieveOneItem == "last") return outItem;

			}

			return null;
		}

		[Description(@"Use this function when the intent is to filter a list based solely on the property name or pattern of the property name, without needing to match a specific value within that property. 
This function is suitable when the user specifies conditions like ""property starts with"", ""property ends with"", or ""property contains"" without mentioning a value to filter by.

operatorOnPropertyToFilterOn can be: =|!=|startswith|endswith|contains
retrieveOneItem: null|first|last|number (retrieveOneItem can also be a number representing the index.)
can return a list of elements or one element, depending on if retrieveOneItem is set.
<example>
- filter %list% where property is 'Name' => variableToExtractFrom=""%list%"", propertyToFilterOn=""Name""
- filter %list% where property contains 'Addr', return the first => variableToExtractFrom=""%list%"", propertyToFilterOn=""Name"", operatorOnPropertyToFilterOn=""contains"", retrieveOneItem=""first""
</example>
")]
		public async Task<(object?, IError?)> FilterOnProperty(object variableToExtractFrom, string propertyToFilterOn, string operatorOnPropertyToFilterOn = "=", string? retrieveOneItem = null)
		{
			if (variableToExtractFrom == null)
			{
				return (null, new ProgramError("variableToExtractFrom cannot be empty", goalStep, function));
			}

			List<object>? filteredList = null;
			if (variableToExtractFrom is IList list)
			{
				for (int i = 0; i < list.Count; i++)
				{
					JToken obj = variableToExtractFrom as JToken ?? JToken.FromObject(list[i]);
					if (obj is JProperty prop)
					{
						filteredList = MatchProperty(propertyToFilterOn, operatorOnPropertyToFilterOn, null, null, prop);

					}
					if (filteredList == null)
					{
						filteredList = GetFilteredJObject(obj, propertyToFilterOn, operatorOnPropertyToFilterOn);
					}
					if (filteredList != null) i = list.Count;
				}
			}
			else
			{
				JToken obj = variableToExtractFrom as JToken ?? JToken.FromObject(variableToExtractFrom);
				if (obj == null)
				{
					return (null, null);
				}
				throw new Exception("Dont know what this code should do. So throw error");
				var filterObj2 = GetFilteredJObject(obj, propertyToFilterOn, operatorOnPropertyToFilterOn);
			}


			// Handle retrieval logic
			if (retrieveOneItem == "first") return (filteredList?.FirstOrDefault(), null);
			if (retrieveOneItem == "last") return (filteredList?.LastOrDefault(), null);
			if (int.TryParse(retrieveOneItem, out int idx))
			{
				if (filteredList?.Count > idx && idx >= 0) return (filteredList.ElementAt(idx), null);
			}

			return (filteredList?.ToList(), null);
		}

		private bool ApplyOperatorOnPropertyName(string propertyName, string operatorOnPropertyToFilterOn, string propertyToFilterOn)
		{
			switch (operatorOnPropertyToFilterOn.ToLower())
			{
				case "equals":
				case "=":
					return propertyName.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "!=":
					return !propertyName.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "startswith":
					return propertyName.StartsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "endswith":
					return propertyName.EndsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "contains":
					return propertyName.Contains(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				default:
					return false;
			}
		}

		[Description(@" Use this function when the intent is to filter a list based on both the property name and a specific value within that property. 
This function is appropriate when the user specifies conditions that involve both a property and a value, such as ""property is 'Name' and value is 'John'"", or when operators on both the property and value are needed.

propertyToFilterOn: required, property of a list to filter on
valueToFilterBy: required, find a specific Value that is stored in the propertyToFilterOn
operatorOnPropertyToFilter can be following(sperated by |): <|>|equals|startswith|endswith|contains.
retrieveOneItem: first|last|retrieveOneItem can also be a number representing the index.
operatorOnPropertyToFilter: equals|startswith|endswith|contains
propertyToExtract: by default it returns the element that matches the property, can be defined as 'parent' or when propertyToExtract is specified it will find that property and return the object from that property.
operatorToFilterOnValue: =|!= 
operatorToFilterOnValueComparer: insensitive|case-sensitive
<example>
- filter %json% where property starts with ""%item%/"" and has ""John"" as value, get parent object, write to %libraries% => variableToExtractFrom=""%json%"", 
	propertyToFilterOn=""%item%/"", valueToFilterBy=""John"", operatorToFilterOnValue=""contains"", operatorOnPropertyToFilter=""startswith"", propertyToExtract=""parent""
	operatorToFilterOnValueComparer=""insensitive""
- filter %list% where property is ""Quantity"" and is larger then ""10"", give me first, write to %library% => variableToExtractFrom=""%list%"", 
	propertyToFilterOn=""Quantity"", valueToFilterBy=""10"", operatorToFilterOnValue="">"", operatorOnPropertyToFilter=""=""
	operatorToFilterOnValueComparer=""case-sensitive""
	retrieveOneItem=""first""
</example>
")]
		public async Task<(object?, IError?)> FilterOnPropertyAndValue(object variableToExtractFrom, string propertyToFilterOn, string valueToFilterBy, string? operatorToFilterOnValue = "=",
			 string operatorOnPropertyToFilter = "=", string? propertyToExtract = null, bool throwErrorWhenNothingFound = false, string? retrieveOneItem = null,
			 string operatorToFilterOnValueComparer = "insensitive")
		{
			if (variableToExtractFrom == null || string.IsNullOrEmpty(variableToExtractFrom.ToString())) return (null, null);
			if (valueToFilterBy == "null") valueToFilterBy = null;

			Func<object, bool> filterPredicate = GetPredicate(operatorToFilterOnValue, valueToFilterBy, operatorToFilterOnValueComparer);

			List<object> filteredList = new();
			if (variableToExtractFrom is JObject jObject)
			{
				filteredList = GetFilteredJObject(jObject, propertyToFilterOn, operatorOnPropertyToFilter, filterPredicate, propertyToExtract);
			}
			else if (variableToExtractFrom is JArray jArray)
			{
				filteredList = GetFilteredJObject(jArray, propertyToFilterOn, operatorOnPropertyToFilter, filterPredicate, propertyToExtract);

			}
			else if (variableToExtractFrom is System.Collections.IList list)
			{
				var dynamicList = list.ToDynamicList();
				if (dynamicList.Count > 0 && dynamicList[0] is JObject)
				{
					filteredList = dynamicList
						.Where(item =>
						{
							var obj = (JValue)(item as JObject)?[propertyToFilterOn];
							return filterPredicate(obj.Value);

						})
						.ToList();
				}
				else if (dynamicList.Count > 0 && dynamicList[0] is JArray)
				{
					foreach (var itemInlist in dynamicList)
					{
						var array = (itemInlist as JArray).ToDynamicList();

						filteredList.AddRange(array
						.Where(item =>
						{
							var obj = item as JObject;
							if (obj == null) { return false; }

							var jValue = obj?[propertyToFilterOn] as JValue;

							return filterPredicate(jValue?.Value);

						})
						.ToList());
					}
				}
				else if (list.Count > 0 && TypeHelper.ImplementsDict(list[0], out IDictionary? dict2))
				{
					var keyName = dict2.Keys.Cast<string>().FirstOrDefault(p => p.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase));
					if (keyName == null) return (null, new ProgramError($"Could not find {propertyToFilterOn} in object"));

					foreach (var item in list)
					{
						if (!TypeHelper.ImplementsDict(item, out IDictionary? dict)) continue;

						var obj = dict[keyName];
						var result = filterPredicate(obj);
						if (!result) continue;

						if (string.IsNullOrWhiteSpace(propertyToExtract))
						{
							filteredList.Add(obj);
						}
						else if (propertyToExtract == "parent")
						{
							filteredList.Add(dict);
						}
						else
						{

							keyName = dict.Keys.Cast<string>().FirstOrDefault(p => p.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase));
							if (string.IsNullOrEmpty(keyName))
							{
								filteredList.Add(obj);
							}
							else
							{
								filteredList.Add(dict[keyName]);
							}
						}
					}
				}
				else
				{

					foreach (var item in list)
					{

						var property = item.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase));
						if (property != null)
						{
							var result = filterPredicate(property.GetValue(item));
							if (result)
							{
								filteredList.Add(item);
							}
						}

					}

				}


			}
			else if (variableToExtractFrom is IDictionary dictionary)
			{
				var filteredDictionary = dictionary.Cast<dynamic>()
					.Where(entry => entry.Key.ToString().Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
									filterPredicate(entry.Value.ToString()))
					.ToDictionary(entry => entry.Key, entry => entry.Value);

				if (retrieveOneItem == "first") return (filteredDictionary.FirstOrDefault(), null);
				if (retrieveOneItem == "last") return (filteredDictionary.LastOrDefault(), null);

				return (filteredDictionary, null);
			}
			else
			{
				var json = JsonConvert.SerializeObject(variableToExtractFrom);
				var jsonObject = JsonConvert.DeserializeObject(json);
				filteredList = GetFilteredJObject(jsonObject as JToken, propertyToFilterOn, operatorOnPropertyToFilter, filterPredicate, propertyToExtract);

			}

			if (filteredList == null) return (null, null);

			if (retrieveOneItem == "first") return (filteredList.FirstOrDefault(), null);
			if (retrieveOneItem == "last") return (filteredList.LastOrDefault(), null);
			if (int.TryParse(retrieveOneItem, out int idx))
			{
				if (filteredList.Count > idx && idx >= 0) return (filteredList[idx], null);
				return (null, new ProgramError($"List does not have {idx + 1} items"));
			}
			return (filteredList, null);

		}

		private bool ImplementsDict(object? v, out IDictionary dict)
		{
			throw new NotImplementedException();
		}

		private List<object>? GetFilteredJObject(JToken jToken, string propertyToFilterOn, string propertyToFilterOnOperator, Func<object, bool>? filterPredicate = null, string? propertyToExtract = null)
		{
			var list = new List<object>();
			if (jToken is JArray jArray)
			{
				foreach (var item in jArray)
				{
					var obj = GetFilteredJObject(item, propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract);
					if (obj != null) list.AddRange(obj);
				}
			}
			if (jToken is JProperty property)
			{
				var obj = MatchProperty(propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract, property);
				list.AddRange(obj);
			}
			if (jToken is not JObject) return list;

			var properties = (jToken as JObject).Properties();
			foreach (var prop in properties)
			{
				var match = MatchProperty(propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract, prop);
				if (match != null) list.AddRange(match);

			}
			return list;
		}

		private List<object>? MatchProperty(string propertyToFilterOn, string propertyToFilterOnOperator, Func<object?, bool>? filterPredicate, string? propertyToExtract, JProperty prop)
		{
			object? value = prop.Value;
			if (value is JValue jValue) value = jValue.Value;

			if (propertyToFilterOnOperator == "=" || propertyToFilterOnOperator == "equals")
			{
				if (prop.Name.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
								(filterPredicate == null || filterPredicate(value)))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return [prop.Value];
					if (prop.Name == propertyToExtract) return [prop.Value];
					return [ParseParent(prop, propertyToExtract)];
				}
			}
			else if (propertyToFilterOnOperator == "startswith")
			{
				if (prop.Name.StartsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
													(filterPredicate == null || filterPredicate(prop.Value)))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return [prop.Value];
					if (prop.Name.StartsWith(propertyToExtract)) return [prop.Value];
					return [ParseParent(prop, propertyToExtract)];
				}
			}
			else if (propertyToFilterOnOperator == "endswith")
			{
				if (prop.Name.EndsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
													(filterPredicate == null || filterPredicate(prop.Value)))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return [prop.Value];
					if (prop.Name.EndsWith(propertyToExtract)) return [prop.Value];
					return [ParseParent(prop, propertyToExtract)];
				}
			}
			else if (propertyToFilterOnOperator == "contains")
			{
				if (prop.Name.Contains(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
													(filterPredicate == null || filterPredicate(prop.Value)))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return [prop.Value];
					if (prop.Name.Contains(propertyToExtract)) return [prop.Value];
					return [ParseParent(prop, propertyToExtract)];
				}
			}
			List<object> filteredList = new();
			if (prop.Value is JObject nestedObject)
			{
				var obj = GetFilteredJObject(nestedObject, propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract);
				if (obj != null) filteredList.AddRange(obj);
			}
			else if (prop.Value is JArray nestedArray)
			{

				foreach (var item in nestedArray)
				{
					var obj = GetFilteredJObject(item, propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract);
					if (obj != null) filteredList.AddRange(obj);
				}
			}
			return filteredList;
		}

		private object ParseParent(JToken prop, string propertyToExtract)
		{
			if (propertyToExtract == null) return prop;
			if (propertyToExtract == "parent") return prop.Parent;

			var path = prop.Parent.Path;
			var propertyName = path;
			if (propertyName.Contains("["))
			{
				propertyName = path.Substring(0, path.IndexOf("["));
			}
			if (propertyName.Equals(propertyToExtract, StringComparison.OrdinalIgnoreCase)) return prop.Parent;

			return ParseParent(prop.Parent, propertyToExtract);
		}

		private Func<object, bool> GetPredicate(string operatorToFilter, string valueToFilterBy, string operatorToFilterOnValueComparer)
		{

			var comparer = (operatorToFilterOnValueComparer == "insensitive") ? StringComparison.OrdinalIgnoreCase : StringComparison.InvariantCulture;
			return operatorToFilter.ToLower() switch
			{
				"contains" => new Func<object, bool>(v =>
				{


					return v != null && v.ToString().Contains(valueToFilterBy, comparer);
				}),

				"startwith" => new Func<object, bool>(v => v != null && v.ToString().StartsWith(valueToFilterBy, comparer)),
				"endswith" => new Func<object, bool>(v => v != null && v.ToString().EndsWith(valueToFilterBy, comparer)), // Similar to contains
				"equals" or "=" => new Func<object, bool>(v =>
				{
					if (v == null && valueToFilterBy == null) return true;
					if (v == null) return false;

					if (v is string str)
					{
						return str != null && str.Equals(valueToFilterBy, comparer);
					}

					if (v is JToken token)
					{
						return token.ToString().Equals(valueToFilterBy.ToString(), comparer);
					}
					var obj = TypeHelper.ConvertToType(valueToFilterBy, v.GetType());

					return v != null && v.Equals(obj);
				}),
				"!=" => new Func<object, bool>(v => v != null && !v.Equals(valueToFilterBy)),
				">" => new Func<object, bool>(v => v != null && double.TryParse(v.ToString(), out var result) && result > double.Parse(valueToFilterBy)),
				"<" => new Func<object, bool>(v => v != null && double.TryParse(v.ToString(), out var result) && result < double.Parse(valueToFilterBy)),
				_ => throw new ArgumentException($"Unsupported operator: {operatorToFilter}")
			};
		}

		[Description("Retrieve element(s) from html by a css selector, retrieveOneItem: first|last|number (retrieveOneItem can also be a number representing the index.) ")]
		public async Task<object?> ExtractByCssSelector(string html, string cssSelector, string? retrieveOneItem = null)
		{
			if (string.IsNullOrEmpty(html)) return null;

			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var nodes = doc.DocumentNode.QuerySelectorAll(cssSelector);

			if (!string.IsNullOrEmpty(retrieveOneItem))
			{
				if (retrieveOneItem == "first") return MapHtmlNode(nodes.FirstOrDefault());
				if (retrieveOneItem == "last") return MapHtmlNode(nodes.LastOrDefault());
				if (int.TryParse(retrieveOneItem, out int idx)) return nodes.ElementAtOrDefault(idx);
			}

			List<HtmlNode> returnNodes = new();
			foreach (var node in nodes)
			{
				var mappedNode = MapHtmlNode(node);
				if (mappedNode != null) returnNodes.Add(mappedNode);
			}

			return returnNodes;
		}

		public async Task<object?> ExtractFromXPath(string html, string xpath)
		{
			if (string.IsNullOrEmpty(html)) return null;

			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var node = doc.DocumentNode.SelectSingleNode(xpath);
			return MapHtmlNode(node);
		}


		public record HtmlNode(string Name, string OuterHtml, string? InnerText, Dictionary<string, string> Attributes)
		{
			[JsonIgnore]
			public Goal Goal { get; set; }
			public override string ToString()
			{
				HtmlSanitizerOptions options = Goal.GetVariable<HtmlSanitizerOptions>() ?? new();
				var sanitizer = new HtmlSanitizer(options);
				return sanitizer.Sanitize(OuterHtml);
			}

			public (string?, IError?) this[string key] => Attributes.TryGetValue(key, out var value) ? (value, null) : (null, new ProgramError($"{key} not found", Key: "AttibuteNotFound"));
		}
		private List<HtmlNode> MapHtmlNodes(IEnumerable<HtmlAgilityPack.HtmlNode> nodes)
		{
			List<HtmlNode> htmlNodes = new();
			foreach (var node in nodes)
			{
				var mappedNode = MapHtmlNode(node);
				if (mappedNode == null) continue;

				htmlNodes.Add(mappedNode);
			}
			return htmlNodes;
		}
		private HtmlNode? MapHtmlNode(HtmlAgilityPack.HtmlNode? node)
		{
			if (node == null) return null;

			var attributes = node.Attributes.ToDictionary(a => a.Name, a => a.Value);
			return new HtmlNode(node.Name, node.OuterHtml, node.InnerText?.Trim(), attributes)
			{
				Goal = goal
			};
		}



	}
}
