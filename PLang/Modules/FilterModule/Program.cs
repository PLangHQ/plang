﻿using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Runtime;
using System.Collections;
using System.ComponentModel;
using System.Linq.Dynamic.Core;

namespace PLang.Modules.FilterModule
{
	[Description("Allow user to filter, select, query from a variable and get specific item from that variable.")]
	public class Program : BaseProgram
	{ 
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

			JToken? filterObj = null;
			if (variableToExtractFrom is IList list)
			{
				for (int i=0;i<list.Count;i++)
				{
					JToken obj = variableToExtractFrom as JToken ?? JToken.FromObject(list[i]);
					if (obj is JProperty prop)
					{
						filterObj = MatchProperty(propertyToFilterOn, operatorOnPropertyToFilterOn, null, null, prop) as JToken;

					}
					if (filterObj == null)
					{
						filterObj = GetFilteredJObject(obj, propertyToFilterOn, operatorOnPropertyToFilterOn) as JToken;
					}
					if (filterObj != null) i = list.Count;
				}
			} else
			{
				JToken obj = variableToExtractFrom as JToken ?? JToken.FromObject(variableToExtractFrom);
				if (obj == null)
				{
					return (null, null);
				}

				var filterObj2 = GetFilteredJObject(obj, propertyToFilterOn, operatorOnPropertyToFilterOn) as JToken;
			} 
			

			// Handle retrieval logic
			if (retrieveOneItem == "first") return (filterObj?.FirstOrDefault(), null);
			if (retrieveOneItem == "last") return (filterObj?.LastOrDefault(), null);
			if (int.TryParse(retrieveOneItem, out int idx))
			{
				List<JToken>? tokenList = filterObj?.ToList();
				if (tokenList?.Count > idx && idx >= 0) return (tokenList.ElementAt(idx), null);
			}

			return (filterObj?.ToList(), null);
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
valueToFilterBy: required, find a specific value on the propertyToFilterOn
operatorOnPropertyToFilter can be following(sperated by |): <|>|equals|startswith|endswith|contains.
retrieveOneItem: first|last|retrieveOneItem can also be a number representing the index.
operatorOnPropertyToFilter: equals|startswith|endswith|contains
propertyToExtract: by default it returns the element that matches the property, but when propertyToExtract is specified it will find that property and return the object from that property
operatorToFilterOnValueComparer: insensitive|case-sensitive
<example>
- filter %json% where property starts with ""%item%/"" and has ""John"" as value, write to %libraries% => variableToExtractFrom=""%json%"", 
	propertyToFilterOn=""%item%/"", valueToFilterBy=""John"", operatorToFilterOnValue=""contains"", operatorOnPropertyToFilter=""startswith""
	operatorToFilterOnValueComparer=""insensitive""
- filter %list% where property is ""Quantity"" and is larger then ""10"", give me first, write to %library% => variableToExtractFrom=""%list%"", 
	propertyToFilterOn=""Quantity"", valueToFilterBy=""10"", operatorToFilterOnValue="">"", operatorOnPropertyToFilter=""=""
	operatorToFilterOnValueComparer=""case-sensitive""
	retrieveOneItem=""first""
</example>
")]
		public async Task<object?> FilterOnPropertyAndValue(object variableToExtractFrom, string propertyToFilterOn, string valueToFilterBy, string?	operatorToFilterOnValue = "=", 
			 string operatorOnPropertyToFilter = "=", string? propertyToExtract = null, bool throwErrorWhenNothingFound = false, string? retrieveOneItem = null, 
			 string operatorToFilterOnValueComparer = "insensitive")
		{ 
			if (variableToExtractFrom == null || string.IsNullOrEmpty(variableToExtractFrom.ToString())) return null;

			Func<string, bool> filterPredicate = GetPredicate(operatorToFilterOnValue, valueToFilterBy, operatorToFilterOnValueComparer);

			if (variableToExtractFrom is JObject jObject)
			{
				var filteredObject = GetFilteredJObject(jObject, propertyToFilterOn, operatorOnPropertyToFilter, filterPredicate, propertyToExtract); 

				return filteredObject;
			}
			else if (variableToExtractFrom is JArray jArray)
			{
				var filteredObject = GetFilteredJObject(jArray, propertyToFilterOn, operatorOnPropertyToFilter, filterPredicate, propertyToExtract) as JArray;
				if (filteredObject != null && retrieveOneItem != null)
				{
					if (retrieveOneItem == "first") return filteredObject.FirstOrDefault();
					if (retrieveOneItem == "last") return filteredObject.FirstOrDefault();
					if (int.TryParse(retrieveOneItem, out int idx))
					{
						if (filteredObject.Count > idx && idx >= 0) return filteredObject[idx];
						return null;
					}
				}
				return filteredObject;
				
			}
			else if (variableToExtractFrom is System.Collections.IList list)
			{
				var filteredList = list.ToDynamicList()
					.Where(item => filterPredicate((item as JObject)?[propertyToFilterOn]?.ToString()))
					.ToList();

				if (retrieveOneItem != null)
				{
					if (retrieveOneItem == "first") return filteredList.FirstOrDefault();
					if (retrieveOneItem == "last") return filteredList.FirstOrDefault();
					if (int.TryParse(retrieveOneItem, out int idx))
					{
						if (filteredList.Count > idx && idx >= 0) return filteredList[idx];
						return null;
					}
				}
				return filteredList;
			}
			else if (variableToExtractFrom is IDictionary<string, object> dictionary)
			{
				var filteredDictionary = dictionary
					.Where(entry => entry.Key.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
									filterPredicate(entry.Value.ToString()))
					.ToDictionary(entry => entry.Key, entry => entry.Value);

				return filteredDictionary;
			}
			else
			{
				var json = JsonConvert.SerializeObject(variableToExtractFrom);
				var filteredObject = GetFilteredJObject(json, propertyToFilterOn, operatorOnPropertyToFilter, filterPredicate, propertyToExtract);

				return filteredObject;
				throw new ArgumentException("Unsupported data type for filtering.");
			}
		}

		private object? GetFilteredJObject(JToken jToken, string propertyToFilterOn, string propertyToFilterOnOperator, Func<string, bool>? filterPredicate = null, string? propertyToExtract = null)
		{
			if (jToken is JArray jArray)
			{
				foreach (var item in jArray)
				{
					var obj = GetFilteredJObject(item, propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract);
					if (obj != null) return obj;
				}
			}
			if (jToken is JProperty property)
			{
				return MatchProperty(propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract, property);
			}
			if (jToken is not JObject) return null;

			var properties = (jToken as JObject).Properties();
			foreach (var prop in properties)
			{
				var match = MatchProperty(propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract, prop);
				if (match != null) return match;

			}
			return null;
		}

		private object? MatchProperty(string propertyToFilterOn, string propertyToFilterOnOperator, Func<string, bool>? filterPredicate, string? propertyToExtract, JProperty prop)
		{
			if (propertyToFilterOnOperator == "=")
			{
				if (prop.Name.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
								(filterPredicate == null || filterPredicate(prop.Value.ToString())))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return prop.Value;
					if (prop.Name == propertyToExtract) return prop.Value;
					return ParseParent(prop, propertyToExtract);
				}
			}
			else if (propertyToFilterOnOperator == "startswith")
			{
				if (prop.Name.StartsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
													(filterPredicate == null || filterPredicate(prop.Value.ToString())))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return prop.Value;
					if (prop.Name.StartsWith(propertyToExtract)) return prop.Value;
					return ParseParent(prop, propertyToExtract);
				}
			}
			else if (propertyToFilterOnOperator == "endswith")
			{
				if (prop.Name.EndsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
													(filterPredicate == null || filterPredicate(prop.Value.ToString())))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return prop.Value;
					if (prop.Name.EndsWith(propertyToExtract)) return prop.Value;
					return ParseParent(prop, propertyToExtract);
				}
			}
			else if (propertyToFilterOnOperator == "contains")
			{
				if (prop.Name.Contains(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
													(filterPredicate == null || filterPredicate(prop.Value.ToString())))
				{
					if (string.IsNullOrEmpty(propertyToExtract)) return prop.Value;
					if (prop.Name.Contains(propertyToExtract)) return prop.Value;
					return ParseParent(prop, propertyToExtract);
				}
			}

			if (prop.Value is JObject nestedObject)
			{
				var obj = GetFilteredJObject(nestedObject, propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract);
				if (obj != null) return obj;
			}
			else if (prop.Value is JArray nestedArray)
			{
				foreach (var item in nestedArray)
				{
					var obj = GetFilteredJObject(item, propertyToFilterOn, propertyToFilterOnOperator, filterPredicate, propertyToExtract);
					if (obj != null) return obj;
				}
			}
			return null;
		}

		private object ParseParent(JToken prop, string propertyToExtract)
		{
			var path = prop.Parent.Path;
			var propertyName = path;
			if (propertyName.Contains("["))
			{
				propertyName = path.Substring(0, path.IndexOf("["));
			}
			if (propertyName.Equals(propertyToExtract, StringComparison.OrdinalIgnoreCase)) return prop.Parent;

			return ParseParent(prop.Parent, propertyToExtract);
		}

		private Func<string, bool> GetPredicate(string operatorToFilter, string valueToFilterBy, string operatorToFilterOnValueComparer)
		{
			var comparer = (operatorToFilterOnValueComparer == "insensitive") ? StringComparison.OrdinalIgnoreCase : StringComparison.InvariantCulture;
			return operatorToFilter.ToLower() switch
			{
				"contains" => new Func<string, bool>(v => v.Contains(valueToFilterBy, comparer)),
				"startwith" => new Func<string, bool>(v => v.StartsWith(valueToFilterBy, comparer)),
				"endswith" => new Func<string, bool>(v => v.EndsWith(valueToFilterBy, comparer)), // Similar to contains
				"=" => new Func<string, bool>(v => v.Equals(valueToFilterBy, comparer)),
				">" => new Func<string, bool>(v => double.TryParse(v, out var result) && result > double.Parse(valueToFilterBy)),
				"<" => new Func<string, bool>(v => double.TryParse(v, out var result) && result < double.Parse(valueToFilterBy)),
				_ => throw new ArgumentException($"Unsupported operator: {operatorToFilter}")
			};
		}
	}
}
