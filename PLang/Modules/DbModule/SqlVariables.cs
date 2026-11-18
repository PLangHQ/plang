using System.Text;
using System.Text.RegularExpressions;
using static PLang.Modules.DbModule.Program;

namespace PLang.Modules.DbModule;

/// <summary>
/// Reconciles the %variables% of a built sql statement with its parameters. A %variable% that stands
/// where a value goes is a parameter: the sql carries @name and the parameter list carries
/// ParameterInfo("@name", "%name%"). The llm is asked for exactly that, but a long statement comes back
/// with some occurrences still written as %name%, and the runtime then pastes the raw value into the
/// sql text instead of binding it.
/// </summary>
public static class SqlVariables
{
	/// <summary>A %variable% written in the sql text, outside string literals, quoted identifiers and comments.</summary>
	public record Occurrence(string Name, int Index, int Length, bool IsValue);

	/// <summary>The sql and parameters after reconciling them, or the reason they cannot be reconciled.</summary>
	public record Result(string Sql, List<ParameterInfo> Parameters, List<string> Added, string? Error);

	private static readonly Regex VariableAt = new(@"\G%([A-Za-z_][A-Za-z0-9_.\[\]]*)%", RegexOptions.Compiled);
	private static readonly Regex VariablesInText = new(@"(?<!\\)%([^\s%]+)%", RegexOptions.Compiled);

	// Keywords after which a name is an object, not a value: `from %table%` is replaced in the text at run
	// time, a parameter cannot stand there.
	private static readonly HashSet<string> ObjectKeywords = new(StringComparer.OrdinalIgnoreCase)
		{ "from", "join", "into", "update", "table", "view", "index", "trigger", "pragma", "exists" };

	/// <summary>
	/// Every %variable% in the sql with whether it stands where a value goes. A % inside a string
	/// literal ('%x%' in a LIKE), a quoted identifier or a comment is not a variable and is not returned.
	/// </summary>
	public static List<Occurrence> Find(string sql)
	{
		var found = new List<Occurrence>();
		int i = 0;
		while (i < sql.Length)
		{
			char c = sql[i];
			if (c == '\'' || c == '"' || c == '`')
			{
				i = SkipQuoted(sql, i, c);
				continue;
			}
			if (c == '[' && i > 0 && !IsWordChar(sql[i - 1]))
			{
				int close = sql.IndexOf(']', i + 1);
				i = close < 0 ? sql.Length : close + 1;
				continue;
			}
			if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
			{
				int end = sql.IndexOf('\n', i);
				i = end < 0 ? sql.Length : end + 1;
				continue;
			}
			if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
			{
				int end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
				i = end < 0 ? sql.Length : end + 2;
				continue;
			}
			if (c == '%')
			{
				var match = VariableAt.Match(sql, i);
				if (match.Success)
				{
					found.Add(new Occurrence(match.Groups[1].Value, i, match.Length, StandsWhereValueGoes(sql, i, match.Length)));
					i += match.Length;
					continue;
				}
			}
			i++;
		}
		return found;
	}

	private static int SkipQuoted(string sql, int start, char quote)
	{
		int i = start + 1;
		while (i < sql.Length)
		{
			if (sql[i] == quote)
			{
				// a doubled quote is an escaped quote inside the literal
				if (i + 1 < sql.Length && sql[i + 1] == quote) { i += 2; continue; }
				return i + 1;
			}
			i++;
		}
		return sql.Length;
	}

	private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

	private static bool StandsWhereValueGoes(string sql, int index, int length)
	{
		// part of a longer name: users_%year%, %schema%.users
		if (index > 0 && (IsWordChar(sql[index - 1]) || sql[index - 1] == '.')) return false;
		int after = index + length;
		if (after < sql.Length && (IsWordChar(sql[after]) || sql[after] == '.')) return false;

		int before = index - 1;
		while (before >= 0 && char.IsWhiteSpace(sql[before])) before--;
		int wordEnd = before;
		while (before >= 0 && IsWordChar(sql[before])) before--;
		if (wordEnd > before && ObjectKeywords.Contains(sql.Substring(before + 1, wordEnd - before))) return false;

		return true;
	}

	/// <summary>The names of the %variables% a step's text mentions.</summary>
	public static HashSet<string> VariablesInStep(string? stepText)
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrEmpty(stepText)) return names;
		foreach (Match m in VariablesInText.Matches(stepText)) names.Add(m.Groups[1].Value);
		return names;
	}

	/// <summary>
	/// Replaces every %variable% that stands where a value goes by its @parameter, adding the parameter when the
	/// llm did not list it, and reports what cannot be reconciled: a %variable% that is neither one of the
	/// step's variables nor a listed parameter, and a listed parameter the sql does not use.
	/// </summary>
	public static Result Fix(string sql, List<ParameterInfo>? parameters, string? stepText, IEnumerable<string>? tableNames = null)
	{
		var result = new List<ParameterInfo>(parameters ?? new());
		var added = new List<string>();
		var problems = new List<string>();
		var stepVariables = VariablesInStep(stepText);
		var objectNames = (tableNames ?? []).ToList();

		var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var occurrences = Find(sql)
			.Where(o => o.IsValue && !objectNames.Any(t => t.Contains($"%{o.Name}%", StringComparison.OrdinalIgnoreCase)))
			.ToList();

		foreach (var name in occurrences.Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var declared = result.FirstOrDefault(p => string.Equals(p.VariableNameOrValue?.ToString()?.Trim(), $"%{name}%", StringComparison.OrdinalIgnoreCase));
			if (declared != null)
			{
				replacements[name] = "@" + ParameterNameOf(declared.ParameterName);
			}
			else if (stepVariables.Contains(name))
			{
				var parameterName = UniqueParameterName(name, result);
				result.Add(new ParameterInfo("System.Object", "@" + parameterName, $"%{name}%"));
				added.Add("@" + parameterName);
				replacements[name] = "@" + parameterName;
			}
			else
			{
				problems.Add($"The sql contains %{name}% but the step does not mention it and no parameter carries it. Use the variables the step names, or write the value as it is.");
			}
		}

		var fixedSql = sql;
		foreach (var occurrence in occurrences.OrderByDescending(o => o.Index))
		{
			if (!replacements.TryGetValue(occurrence.Name, out var parameter)) continue;
			fixedSql = fixedSql.Substring(0, occurrence.Index) + parameter + fixedSql.Substring(occurrence.Index + occurrence.Length);
		}

		// A parameter the sql does not use is not an error: binding an unused parameter is harmless, and a step that
		// used to build must not start failing because the model listed one more parameter than the sql needs.

		string? error = null;
		if (problems.Count > 0)
		{
			error = string.Join("\n", problems) +
				"\nEvery %variable% of the step that stands where a value goes, in any clause: where, case, join, a CTE (with .. as), a subquery, a window function, the select list, MUST be written as the same @parameter in the sql text and listed once in the parameters, e.g. `case %tab% when 'a' then 1 end` => sql `case @tab when 'a' then 1 end`, parameter ParameterInfo(\"System.String\", \"@tab\", \"%tab%\"). The sql text must hold no %variable% afterwards.";
		}
		return new Result(fixedSql, result, added, error);
	}

	// A parameter name never carries the @ or a dot: the runtime strips both before it binds the value.
	private static string ParameterNameOf(string parameterName) => parameterName.Replace("@", "").Replace(".", "");

	private static string UniqueParameterName(string variableName, List<ParameterInfo> existing)
	{
		var baseName = Regex.Replace(variableName, @"[^A-Za-z0-9_]", "");
		if (baseName.Length == 0 || char.IsDigit(baseName[0])) baseName = "p" + baseName;

		var name = baseName;
		int n = 2;
		while (existing.Any(p => string.Equals(ParameterNameOf(p.ParameterName), name, StringComparison.OrdinalIgnoreCase)))
		{
			name = baseName + n++;
		}
		return name;
	}

	/// <summary>Methods whose sql is built from the step's own text and so is bound through a parameter list.</summary>
	private static readonly HashSet<string> MethodsWithParameters = new(StringComparer.OrdinalIgnoreCase)
	{
		"Select", "SelectOneRow", "SelectWithMultipleDataSources", "SelectOneRowWithMultipleDataSources",
		"Update", "Delete", "Insert", "InsertOrUpdate", "InsertAndSelectIdOfInsertedRow", "InsertOrUpdateAndSelectIdOfRow", "Execute"
	};

	/// <summary>
	/// Applies Fix to a built function. Returns the function with the sql and its parameter list reconciled,
	/// or the error to hand back to the llm.
	/// </summary>
	public static (Builder.DbGenericFunction Function, string? Error) Apply(Builder.DbGenericFunction gf, string? stepText)
	{
		if (!MethodsWithParameters.Contains(gf.Name) || gf.Parameters == null) return (gf, null);

		int sqlIdx = gf.Parameters.FindIndex(p => p.Name == "sql");
		if (sqlIdx == -1) return (gf, null);

		var sql = gf.Parameters[sqlIdx].Value?.ToString();
		if (string.IsNullOrWhiteSpace(sql) || PLang.Utils.VariableHelper.IsVariable(sql)) return (gf, null);

		var listName = gf.Parameters.Any(p => p.Name == "sqlParameters") ? "sqlParameters"
			: gf.Parameters.Any(p => p.Name == "parameters") ? "parameters"
			: gf.Name.Equals("Execute", StringComparison.OrdinalIgnoreCase) ? "parameters" : "sqlParameters";

		var parameters = gf.GetParameter<List<ParameterInfo>>(listName);
		var result = Fix(sql, parameters, stepText, gf.TableNames);
		if (result.Error != null) return (gf, result.Error);

		var updated = gf.Parameters.ToList();
		updated[sqlIdx] = updated[sqlIdx] with { Value = result.Sql };

		int listIdx = updated.FindIndex(p => p.Name == listName);
		if (listIdx != -1)
		{
			updated[listIdx] = updated[listIdx] with { Value = result.Parameters };
		}
		else if (result.Parameters.Count > 0)
		{
			updated.Add(new Builder.Parameter("System.Collections.Generic.List`1[PLang.Modules.DbModule.Program+ParameterInfo]", listName, result.Parameters));
		}
		return (gf with { Parameters = updated }, null);
	}
}
