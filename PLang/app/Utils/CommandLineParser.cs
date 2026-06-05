using System.Text.Json;

namespace app.Utils;

/// <summary>
/// Parses CLI arguments into a goal name and key=value parameters.
/// Values are pure JSON — any valid JSON value works: strings, numbers, booleans, objects, arrays.
///
/// Examples:
///   plang Start                          → goal=Start.goal
///   plang --build                        → !build=true
///   plang --debug=Start:3                → !debug="Start:3"
///   plang name="my app"                  → name="my app"
///   plang --build={"files":"test.goal"}  → !build={"files":"test.goal"}
///   plang count=42 active=true           → count=42, active=true
/// </summary>
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

		int i = 0;

		// First arg is the goal name if it doesn't look like a parameter
		if (!IsParameter(args[0]))
		{
			GoalName = args[0];
			if (!GoalName.EndsWith(".goal"))
				GoalName += ".goal";
			i = 1;
		}

		// Parse remaining args as key=value pairs
		while (i < args.Length)
		{
			var arg = args[i];
			bool isSystem = arg.StartsWith("--");
			if (isSystem) arg = arg[2..];

			var eqIndex = arg.IndexOf('=');
			if (eqIndex < 0)
			{
				// Bare flag: --build → true
				var storageKey = isSystem ? "!" + arg : arg;
				Parameters[storageKey] = true;
				i++;
				continue;
			}

			var key = arg[..eqIndex];
			var rawValue = arg[(eqIndex + 1)..];

			// If value starts with { or [, collect remaining args until JSON is complete
			if (rawValue.StartsWith('{') || rawValue.StartsWith('['))
			{
				rawValue = CollectJson(rawValue, args, ref i);
			}

			var storageKey2 = isSystem ? "!" + key : key;
			Parameters[storageKey2] = ParseValue(rawValue);
			i++;
		}
	}

	/// <summary>
	/// Collects a JSON value that may span multiple args (spaces in JSON).
	/// Advances the index past consumed args.
	/// </summary>
	private static string CollectJson(string start, string[] args, ref int i)
	{
		// Try parsing as-is first
		if (IsValidJson(start)) return start;

		// JSON may be split across args — reassemble
		var sb = new System.Text.StringBuilder(start);
		while (++i < args.Length)
		{
			sb.Append(' ').Append(args[i]);
			if (IsValidJson(sb.ToString()))
				return sb.ToString();
		}
		// Reached end without valid JSON — return what we have, ParseValue will handle it
		i--; // back up so the outer loop's i++ doesn't skip
		return sb.ToString();
	}

	private static bool IsValidJson(string s)
	{
		try { JsonDocument.Parse(s); return true; }
		catch (JsonException) { return false; }
	}

	private bool IsParameter(string arg)
	{
		if (string.IsNullOrWhiteSpace(arg)) return false;
		if (arg.StartsWith("--")) return true;
		if (arg.Contains('=')) return true;
		if (arg.Contains('.') && !arg.EndsWith(".goal")) return true;
		return false;
	}

	/// <summary>
	/// Parses a raw value string. Tries JSON first, then falls back to string.
	/// JSON handles all types: "text", 42, true, null, {"key":"val"}, ["a","b"].
	/// </summary>
	private static object ParseValue(string rawValue)
	{
		if (string.IsNullOrEmpty(rawValue))
			return string.Empty;

		// Strip surrounding quotes if present (shell may pass them)
		if ((rawValue.StartsWith('"') && rawValue.EndsWith('"')) ||
			(rawValue.StartsWith('\'') && rawValue.EndsWith('\'')))
			rawValue = rawValue[1..^1];

		// Try JSON deserialization — handles numbers, booleans, null, objects, arrays
		try
		{
			using var doc = JsonDocument.Parse(rawValue);
			var element = doc.RootElement;
			return element.ValueKind switch
			{
				// CLI config is infra (a flag property bag, not a PLang value), so
				// keep it as a raw Dictionary/List rather than the native dict/list
				// value types — the --debug/--test/--app consumers branch on
				// IDictionary<string,object?>. Object and array stay symmetric: both
				// decompose to raw via ToRaw (dict.ToRaw now recurses nested lists too).
				JsonValueKind.Object => ((app.type.dict.@this)data.@this.UnwrapJsonElement(element)!).ToRaw(),
				JsonValueKind.Array => ((app.type.list.@this)data.@this.UnwrapJsonElement(element)!).ToRaw(),
				JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				JsonValueKind.Null => null!,
				JsonValueKind.String => element.GetString()!,
				_ => rawValue
			};
		}
		catch (JsonException)
		{
			// Not valid JSON — return as string
			return rawValue;
		}
	}
}
