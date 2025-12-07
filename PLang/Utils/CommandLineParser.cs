


using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PLang.Utils;

public class CommandLineParser
{
	public string GoalName { get; private set; } = "Start.goal";
	public Dictionary<string, object> Parameters { get; private set; } = new();

	public static (string goalName, Dictionary<string, object> parameters) Parse(string[] args)
	{
		var parser = new CommandLineParser();
		parser.ParseArgs(args);
		return (parser.GoalName, parser.Parameters);
	}

	private void ParseArgs(string[] args)
	{
		if (args.Length == 0) return;

		int startIndex = 0;

		// Check if args[0] is a goal name or a parameter
		if (!IsParameter(args[0]))
		{
			GoalName = args[0];
			if (!GoalName.EndsWith(".goal"))
				GoalName += ".goal";
			startIndex = 1;
		}

		// Join remaining args and parse as parameters
		var paramString = string.Join(" ", args, startIndex, args.Length - startIndex);
		ParseParameters(paramString);
	}

	private bool IsParameter(string arg)
	{
		if (string.IsNullOrWhiteSpace(arg)) return false;

		// Starts with ! (flag or namespaced param)
		if (arg.StartsWith("!")) return true;

		// Contains = (key=value)
		if (arg.Contains("=")) return true;

		// Contains . with no spaces before = (namespaced like llm.service=)
		// This catches cases where the full param might be split across args
		if (arg.Contains(".") && !arg.EndsWith(".goal")) return true;

		return false;
	}

	private void ParseParameters(string input)
	{
		if (string.IsNullOrWhiteSpace(input)) return;

		// Regex to match parameters:
		// - !flag (boolean true)
		// - !key.subkey="value" or !key.subkey=value
		// - key=value or key="value"
		// - key.subkey=value or key.subkey="value"

		var pattern = @"
            (?:^|[\s,]+)                           # start or separator
            (!)?                                   # optional ! prefix (group 1)
            ([\w.]+)                               # key, possibly with dots (group 2)
            (?:
                \s*=\s*                            # equals sign
                (?:
                    ""([^""]*)""|                  # quoted value (group 3)
                    '([^']*)'|                     # single quoted value (group 4)
                    ([^\s,]+)                      # unquoted value (group 5)
                )
            )?                                     # value part is optional (for flags)
        ";

		var regex = new Regex(pattern, RegexOptions.IgnorePatternWhitespace);
		var matches = regex.Matches(input);

		foreach (Match match in matches)
		{
			bool hasExclaim = match.Groups[1].Success;
			string key = match.Groups[0].Value;

			// Determine value
			string? rawValue = match.Groups[3].Success ? match.Groups[3].Value :
							   match.Groups[4].Success ? match.Groups[4].Value :
							   match.Groups[5].Success ? match.Groups[5].Value :
							   null;

			object value;

			if (rawValue == null)
			{
				// No value provided - it's a flag, ! means true
				value = hasExclaim;
			}
			else
			{
				value = ParseValue(rawValue);
			}

			Parameters[key] = value;
		}
	}

	private object ParseValue(string rawValue)
	{
		if (string.IsNullOrEmpty(rawValue))
			return string.Empty;

		// Boolean
		if (rawValue.Equals("true", StringComparison.OrdinalIgnoreCase))
			return true;
		if (rawValue.Equals("false", StringComparison.OrdinalIgnoreCase))
			return false;

		// Null
		if (rawValue.Equals("null", StringComparison.OrdinalIgnoreCase))
			return null!;

		// Integer (long for larger numbers)
		if (long.TryParse(rawValue, out long longVal))
		{
			// Return int if it fits, otherwise long
			if (longVal >= int.MinValue && longVal <= int.MaxValue)
				return (int)longVal;
			return longVal;
		}

		// Decimal (better for currency/precision than double)
		if (decimal.TryParse(rawValue, out decimal decVal))
			return decVal;

		// Guid
		if (Guid.TryParse(rawValue, out Guid guidVal))
			return guidVal;

		// DateTime (ISO 8601 formats)
		if (DateTime.TryParse(rawValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dateVal))
		{
			// Only treat as date if it looks like a date format, not just any parseable string
			if (Regex.IsMatch(rawValue, @"^\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4}"))
				return dateVal;
		}

		// TimeSpan (formats like 1:30:00, 00:05:30)
		if (rawValue.Contains(':') && TimeSpan.TryParse(rawValue, out TimeSpan timeVal))
			return timeVal;

		// JSON array [...] or object {...}
		if ((rawValue.StartsWith("[") && rawValue.EndsWith("]")) ||
			(rawValue.StartsWith("{") && rawValue.EndsWith("}")))
		{
			try
			{
				return JsonConvert.DeserializeObject(rawValue);
			}
			catch
			{
				// Fall through to string
			}
		}

		return rawValue;
	}
}