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
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public @this Grep(@this data, string pattern, int contextLines = 0)
    {
        var text = data.Peek()?.ToString();
        if (text == null || string.IsNullOrEmpty(pattern))
            return new @this(data.Name, "", context: data.Context);

        var lines = text.Split('\n');
        var matchIndices = FindMatchingLines(lines, pattern);

        if (matchIndices.Count == 0)
            return new @this(data.Name, "", context: data.Context);

        if (contextLines <= 0)
        {
            // No context — just matching lines with line numbers
            var sb = new StringBuilder();
            foreach (var i in matchIndices)
            {
                sb.AppendLine($"{i + 1}: {lines[i]}");
            }
            return new @this(data.Name, sb.ToString().TrimEnd(), context: data.Context);
        }

        // With context lines
        return FormatWithContext(lines, matchIndices, contextLines, data);
    }

    public @this GrepCount(@this data, string pattern)
    {
        var text = data.Peek()?.ToString();
        if (text == null || string.IsNullOrEmpty(pattern))
            return new @this(data.Name, 0, context: data.Context);

        var lines = text.Split('\n');
        var count = FindMatchingLines(lines, pattern).Count;
        return new @this(data.Name, count, context: data.Context);
    }

    private static List<int> FindMatchingLines(string[] lines, string pattern)
    {
        var matches = new List<int>();
        Regex regex;

        // a pattern that isn't a regex is the program's error, naming it — never a quiet "contains"
        try { regex = new Regex(pattern, RegexOptions.IgnoreCase); }
        catch (ArgumentException ex)
        {
            throw new global::app.error.AppException($"'{pattern}' is not a valid pattern: {ex.Message}", ex, "InvalidPattern", 400);
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (regex.IsMatch(lines[i])) matches.Add(i);
        }

        return matches;
    }

    private static @this FormatWithContext(string[] lines, List<int> matchIndices, int contextLines, @this data)
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

        return new @this(data.Name, sb.ToString().TrimEnd(), context: data.Context);
    }
}
