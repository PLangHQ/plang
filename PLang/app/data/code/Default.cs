using System.Text;
using System.Text.RegularExpressions;

namespace app.data.code;

/// <summary>
/// Default grep provider for text/string data.
/// Line-based matching with regex, line numbers, and context lines.
/// </summary>
public class Default : IGrep
{
    public string Name => "default";
    public bool IsDefault { get; set; } = true;
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public @this Grep(@this data, string pattern, int contextLines = 0)
    {
        var text = data.Peek()?.ToString();
        if (text == null || string.IsNullOrEmpty(pattern))
            return new @this(data.Name, "");

        var lines = text.Split('\n');
        var matchIndices = FindMatchingLines(lines, pattern);

        if (matchIndices.Count == 0)
            return new @this(data.Name, "");

        if (contextLines <= 0)
        {
            // No context — just matching lines with line numbers
            var sb = new StringBuilder();
            foreach (var i in matchIndices)
            {
                sb.AppendLine($"{i + 1}: {lines[i]}");
            }
            return new @this(data.Name, sb.ToString().TrimEnd());
        }

        // With context lines
        return FormatWithContext(lines, matchIndices, contextLines, data.Name);
    }

    public @this GrepCount(@this data, string pattern)
    {
        var text = data.Peek()?.ToString();
        if (text == null || string.IsNullOrEmpty(pattern))
            return new @this(data.Name, 0);

        var lines = text.Split('\n');
        var count = FindMatchingLines(lines, pattern).Count;
        return new @this(data.Name, count);
    }

    private static List<int> FindMatchingLines(string[] lines, string pattern)
    {
        var matches = new List<int>();
        Regex? regex = null;

        try { regex = new Regex(pattern, RegexOptions.IgnoreCase); }
        catch (ArgumentException) { /* invalid regex — fallback to contains */ }

        for (int i = 0; i < lines.Length; i++)
        {
            bool isMatch = regex != null
                ? regex.IsMatch(lines[i])
                : lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase);

            if (isMatch) matches.Add(i);
        }

        return matches;
    }

    private static @this FormatWithContext(string[] lines, List<int> matchIndices, int contextLines, string name)
    {
        var sb = new StringBuilder();
        var printed = new HashSet<int>();
        bool needsSeparator = false;

        foreach (var matchIdx in matchIndices)
        {
            var start = Math.Max(0, matchIdx - contextLines);
            var end = Math.Min(lines.Length - 1, matchIdx + contextLines);

            // Add separator between non-adjacent groups
            if (needsSeparator && !printed.Contains(start - 1))
                sb.AppendLine("--");

            for (int i = start; i <= end; i++)
            {
                if (printed.Contains(i)) continue;
                printed.Add(i);

                var prefix = i == matchIdx ? ">" : " ";
                sb.AppendLine($"{prefix}{i + 1}: {lines[i]}");
            }

            needsSeparator = true;
        }

        return new @this(name, sb.ToString().TrimEnd());
    }
}
