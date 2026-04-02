using System.Text.RegularExpressions;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory.Navigators;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Data — navigation concern.
/// GetChild traverses dot notation, bracket indexing, and method calls into nested values.
/// Method calls: %data.grep("pattern").maxLength(100)% — chainable, return Data.
/// </summary>
public partial class Data
{
    private const int MaxNavigationDepth = 100;

    /// <summary>
    /// Gets a child value by path (dot notation, index, or method call).
    /// </summary>
    public virtual Data? GetChild(string path, int depth = 0)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        if (depth > MaxNavigationDepth)
            return FromError(new ServiceError(
                $"Navigation path exceeds maximum depth ({MaxNavigationDepth})",
                "NavigationDepthExceeded", 400));

        // Parse next segment — respects parentheses (method calls), brackets, and quotes
        var (segment, remaining) = ParseNextSegment(path);

        // Handle bracket notation: [0] or [key] — extract content
        if (segment.StartsWith('[') && segment.EndsWith(']'))
        {
            segment = segment[1..^1];
            // Strip leading dot from remaining if present
            if (remaining.StartsWith('.'))
                remaining = remaining[1..];
        }

        // Check for method call: segment contains (...)
        var parenIndex = segment.IndexOf('(');
        if (parenIndex > 0 && segment.EndsWith(')'))
        {
            var methodName = segment[..parenIndex];
            var argsStr = segment[(parenIndex + 1)..^1]; // strip ( and )
            var result = InvokeMethod(methodName, argsStr);
            if (result == null) return null;

            if (string.IsNullOrEmpty(remaining))
                return result;
            return result.GetChild(remaining, depth + 1);
        }

        // Get child value from current value
        var childValue = GetChildValue(segment);
        if (childValue == null)
            return null;

        var child = new Data(segment, childValue, parent: this);
        child.Context = _context;

        // Inject context on IContext values during traversal
        if (childValue is PLang.Runtime2.modules.IContext contextual && _context != null)
            contextual.Context = _context;

        if (string.IsNullOrEmpty(remaining))
            return child;

        return child.GetChild(remaining, depth + 1);
    }

    /// <summary>
    /// Parses the next segment from a dot-path, handling parentheses and brackets.
    /// Returns (segment, remaining).
    /// </summary>
    private static (string segment, string remaining) ParseNextSegment(string path)
    {
        int depth = 0;
        bool inQuote = false;
        char quoteChar = '\0';

        for (int i = 0; i < path.Length; i++)
        {
            var c = path[i];

            // Track quotes
            if ((c == '"' || c == '\'') && depth <= 1)
            {
                if (!inQuote) { inQuote = true; quoteChar = c; }
                else if (c == quoteChar) { inQuote = false; }
                continue;
            }

            if (inQuote) continue;

            // Track parentheses depth
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }

            // Split at open bracket at depth 0: "Steps[0]" → ("Steps", "[0]")
            if (c == '[' && depth == 0 && i > 0)
            {
                return (path[..i], path[i..]);
            }

            // Track bracket depth
            if (c == '[') { depth++; continue; }
            if (c == ']') { depth--; continue; }

            // Split on dot only at depth 0
            if (c == '.' && depth == 0)
            {
                return (path[..i], path[(i + 1)..]);
            }
        }

        return (path, "");
    }

    /// <summary>
    /// Invokes a method-like navigation on Data. Chainable — returns Data.
    /// Override in subclasses to add domain-specific methods.
    /// </summary>
    protected virtual Data? InvokeMethod(string method, string args)
    {
        var str = Value?.ToString();

        return method.ToLowerInvariant() switch
        {
            "grep" => InvokeGrep(args),
            "grepcount" => InvokeGrepCount(args),
            "maxlength" => MaxLength(str, ParseIntArg(args)),
            "trim" => new Data(Name, str?.Trim()),
            "tolower" => new Data(Name, str?.ToLowerInvariant()),
            "toupper" => new Data(Name, str?.ToUpperInvariant()),
            "replace" => Replace(str, args),
            _ => null
        };
    }

    private Data InvokeGrep(string args)
    {
        var provider = ResolveGrepProvider();
        var (pattern, contextLines) = ParseGrepArgs(args);
        return provider.Grep(this, pattern ?? "", contextLines);
    }

    private Data InvokeGrepCount(string args)
    {
        var provider = ResolveGrepProvider();
        return provider.GrepCount(this, ParseStringArg(args) ?? "");
    }

    private Providers.IGrepProvider ResolveGrepProvider()
    {
        var engine = _context?.Engine;
        if (engine != null)
        {
            var result = engine.Providers.Get<Providers.IGrepProvider>();
            if (result?.Value is Providers.IGrepProvider provider) return provider;
        }
        return new Providers.DefaultGrepProvider();
    }

    private static (string? pattern, int contextLines) ParseGrepArgs(string args)
    {
        // grep("pattern") or grep("pattern", 3)
        var parts = Regex.Matches(args, @"""([^""]*)""|'([^']*)'|(\d+)");
        string? pattern = null;
        int contextLines = 0;

        foreach (Match m in parts)
        {
            if (m.Groups[1].Success) pattern ??= m.Groups[1].Value;
            else if (m.Groups[2].Success) pattern ??= m.Groups[2].Value;
            else if (m.Groups[3].Success && pattern != null) int.TryParse(m.Groups[3].Value, out contextLines);
        }

        return (pattern, contextLines);
    }

    private Data MaxLength(string? text, int max)
    {
        if (text == null) return new Data(Name, "");
        if (max <= 0) return new Data(Name, text); // 0 = no limit
        return new Data(Name, text.Length > max ? text[..max] + "..." : text);
    }

    private Data Replace(string? text, string args)
    {
        if (text == null) return new Data(Name, "");
        // Parse two string args: replace("old", "new")
        var parts = Regex.Matches(args, @"""([^""]*)""|'([^']*)'");
        if (parts.Count >= 2)
        {
            var oldStr = parts[0].Groups[1].Success ? parts[0].Groups[1].Value : parts[0].Groups[2].Value;
            var newStr = parts[1].Groups[1].Success ? parts[1].Groups[1].Value : parts[1].Groups[2].Value;
            return new Data(Name, text.Replace(oldStr, newStr));
        }
        return new Data(Name, text);
    }

    private static string? ParseStringArg(string args)
    {
        args = args.Trim();
        if (args.StartsWith('"') && args.EndsWith('"')) return args[1..^1];
        if (args.StartsWith('\'') && args.EndsWith('\'')) return args[1..^1];
        return args;
    }

    private static int ParseIntArg(string args)
    {
        args = args.Trim();
        return int.TryParse(args, out var n) ? n : 0;
    }

    private object? GetChildValue(string key)
    {
        // First check properties on the Data object itself (e.g., PathData.Exists)
        var ownProp = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (ownProp != null && (ownProp.DeclaringType != typeof(Data)
            || key.Equals("Success", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Error", StringComparison.OrdinalIgnoreCase)))
        {
            return ownProp.GetValue(this);
        }

        var val = Value;
        if (val == null) return null;
        return ValueNavigators.Navigate(val, key);
    }
}
