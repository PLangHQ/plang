using PLang.Building.Model;
using PLang.Utils;
using System.Text;

var outDir = args.Length > 0
	? args[0]
	: Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Documentation", "modules", "reference");
outDir = Path.GetFullPath(outDir);
Directory.CreateDirectory(outDir);

var modules = typeof(PLang.Modules.BaseProgram).Assembly.GetTypes()
	.Where(t => t.Name == "Program" && t.FullName != null && t.FullName.StartsWith("PLang.Modules.") && !t.IsAbstract)
	.OrderBy(t => t.FullName)
	.ToList();

var shortNames = modules.ToDictionary(t => t, t => t.FullName!.Replace("PLang.Modules.", "").Replace(".Program", ""));

string Hint(Type type)
{
	var name = shortNames[type];
	var bare = name.EndsWith("Module") ? name[..^"Module".Length] : name;
	foreach (var candidate in new[] { bare, name })
	{
		var matches = modules.Count(m => shortNames[m].Contains(candidate, StringComparison.OrdinalIgnoreCase));
		if (matches == 1) return "[" + candidate.ToLowerInvariant() + "]";
	}
	return "[" + bare.ToLowerInvariant() + "] (ambiguous, matches "
		+ string.Join(", ", modules.Where(m => shortNames[m].Contains(bare, StringComparison.OrdinalIgnoreCase)).Select(m => shortNames[m]))
		+ ")";
}

string TypeName(string? type)
{
	var name = (type ?? "object").Replace("System.", "").Replace("Collections.Generic.", "");
	var tick = name.IndexOf('`');
	while (tick > -1)
	{
		var open = name.IndexOf('[', tick);
		if (open == -1) break;
		var close = name.LastIndexOf(']');
		if (close < open) break;
		var arguments = string.Join(", ", name[(open + 1)..close].Split(',').Select(a => a.Trim()));
		name = name[..tick] + "<" + arguments + ">" + name[(close + 1)..];
		tick = name.IndexOf('`');
	}
	return name.Replace("|", "\\|");
}

string Signature(MethodDescription method)
{
	var parts = (method.Parameters ?? new List<IPropertyDescription>()).Select(p =>
	{
		var text = TypeName(p.Type) + " " + p.Name;
		if (!p.IsRequired) text += " = " + (p.DefaultValue?.ToString() ?? "null");
		return text;
	});
	return method.MethodName + "(" + string.Join(", ", parts) + ") : " + TypeName(method.ReturnValue?.Type);
}

bool Informative(IPropertyDescription parameter) =>
	!string.IsNullOrWhiteSpace(parameter.Description)
	|| (parameter is EnumDescription e && !string.IsNullOrEmpty(e.AvailableValues))
	|| (parameter is ComplexDescription c && c.TypeProperties != null && c.TypeProperties.Count > 0);

void WriteParameters(StringBuilder sb, List<IPropertyDescription>? parameters, string indent)
{
	if (parameters == null || parameters.Count == 0) return;
	if (indent.Length == 0 && !parameters.Any(Informative)) return;

	foreach (var parameter in parameters)
	{
		sb.Append(indent).Append("- `").Append(parameter.Name).Append("` *").Append(TypeName(parameter.Type)).Append('*');
		if (!parameter.IsRequired) sb.Append(", default `").Append(parameter.DefaultValue?.ToString() ?? "null").Append('`');
		if (parameter is EnumDescription enumeration && !string.IsNullOrEmpty(enumeration.AvailableValues))
			sb.Append(", one of `").Append(enumeration.AvailableValues).Append('`');
		if (!string.IsNullOrWhiteSpace(parameter.Description))
			sb.Append(" — ").Append(parameter.Description!.Replace("\n", " ").Trim());
		sb.AppendLine();

		if (parameter is ComplexDescription complex && complex.TypeProperties != null && indent.Length < 4)
			WriteParameters(sb, complex.TypeProperties, indent + "  ");
	}
}

var index = new StringBuilder();
index.AppendLine("# Module reference");
index.AppendLine();
index.AppendLine("Generated from the running code by `Tools/ModuleDocGen`, so it lists exactly the methods the");
index.AppendLine("builder can map a step onto. Run it again after changing any module:");
index.AppendLine();
index.AppendLine("```");
index.AppendLine("dotnet run --project Tools/ModuleDocGen");
index.AppendLine("```");
index.AppendLine();
index.AppendLine("The hint in the second column is what you put in front of a step to force the module, e.g.");
index.AppendLine("`- [file] write %content% to file.txt`. The hint is matched as a substring of the module name");
index.AppendLine("(`StepBuilder.GetUserRequestedModule`), so any substring that hits one module works and");
index.AppendLine("`[list]` selects ListDictionaryModule just as well as `[listdictionary]`. A hint that matches");
index.AppendLine("several modules narrows the choice to those and leaves the last word to the llm, which is why");
index.AppendLine("the column spells out the ambiguous ones.");
index.AppendLine();
index.AppendLine("| Module | Hint | Methods | What it does |");
index.AppendLine("| --- | --- | --- | --- |");

var methodIndex = new List<(string Module, string Method, string Signature)>();
var modulesWithoutDescription = new List<string>();
var methodsWithoutDescription = new List<string>();

foreach (var module in modules)
{
	var (description, error) = new ClassDescriptionHelper().GetClassDescription(module);
	if (description == null)
	{
		Console.WriteLine($"skipped {shortNames[module]}: {error?.Message}");
		continue;
	}

	var name = shortNames[module];
	var summary = (description.Description ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
	if (summary.Length == 0) modulesWithoutDescription.Add(name);
	index.AppendLine($"| [{name}](./{name}.md) | `{Hint(module)}` | {description.Methods.Count} | {summary.MaxLength(160)} |");

	var sb = new StringBuilder();
	sb.AppendLine($"# {name}");
	sb.AppendLine();
	sb.AppendLine($"Hint: `{Hint(module)}`  ");
	sb.AppendLine($"Type: `{module.FullName}`");
	sb.AppendLine();
	if (!string.IsNullOrWhiteSpace(description.Description))
	{
		sb.AppendLine(description.Description!.Trim());
		sb.AppendLine();
	}

	sb.AppendLine("## Methods");
	sb.AppendLine();
	foreach (var method in description.Methods.OrderBy(m => m.MethodName))
	{
		methodIndex.Add((name, method.MethodName, Signature(method)));
		if (string.IsNullOrWhiteSpace(method.Description)) methodsWithoutDescription.Add(name + "." + method.MethodName);
		sb.AppendLine($"### {method.MethodName}");
		sb.AppendLine();
		sb.AppendLine("```");
		sb.AppendLine(Signature(method));
		sb.AppendLine("```");
		sb.AppendLine();
		if (!string.IsNullOrWhiteSpace(method.Description))
		{
			sb.AppendLine(method.Description!.Trim());
			sb.AppendLine();
		}
		WriteParameters(sb, method.Parameters, "");
		if (method.Examples != null && method.Examples.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("Examples:");
			sb.AppendLine();
			foreach (var example in method.Examples) sb.AppendLine("- " + example.Replace("\n", " ").Trim());
		}
		sb.AppendLine();
	}

	File.WriteAllText(Path.Combine(outDir, name + ".md"), sb.ToString());
}

index.AppendLine();
index.AppendLine("## Every method, alphabetically");
index.AppendLine();
index.AppendLine("| Method | Module | Signature |");
index.AppendLine("| --- | --- | --- |");
foreach (var entry in methodIndex.OrderBy(m => m.Method).ThenBy(m => m.Module))
{
	index.AppendLine($"| {entry.Method} | [{entry.Module}](./{entry.Module}.md) | `{entry.Signature.Replace("|", "\\|")}` |");
}

index.AppendLine();
index.AppendLine("## Gaps");
index.AppendLine();
index.AppendLine($"{modulesWithoutDescription.Count} modules and {methodsWithoutDescription.Count} of {methodIndex.Count} methods carry no");
index.AppendLine("`[Description]`, so the builder has nothing but the name to match a step against. Those are the");
index.AppendLine("ones the llm guesses at, and the ones worth writing first.");
index.AppendLine();
if (modulesWithoutDescription.Count > 0)
{
	index.AppendLine("Modules: " + string.Join(", ", modulesWithoutDescription));
	index.AppendLine();
}
if (methodsWithoutDescription.Count > 0)
{
	index.AppendLine("<details><summary>Methods with no description</summary>");
	index.AppendLine();
	foreach (var method in methodsWithoutDescription) index.AppendLine("- " + method);
	index.AppendLine();
	index.AppendLine("</details>");
}

File.WriteAllText(Path.Combine(outDir, "README.md"), index.ToString());
Console.WriteLine($"{modules.Count} modules, {methodIndex.Count} methods written to {outDir}");
