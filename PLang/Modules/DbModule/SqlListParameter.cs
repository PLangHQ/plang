using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace PLang.Modules.DbModule;

/// <summary>
/// A parameter whose value is a list is either a list of values for the sql (`where id in @ids`, expanded into one parameter per item)
/// or one value that happens to be a JSON array (`json_each(@json)`, `json_extract(@json, '$[0]')`). The sql tells which.
/// </summary>
public static class SqlListParameter
{
	/// <summary>
	/// True when the sql takes the parameter as a list of values: `in @name`, `in (@name)`, `not in @name`.
	/// Any other use, a function argument, a comparison, an insert value, is one value.
	/// </summary>
	public static bool UsedAsList(string? sql, string parameterName)
	{
		if (string.IsNullOrEmpty(sql) || string.IsNullOrEmpty(parameterName)) return false;
		return Regex.IsMatch(sql, @"\bin\s*\(?\s*" + Regex.Escape(parameterName) + @"(?![A-Za-z0-9_])", RegexOptions.IgnoreCase);
	}

	/// <summary>The value as the one value it is bound as: a list is its JSON text.</summary>
	public static object? ToJsonText(object? value)
	{
		if (value is JToken token) return token.ToString(Formatting.None);
		if (value is System.Collections.IList list && value is not byte[]) return JsonConvert.SerializeObject(list);
		return value;
	}
}
