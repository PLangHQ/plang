namespace app.module;

/// <summary>
/// Loads per-action LLM teaching from markdown files under
/// <c>os/system/modules/&lt;module&gt;/{module,&lt;action&gt;}.{notes,examples,description}.md</c>.
///
/// <para>Layers (module-level + action-level) are kept split — renderer concats at render
/// time. <c>module</c> is a reserved stem; no action may be literally named "module".
/// Empty / missing files yield null fields (or empty paragraph list). Orphan files
/// (stem is not <c>module</c> and not a registered action) are surfaced via
/// <see cref="ScanOrphans"/>; the loader itself never throws.</para>
///
/// <para>All disk reads route through the <c>path.@this</c> verb surface so an
/// attacker-controlled <c>MarkdownTeachingRoot</c> can't side-channel reads past
/// <c>AuthGate</c>.</para>
/// </summary>
public static class MarkdownTeaching
{
    public const string ModuleStem = "module";

    public sealed record Loaded(
        string? Notes,
        string? ModuleNotes,
        string? Description,
        string? ModuleDescription,
        List<string> ExamplesMd,
        List<string> ModuleExamplesMd);

    /// <summary>
    /// Read teaching for one action. Returns empty/null fields when the module
    /// folder or the individual files are missing.
    /// </summary>
    public static async Task<Loaded> Load(path? modulesRoot, string moduleName, string actionName)
    {
        if (modulesRoot == null) return Empty;
        var folder = modulesRoot.Combine(moduleName);
        var folderExists = await folder.ExistsAsync();
        if (!folderExists.Success || (await folderExists.Value())?.Value != true) return Empty;

        var notes          = await ReadOrNull(folder.Combine($"{actionName}.notes.md"));
        var examples       = await ReadParagraphs(folder.Combine($"{actionName}.examples.md"));
        var description    = await ReadOrNull(folder.Combine($"{actionName}.description.md"));
        var modNotes       = await ReadOrNull(folder.Combine($"{ModuleStem}.notes.md"));
        var modExamples    = await ReadParagraphs(folder.Combine($"{ModuleStem}.examples.md"));
        var modDescription = await ReadOrNull(folder.Combine($"{ModuleStem}.description.md"));

        return new Loaded(notes, modNotes, description, modDescription, examples, modExamples);
    }

    /// <summary>
    /// Concat module-first + blank line + action — the rendered "Notes" body.
    /// Empty/missing on both sides → null (signals "omit block entirely"); one
    /// side present → just that side, no leading/trailing blank line.
    /// </summary>
    public static string? MergeLayers(string? moduleText, string? actionText)
    {
        var m = string.IsNullOrWhiteSpace(moduleText) ? null : moduleText!.Trim();
        var a = string.IsNullOrWhiteSpace(actionText) ? null : actionText!.Trim();
        if (m == null && a == null) return null;
        if (m == null) return a;
        if (a == null) return m;
        return m + "\n\n" + a;
    }

    /// <summary>
    /// Walk every module folder under <paramref name="modulesRoot"/> and report
    /// markdown files whose stem is not <c>module</c> and not in the per-module
    /// registered action set. One <see cref="Orphan"/> per file — no dedup.
    /// </summary>
    public static async Task<IReadOnlyList<Orphan>> ScanOrphans(
        path? modulesRoot,
        Func<string, IEnumerable<string>> registeredActions)
    {
        var orphans = new List<Orphan>();
        if (modulesRoot == null) return orphans;

        var rootExists = await modulesRoot.ExistsAsync();
        if (!rootExists.Success || (await rootExists.Value())?.Value != true) return orphans;

        // Recursive list of every file under modulesRoot. Keep only files whose
        // direct parent (the module folder) sits one level below modulesRoot —
        // the original loader's non-recursive per-module scan.
        var listResult = await modulesRoot.List("*", recursive: true);
        if (!listResult.Success || await listResult.Value() == null) return orphans;

        var rootAbs = modulesRoot.Absolute;
        var actionsByModule = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var list = await listResult.Value();
        foreach (var row in list!)
        {
            var file = await row.Value<global::app.type.path.@this>();
            if (file == null) continue;
            var moduleDir = file.Parent;
            if (moduleDir == null) continue;
            var grand = moduleDir.Parent;
            if (grand == null) continue;
            if (!string.Equals(grand.Absolute, rootAbs, global::app.type.path.@this.RootComparison)) continue;

            var fileName = file.FileName;
            if (!IsTeachingFile(fileName, out var stem)) continue;
            if (string.Equals(stem, ModuleStem, StringComparison.OrdinalIgnoreCase)) continue;

            var moduleName = moduleDir.FileName;
            if (!actionsByModule.TryGetValue(moduleName, out var actions))
            {
                actions = new HashSet<string>(
                    registeredActions(moduleName) ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                actionsByModule[moduleName] = actions;
            }
            if (actions.Contains(stem)) continue;
            orphans.Add(new Orphan(moduleName, stem, file.Absolute));
        }
        return orphans;
    }

    public sealed record Orphan(string Module, string Stem, string Path);

    private static bool IsTeachingFile(string fileName, out string stem)
    {
        stem = "";
        foreach (var suffix in Suffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stem = fileName.Substring(0, fileName.Length - suffix.Length);
                return true;
            }
        }
        return false;
    }

    private static readonly string[] Suffixes = { ".notes.md", ".examples.md", ".description.md" };

    private static async Task<string?> ReadOrNull(path file)
    {
        var result = await file.ReadText();
        if (!result.Success) return null;
        var text = (await result.Value())?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Read a file as a list of paragraphs (split on one-or-more blank lines).
    /// Empty/missing → empty list. Each paragraph is trimmed.
    /// </summary>
    private static async Task<List<string>> ReadParagraphs(path file)
    {
        var result = new List<string>();
        var read = await file.ReadText();
        if (!read.Success) return result;
        var text = (await read.Value())?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return result;
        // Split on blank line (\n\n, with possible \r and surrounding whitespace).
        var parts = System.Text.RegularExpressions.Regex.Split(text!, @"\r?\n\s*\r?\n");
        foreach (var p in parts)
        {
            var trimmed = p.Trim();
            if (trimmed.Length > 0) result.Add(trimmed);
        }
        return result;
    }

    private static readonly Loaded Empty =
        new(null, null, null, null, new List<string>(), new List<string>());
}
