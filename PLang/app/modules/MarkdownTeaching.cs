using System.IO;

namespace app.modules;

/// <summary>
/// Loads per-action LLM teaching from markdown files under
/// <c>os/system/modules/&lt;module&gt;/{module,&lt;action&gt;}.{notes,examples,description}.md</c>.
/// Architect plan: <c>.bot/compile-llm-notes-per-action/architect/plan.md</c>.
///
/// Layers (module-level + action-level) are kept split — renderer concats at render
/// time. <c>module</c> is a reserved stem; no action may be literally named "module".
/// Empty / missing files yield null fields (or empty paragraph list). Orphan files
/// (stem is not <c>module</c> and not a registered action) are surfaced via
/// <see cref="ScanOrphans"/>; the loader itself never throws.
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
    public static Loaded Load(string? modulesRoot, string moduleName, string actionName)
    {
        if (string.IsNullOrEmpty(modulesRoot)) return Empty;
        var folder = Path.Combine(modulesRoot, moduleName);
        if (!Directory.Exists(folder)) return Empty;

        var notes          = ReadOrNull(Path.Combine(folder, $"{actionName}.notes.md"));
        var examples       = ReadParagraphs(Path.Combine(folder, $"{actionName}.examples.md"));
        var description    = ReadOrNull(Path.Combine(folder, $"{actionName}.description.md"));
        var modNotes       = ReadOrNull(Path.Combine(folder, $"{ModuleStem}.notes.md"));
        var modExamples    = ReadParagraphs(Path.Combine(folder, $"{ModuleStem}.examples.md"));
        var modDescription = ReadOrNull(Path.Combine(folder, $"{ModuleStem}.description.md"));

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
    public static IEnumerable<Orphan> ScanOrphans(
        string? modulesRoot,
        Func<string, IEnumerable<string>> registeredActions)
    {
        if (string.IsNullOrEmpty(modulesRoot)) yield break;
        if (!Directory.Exists(modulesRoot)) yield break;

        foreach (var moduleDir in Directory.EnumerateDirectories(modulesRoot))
        {
            var moduleName = Path.GetFileName(moduleDir);
            var actions = new HashSet<string>(
                registeredActions(moduleName) ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(moduleDir))
            {
                var name = Path.GetFileName(file);
                if (!IsTeachingFile(name, out var stem)) continue;
                if (string.Equals(stem, ModuleStem, StringComparison.OrdinalIgnoreCase)) continue;
                if (actions.Contains(stem)) continue;
                yield return new Orphan(moduleName, stem, file);
            }
        }
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

    private static string? ReadOrNull(string path)
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Read a file as a list of paragraphs (split on one-or-more blank lines).
    /// Empty/missing → empty list. Each paragraph is trimmed.
    /// </summary>
    private static List<string> ReadParagraphs(string path)
    {
        var result = new List<string>();
        if (!File.Exists(path)) return result;
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return result;
        // Split on blank line (\n\n, with possible \r and surrounding whitespace).
        var parts = System.Text.RegularExpressions.Regex.Split(text, @"\r?\n\s*\r?\n");
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
