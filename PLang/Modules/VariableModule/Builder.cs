using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors.Builder;
using System.Text.RegularExpressions;

namespace PLang.Modules.VariableModule
{
	public class Builder : BaseBuilder
	{
		private static readonly Regex VariableInText = new(@"%([^%\s]+)%", RegexOptions.Compiled);

		/// <summary>
		/// A return step carries every %variable% its text names. A build that dropped them returns nothing to the caller,
		/// with no error, so it is built again; a literal value has no name to be returned under and is refused.
		/// </summary>
		public async Task<(Instruction?, IBuilderError?)> BuilderReturn(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			var named = VariableInText.Matches(step.Text).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			if (named.Count == 0)
			{
				var rest = Regex.Replace(step.Text.Trim(), @"^-?\s*return\b", "", RegexOptions.IgnoreCase).Trim();
				if (rest.Contains('"') || rest.Contains('\''))
				{
					return (instruction, new StepBuilderError($"`{step.Text.Trim()}` returns a value that has no name, so the caller gets nothing.", step, "ReturnValueHasNoName", Retry: false,
						FixSuggestion: $"Give the value a name: `return \"...\" as %result%`, or set a variable first: `set %result% = \"...\"` then `return %result%`."));
				}
				return (instruction, null);
			}

			var entries = GetEntries(gf.Parameters?.FirstOrDefault(p => p.Name == "variables")?.Value);
			var missing = named.Where(name => !entries.Any(e =>
					e.Key.TrimStart('!').Equals(name, StringComparison.OrdinalIgnoreCase)
					|| (e.Value?.IndexOf("%" + name + "%", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)).ToList();
			if (missing.Count == 0) return (instruction, null);

			var expected = "{" + string.Join(", ", named.Select(n => $"\"{n}\": \"%{n}%\"")) + "}";
			return (instruction, new StepBuilderError($"The step returns {string.Join(", ", missing.Select(m => "%" + m + "%"))} but the parameter variables does not contain it. variables must hold every variable the step names, e.g. variables={expected}", step, "ReturnIsMissingVariable", Retry: true));
		}

		/// <summary>
		/// `set %rep% = %s.Trim().Replace(" ", "-")%` is one variable with calls on it, not a calculation. Built as
		/// SetVariableWithCalculation the expression became `%s%.Trim().Replace(...)`, which NCalc cannot parse, so it failed
		/// at run time ("Error parsing the expression").
		/// </summary>
		public async Task<(Instruction?, IBuilderError?)> BuilderSetVariableWithCalculation(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			var assignment = Regex.Match(step.Text.Trim(), @"^-?\s*set\s+(?:var(?:iable)?\s+)?%[^%]+%\s*=\s*(.+?)\s*(?:,\s*round\b.*)?$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			if (!assignment.Success) return (instruction, null);

			var value = assignment.Groups[1].Value.Trim();
			if (!PLang.Utils.VariableHelper.IsVariable(value)) return (instruction, null);

			return (instruction, new StepBuilderError($"`{value}` is one variable (with its methods called), not a calculation. Use SetVariable with value=\"{value}\".", step, "NotACalculation", Retry: true));
		}

		private static List<KeyValuePair<string, string?>> GetEntries(object? value)
		{
			if (value is string str && str.TrimStart().StartsWith("{"))
			{
				try { value = JObject.Parse(str); } catch { return []; }
			}
			if (value is JObject jObject)
			{
				return jObject.Properties().Select(p => new KeyValuePair<string, string?>(p.Name, p.Value?.ToString())).ToList();
			}
			if (value is System.Collections.IDictionary dictionary)
			{
				var list = new List<KeyValuePair<string, string?>>();
				foreach (System.Collections.DictionaryEntry entry in dictionary) list.Add(new(entry.Key.ToString() ?? "", entry.Value?.ToString()));
				return list;
			}
			return [];
		}
	}
}
