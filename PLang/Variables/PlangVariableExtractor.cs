namespace PLang.Variables;


using System.Text.Json;
using System.Text.RegularExpressions;

public class PlangVariableExtractor
{
	private static readonly Regex VariablePattern = new(@"%[\w.]+(?:[^%]*)?%", RegexOptions.Compiled);

	private static readonly HashSet<string> SkipProperties = new(StringComparer.OrdinalIgnoreCase)
	{
		"Text", "Reasoning", "DeveloperComment", "LlmComments", "variables"
	};

	public List<VariableMatch> ExtractVariables(string json)
	{
		var results = new List<VariableMatch>();
		using var doc = JsonDocument.Parse(json);
		ExtractFromElement(doc.RootElement, "", results);
		return results;
	}

	private void ExtractFromElement(JsonElement element, string path, List<VariableMatch> results)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				{
					if (SkipProperties.Contains(property.Name))
						continue;

					var newPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
					ExtractFromElement(property.Value, newPath, results);
				}
				break;

			case JsonValueKind.Array:
				int index = 0;
				foreach (var item in element.EnumerateArray())
				{
					ExtractFromElement(item, $"{path}[{index}]", results);
					index++;
				}
				break;

			case JsonValueKind.String:
				var value = element.GetString();
				if (value != null && VariablePattern.IsMatch(value))
				{
					var matches = VariablePattern.Matches(value);
					foreach (Match match in matches)
					{
						results.Add(new VariableMatch
						{
							Path = path,
							Value = value,
							Variable = match.Value
						});
					}
				}
				break;
		}
	}
}

public class VariableMatch
{
	public string Path { get; set; } = "";
	public string Value { get; set; } = "";
	public string Variable { get; set; } = "";
}
