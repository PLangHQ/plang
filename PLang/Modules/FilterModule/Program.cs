using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace PLang.Modules.FilterModule
{
	[Description("Allow user to filter, select, query from a variable and get specific item from that variable.")]
	public class Program : BaseProgram
	{
		public async Task<List<JToken>?> FilterOutProperties(string propertyToExtract, object variableToExtractFrom)
		{
			var obj = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(variableToExtractFrom));
			if (obj is JObject jObject)
			{
				var tokens = jObject.SelectTokens(propertyToExtract);
				return tokens.ToList();
			}

			if (obj is JArray jArray)
			{
				var tokens = jArray.SelectTokens(propertyToExtract);
				return tokens.ToList();
			}
			return null;
		}

			[Description("operatorToFilter can be following: < > = startswith endswith contains")]
		public async Task<object?> Filter(string propertyToExtract, object variableToExtractFrom, string propertyToFilterOn, string valueToFilterBy, string operatorToFilter, bool throwErrorWhenNothingFound = false)
		{
			if (variableToExtractFrom == null || string.IsNullOrEmpty(variableToExtractFrom.ToString())) return null;

			Func<string, bool> filterPredicate = GetPredicate(operatorToFilter, valueToFilterBy);

			if (variableToExtractFrom is JObject jObject)
			{
				var filteredObject = GetFilteredJObject(jObject, propertyToFilterOn, filterPredicate, propertyToExtract); 

				return filteredObject;
			}
			else if (variableToExtractFrom is JArray jArray)
			{
				var filteredObject = GetFilteredJObject(jArray, propertyToFilterOn, filterPredicate, propertyToExtract);

				return filteredObject;
				
			}
			else if (variableToExtractFrom is IList<object> list)
			{
				var filteredList = list
					.Where(item => filterPredicate((item as JObject)?[propertyToFilterOn]?.ToString()))
					.ToList();

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
				var filteredObject = GetFilteredJObject(json, propertyToFilterOn, filterPredicate, propertyToExtract);

				return filteredObject;
				throw new ArgumentException("Unsupported data type for filtering.");
			}
		}

		private object? GetFilteredJObject(JToken jToken, string propertyToFilterOn, Func<string, bool> filterPredicate, string propertyToExtract)
		{
			if (jToken is JArray jArray)
			{
				foreach (var item in jArray)
				{
					var obj = GetFilteredJObject(item, propertyToFilterOn, filterPredicate, propertyToExtract);
					if (obj != null) return obj;
				}
			}

			if (jToken is not JObject) return null;

			var properties = (jToken as JObject).Properties();
			foreach (var prop in properties)
			{
				if (prop.Name.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase) &&
								filterPredicate(prop.Value.ToString()))
				{
					if (prop.Name == propertyToExtract) return prop.Value;
					return ParseParent(prop, propertyToExtract);
				}

				if (prop.Value is JObject nestedObject)
				{
					var obj = GetFilteredJObject(nestedObject, propertyToFilterOn, filterPredicate, propertyToExtract);
					if (obj != null) return obj;
				}
				else if (prop.Value is JArray nestedArray)
				{
					foreach (var item in nestedArray)
					{
						var obj = GetFilteredJObject(item, propertyToFilterOn, filterPredicate, propertyToExtract);
						if (obj != null) return obj;
					}
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

		private Func<string, bool> GetPredicate(string operatorToFilter, string valueToFilterBy)
		{
			return operatorToFilter.ToLower() switch
			{
				"contains" => new Func<string, bool>(v => v.Contains(valueToFilterBy, StringComparison.OrdinalIgnoreCase)),
				"startwith" => new Func<string, bool>(v => v.StartsWith(valueToFilterBy, StringComparison.OrdinalIgnoreCase)),
				"endswith" => new Func<string, bool>(v => v.EndsWith(valueToFilterBy, StringComparison.OrdinalIgnoreCase)), // Similar to contains
				"=" => new Func<string, bool>(v => v.Equals(valueToFilterBy, StringComparison.OrdinalIgnoreCase)),
				">" => new Func<string, bool>(v => double.TryParse(v, out var result) && result > double.Parse(valueToFilterBy)),
				"<" => new Func<string, bool>(v => double.TryParse(v, out var result) && result < double.Parse(valueToFilterBy)),
				_ => throw new ArgumentException($"Unsupported operator: {operatorToFilter}")
			};
		}
	}
}
