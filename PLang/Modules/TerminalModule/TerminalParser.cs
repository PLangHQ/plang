using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PLang.Modules.TerminalModule
{
	/// <summary>
	/// Turns the text a program writes to the terminal into objects, driven by a JSON template file.
	/// The template is compiled once (<see cref="Create"/>), so a bad regex or a bad shape is reported
	/// before the program is started. See Documentation/modules/PLang.Modules.TerminalModule.md.
	/// </summary>
	public class TerminalParser
	{
		public const string RawKey = "!Raw";
		public const string RuleKey = "!Rule";
		public const string UnmatchedKey = "!Unmatched";

		private enum Mode { Columns, Match, KeyValue, Lines }
		private enum Kind { Skipped, Ignored, Unmatched, Parsed }

		private sealed class TemplateException(string message) : Exception(message);

		private sealed class Rule
		{
			public string? As;
			public Regex? When;
			public Mode Mode;
			public bool Once;

			public bool WhitespaceSeparator = true;
			public string Separator = "=";
			public Dictionary<string, int> Columns = new();
			public bool LastColumnRest;
			public List<int> JoinColumns = new();

			public Regex? Pattern;

			public bool PairsOnWhitespace;
			public bool Unquote;
			public bool TrimValues = true;
			public string? BlockEnd;
			public Regex? PairKey;
		}

		private sealed record Outcome(Kind Kind, Rule? Rule = null, JObject? Value = null);

		private sealed class Accumulator
		{
			public readonly List<JObject> Items = new();
			public JObject? Merged;
			public JObject? Block;
			public readonly List<string> Raw = new();
			public readonly List<string> Lines = new();
		}

		/// <summary>Per run state. Streaming keeps one for the whole run, the final parse makes its own.</summary>
		public sealed class Session
		{
			internal int Seen;
		}

		public sealed record Result(JToken Value, List<string> Unmatched);

		private readonly List<Rule> rules = new();
		private readonly bool hasRules;
		private readonly int skip;
		private readonly bool trim;
		private readonly bool autoTypes;
		private readonly Regex? ignore;

		/// <summary>How the template wants standard error treated: "error" (default), "output" or "ignore".</summary>
		public enum StderrMode { Error, Output, Ignore }

		public StderrMode Stderr { get; } = StderrMode.Error;
		public IReadOnlyList<int> OkExitCodes { get; } = new[] { 0 };

		private TerminalParser(JObject root)
		{
			var stderrText = Str(root, "stderr", "template");
			if (stderrText != null)
			{
				if (stderrText.Equals("error", StringComparison.OrdinalIgnoreCase)) Stderr = StderrMode.Error;
				else if (stderrText.Equals("output", StringComparison.OrdinalIgnoreCase)) Stderr = StderrMode.Output;
				else if (stderrText.Equals("ignore", StringComparison.OrdinalIgnoreCase)) Stderr = StderrMode.Ignore;
				else throw new TemplateException($"template: \"stderr\" must be \"error\", \"output\" or \"ignore\", not \"{stderrText}\"");
			}

			var okToken = root["okExitCodes"];
			if (okToken != null && okToken.Type != JTokenType.Null)
			{
				if (okToken is not JArray okArray || okArray.Count == 0 || okArray.Any(t => t.Type != JTokenType.Integer))
					throw new TemplateException("template: \"okExitCodes\" must be a non-empty list of whole numbers, e.g. [0, 1]");
				var codes = new List<int>();
				foreach (var t in okArray)
				{
					try { codes.Add(t.Value<int>()); }
					catch (OverflowException) { throw new TemplateException("template: \"okExitCodes\" holds a number that is too large"); }
				}
				OkExitCodes = codes;
			}

			skip = Int(root, "skip", 0, "template");
			trim = Bool(root, "trim", true, "template");
			var types = Str(root, "types", "template") ?? "auto";
			if (types.Equals("auto", StringComparison.OrdinalIgnoreCase)) autoTypes = true;
			else if (types.Equals("string", StringComparison.OrdinalIgnoreCase)) autoTypes = false;
			else throw new TemplateException($"template: \"types\" must be \"auto\" or \"string\", not \"{types}\"");

			ignore = Compile(Str(root, "ignore", "template"), "template", "ignore");

			if (root["rules"] != null)
			{
				if (root["rules"] is not JArray ruleArray || ruleArray.Count == 0)
					throw new TemplateException("template: \"rules\" must be a non-empty list of rules");

				hasRules = true;
				for (int i = 0; i < ruleArray.Count; i++)
				{
					if (ruleArray[i] is not JObject ruleObj) throw new TemplateException($"rule {i + 1}: a rule must be an object");
					var rule = BuildRule(ruleObj, $"rule {i + 1}");
					if (string.IsNullOrEmpty(rule.As)) throw new TemplateException($"rule {i + 1}: \"as\" is required when the template has \"rules\"");
					if (rules.Any(r => string.Equals(r.As, rule.As, StringComparison.OrdinalIgnoreCase)))
						throw new TemplateException($"rule {i + 1}: \"as\" \"{rule.As}\" is used by an earlier rule");
					rules.Add(rule);
				}
			}
			else
			{
				rules.Add(BuildRule(root, "template"));
			}
		}

		public static (TerminalParser? Parser, string? Error) Create(string templateJson, string templateName)
		{
			JObject root;
			try
			{
				root = JObject.Parse(templateJson);
			}
			catch (Exception ex)
			{
				return (null, $"Parse template {templateName} is not valid JSON: {ex.Message}");
			}

			try
			{
				return (new TerminalParser(root), null);
			}
			catch (TemplateException ex)
			{
				return (null, $"Parse template {templateName}: {ex.Message}");
			}
		}

		public Session NewSession() => new();

		/// <summary>
		/// One streamed line. Returns the parsed object (with !Raw, and !Rule when the template has rules),
		/// the line itself when no rule took it, or null when the line is skipped or ignored.
		/// </summary>
		public object? ParseStreamLine(string line, Session session)
		{
			var outcome = Classify(line, session);
			switch (outcome.Kind)
			{
				case Kind.Parsed:
					outcome.Value![RawKey] = line;
					if (hasRules) outcome.Value[RuleKey] = outcome.Rule!.As;
					return outcome.Value;
				case Kind.Unmatched:
					return line;
				default:
					return null;
			}
		}

		public Result ParseAll(string output)
		{
			var session = NewSession();
			var unmatched = new List<string>();
			var accumulators = rules.ToDictionary(r => r, _ => new Accumulator());

			foreach (var rawLine in output.Split('\n'))
			{
				var line = rawLine.TrimEnd('\r');
				var outcome = Classify(line, session);
				if (outcome.Kind == Kind.Unmatched) { unmatched.Add(trim ? line.Trim() : line); continue; }
				if (outcome.Kind != Kind.Parsed) continue;

				var rule = outcome.Rule!;
				var acc = accumulators[rule];
				var obj = outcome.Value!;

				if (rule.Mode == Mode.Lines)
				{
					acc.Lines.Add(obj["line"]!.Value<string>()!);
				}
				else if (rule.Mode == Mode.KeyValue && rule.BlockEnd != null)
				{
					acc.Block ??= new JObject();
					acc.Raw.Add(line);
					foreach (var prop in obj.Properties()) acc.Block[prop.Name] = prop.Value;
					if (obj.Properties().Any(p => p.Name.Equals(rule.BlockEnd, StringComparison.OrdinalIgnoreCase)))
					{
						CloseBlock(acc);
					}
				}
				else if (IsSingle(rule))
				{
					acc.Merged ??= new JObject();
					acc.Raw.Add(line);
					foreach (var prop in obj.Properties()) acc.Merged[prop.Name] = prop.Value;
				}
				else
				{
					obj[RawKey] = line;
					acc.Items.Add(obj);
				}
			}

			foreach (var acc in accumulators.Values)
			{
				if (acc.Block != null) CloseBlock(acc);
				if (acc.Merged != null) acc.Merged[RawKey] = string.Join("\n", acc.Raw);
			}

			JToken value;
			if (!hasRules)
			{
				var rule = rules[0];
				var ruleValue = RuleValue(rule, accumulators[rule]) ?? new JObject();
				value = string.IsNullOrEmpty(rule.As) ? ruleValue : new JObject { [rule.As] = ruleValue };
			}
			else
			{
				var obj = new JObject();
				foreach (var rule in rules)
				{
					var ruleValue = RuleValue(rule, accumulators[rule]);
					if (ruleValue != null) obj[rule.As!] = ruleValue;
				}
				value = obj;
			}

			if (value is JObject valueObject && unmatched.Count > 0)
			{
				valueObject[UnmatchedKey] = new JArray(unmatched);
			}

			return new Result(value, unmatched);
		}

		private bool IsSingle(Rule rule)
		{
			if (rule.Mode == Mode.Lines) return false;
			if (rule.Mode == Mode.KeyValue && rule.BlockEnd != null) return false;
			return rule.Once || (!hasRules && rule.Mode == Mode.KeyValue);
		}

		private JToken? RuleValue(Rule rule, Accumulator acc)
		{
			if (rule.Mode == Mode.Lines) return new JArray(acc.Lines);
			if (rule.Mode == Mode.KeyValue && rule.BlockEnd != null) return new JArray(acc.Items);
			if (IsSingle(rule)) return acc.Merged;
			return new JArray(acc.Items);
		}

		private static void CloseBlock(Accumulator acc)
		{
			if (acc.Block == null) return;
			acc.Block[RawKey] = string.Join("\n", acc.Raw);
			acc.Items.Add(acc.Block);
			acc.Block = null;
			acc.Raw.Clear();
		}

		private Outcome Classify(string rawLine, Session session)
		{
			var line = rawLine.TrimEnd('\r');
			if (trim) line = line.Trim();
			if (line.Length == 0) return new Outcome(Kind.Skipped);

			session.Seen++;
			if (session.Seen <= skip) return new Outcome(Kind.Skipped);
			if (ignore != null && ignore.IsMatch(line)) return new Outcome(Kind.Ignored);

			foreach (var rule in rules)
			{
				if (rule.When != null && !rule.When.IsMatch(line)) continue;

				var obj = rule.Mode switch
				{
					Mode.Columns => ParseColumns(rule, line),
					Mode.Match => ParseMatch(rule, line),
					Mode.Lines => ParseLine(rule, line),
					_ => ParseKeyValue(rule, line)
				};
				return obj == null ? new Outcome(Kind.Unmatched) : new Outcome(Kind.Parsed, rule, obj);
			}

			return new Outcome(Kind.Unmatched);
		}

		private static JObject? ParseLine(Rule rule, string line)
		{
			if (rule.JoinColumns.Count == 0) return new JObject { ["line"] = line };

			var tokens = Regex_NonSpace.Matches(line).Select(m => m.Value).ToList();
			if (tokens.Count < rule.JoinColumns.Max()) return null;
			return new JObject { ["line"] = string.Join(" ", rule.JoinColumns.Select(c => tokens[c - 1])) };
		}

		private JObject? ParseColumns(Rule rule, string line)
		{
			var tokens = new List<(int Start, string Text)>();
			if (rule.WhitespaceSeparator)
			{
				foreach (Match m in Regex_NonSpace.Matches(line)) tokens.Add((m.Index, m.Value));
			}
			else
			{
				char sep = rule.Separator[0];
				int start = 0;
				while (true)
				{
					int idx = line.IndexOf(sep, start);
					if (idx < 0) { tokens.Add((start, line[start..])); break; }
					tokens.Add((start, line[start..idx]));
					start = idx + 1;
				}
			}

			int max = rule.Columns.Values.Max();
			if (tokens.Count < max) return null;

			var obj = new JObject();
			foreach (var (name, index) in rule.Columns)
			{
				var token = tokens[index - 1];
				var text = (rule.LastColumnRest && index == max) ? line[token.Start..] : token.Text;
				if (trim) text = text.Trim();
				obj[name] = Convert(text);
			}
			return obj;
		}

		private static readonly Regex Regex_NonSpace = new(@"\S+", RegexOptions.Compiled);

		private JObject? ParseMatch(Rule rule, string line)
		{
			var match = rule.Pattern!.Match(line);
			if (!match.Success) return null;

			var obj = new JObject();
			foreach (var name in rule.Pattern.GetGroupNames())
			{
				if (int.TryParse(name, out _)) continue;
				var group = match.Groups[name];
				if (!group.Success) continue;
				obj[name] = Convert(trim ? group.Value.Trim() : group.Value);
			}
			return obj;
		}

		private JObject? ParseKeyValue(Rule rule, string line)
		{
			var obj = new JObject();
			if (!rule.PairsOnWhitespace)
			{
				int idx = line.IndexOf(rule.Separator, StringComparison.Ordinal);
				if (idx <= 0) return null;
				var key = line[..idx].Trim();
				if (key.Length == 0) return null;
				obj[key] = CleanValue(rule, line[(idx + rule.Separator.Length)..]);
				return obj;
			}

			var keys = rule.PairKey!.Matches(line);
			if (keys.Count == 0) return null;
			for (int i = 0; i < keys.Count; i++)
			{
				int valueStart = keys[i].Index + keys[i].Length;
				int valueEnd = i + 1 < keys.Count ? keys[i + 1].Index : line.Length;
				obj[keys[i].Groups["k"].Value] = CleanValue(rule, line[valueStart..valueEnd]);
			}
			return obj;
		}

		private JToken CleanValue(Rule rule, string value)
		{
			if (rule.TrimValues) value = value.Trim();
			if (rule.Unquote && value.Length >= 2 && value[0] == '"' && value[^1] == '"')
			{
				return new JValue(value[1..^1]);
			}
			return Convert(value);
		}

		private static readonly Regex NumberPattern = new(@"^-?(0|[1-9]\d*)(\.\d+)?$", RegexOptions.Compiled);

		private JToken Convert(string value)
		{
			if (!autoTypes) return new JValue(value);

			if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return new JValue(true);
			if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return new JValue(false);
			if (NumberPattern.IsMatch(value))
			{
				if (!value.Contains('.') && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return new JValue(l);
				if (value.Contains('.') && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return new JValue(d);
			}
			return new JValue(value);
		}

		private static Rule BuildRule(JObject obj, string label)
		{
			var rule = new Rule
			{
				As = Str(obj, "as", label),
				When = Compile(Str(obj, "when", label), label, "when"),
				Once = Bool(obj, "once", false, label)
			};

			var modeText = Str(obj, "mode", label);
			if (string.IsNullOrEmpty(modeText)) throw new TemplateException($"{label}: \"mode\" is required, one of \"columns\", \"match\", \"keyvalue\" or \"lines\"");

			switch (modeText.ToLowerInvariant())
			{
				case "columns":
					rule.Mode = Mode.Columns;
					var sep = Str(obj, "separator", label) ?? "whitespace";
					if (sep.Equals("whitespace", StringComparison.OrdinalIgnoreCase)) rule.WhitespaceSeparator = true;
					else if (sep.Length == 1) { rule.WhitespaceSeparator = false; rule.Separator = sep; }
					else throw new TemplateException($"{label}: \"separator\" for columns must be \"whitespace\" or a single character, not \"{sep}\"");

					if (obj["columns"] is not JObject cols || cols.Count == 0) throw new TemplateException($"{label}: \"columns\" must be an object like {{\"name\": 1}} with at least one column");
					foreach (var prop in cols.Properties())
					{
						if (prop.Value.Type != JTokenType.Integer || prop.Value.Value<int>() < 1)
							throw new TemplateException($"{label}: column \"{prop.Name}\" must be a 1-based number");
						rule.Columns[prop.Name] = prop.Value.Value<int>();
					}
					rule.LastColumnRest = Bool(obj, "lastColumnRest", false, label);
					break;

				case "match":
					rule.Mode = Mode.Match;
					var pattern = Str(obj, "pattern", label);
					if (string.IsNullOrEmpty(pattern)) throw new TemplateException($"{label}: \"pattern\" is required for mode match");
					rule.Pattern = Compile(pattern, label, "pattern");
					if (!rule.Pattern!.GetGroupNames().Any(n => !int.TryParse(n, out _)))
						throw new TemplateException($"{label}: \"pattern\" needs at least one named group, e.g. (?<name>\\w+)");
					break;

				case "keyvalue":
					rule.Mode = Mode.KeyValue;
					rule.Separator = Str(obj, "separator", label) ?? "=";
					if (rule.Separator.Length == 0) throw new TemplateException($"{label}: \"separator\" cannot be empty");
					var pairsOn = Str(obj, "pairsOn", label) ?? "line";
					if (pairsOn.Equals("whitespace", StringComparison.OrdinalIgnoreCase)) rule.PairsOnWhitespace = true;
					else if (!pairsOn.Equals("line", StringComparison.OrdinalIgnoreCase)) throw new TemplateException($"{label}: \"pairsOn\" must be \"line\" or \"whitespace\", not \"{pairsOn}\"");
					rule.Unquote = Bool(obj, "unquote", false, label);
					rule.TrimValues = Bool(obj, "trimValues", true, label);
					rule.BlockEnd = Str(obj, "blockEnd", label);
					if (rule.PairsOnWhitespace)
					{
						var sepEscaped = Regex.Escape(rule.Separator);
						rule.PairKey = new Regex($@"(?<![^\s])(?<k>(?:(?!{sepEscaped})\S)+){sepEscaped}", RegexOptions.Compiled);
					}
					break;

				case "lines":
					rule.Mode = Mode.Lines;
					if (obj["columns"] is JArray joinCols)
					{
						foreach (var col in joinCols)
						{
							if (col.Type != JTokenType.Integer || col.Value<int>() < 1)
								throw new TemplateException($"{label}: \"columns\" for lines must be a list of 1-based numbers, like [2, 1]");
							rule.JoinColumns.Add(col.Value<int>());
						}
					}
					else if (obj["columns"] != null) throw new TemplateException($"{label}: \"columns\" for lines must be a list of 1-based numbers, like [2, 1]");
					break;

				default:
					throw new TemplateException($"{label}: unknown mode \"{modeText}\", use \"columns\", \"match\", \"keyvalue\" or \"lines\"");
			}

			return rule;
		}

		private static Regex? Compile(string? pattern, string label, string key)
		{
			if (string.IsNullOrEmpty(pattern)) return null;
			try
			{
				return new Regex(pattern, RegexOptions.CultureInvariant);
			}
			catch (ArgumentException ex)
			{
				throw new TemplateException($"{label}: \"{key}\" is not a valid regex ({ex.Message})");
			}
		}

		private static string? Str(JObject obj, string key, string label)
		{
			var token = obj[key];
			if (token == null || token.Type == JTokenType.Null) return null;
			if (token.Type != JTokenType.String) throw new TemplateException($"{label}: \"{key}\" must be a string");
			return token.Value<string>();
		}

		private static bool Bool(JObject obj, string key, bool defaultValue, string label)
		{
			var token = obj[key];
			if (token == null || token.Type == JTokenType.Null) return defaultValue;
			if (token.Type != JTokenType.Boolean) throw new TemplateException($"{label}: \"{key}\" must be true or false");
			return token.Value<bool>();
		}

		private static int Int(JObject obj, string key, int defaultValue, string label)
		{
			var token = obj[key];
			if (token == null || token.Type == JTokenType.Null) return defaultValue;
			if (token.Type != JTokenType.Integer || token.Value<int>() < 0) throw new TemplateException($"{label}: \"{key}\" must be a number, 0 or more");
			return token.Value<int>();
		}
	}
}
