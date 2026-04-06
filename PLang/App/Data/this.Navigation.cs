using System.Text.RegularExpressions;
using App.Errors;
using App.Data.Navigators;

namespace App.Data;

/// <summary>
/// Data — navigation concern.
/// GetChild traverses dot notation, bracket indexing, and method calls into nested values.
/// Method calls: %data.grep("pattern").maxLength(100)% — chainable, return Data.
/// </summary>
public partial class @this
{
    private const int MaxNavigationDepth = 100;

    /// <summary>
    /// Gets a child value by path (dot notation, index, or method call).
    /// </summary>
    public virtual @this? GetChild(string path, int depth = 0)
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

        // ! prefix = Data infrastructure access (Name, Error, Success, Type, Properties)
        // . prefix (default) = domain/value navigation
        bool isInfrastructure = segment.StartsWith('!');
        if (isInfrastructure)
            segment = segment[1..]; // strip the !

        var childValue = isInfrastructure
            ? GetInfrastructureValue(segment)
            : GetChildValue(segment);
        if (childValue == null)
            return null;

        var child = new @this(segment, childValue, parent: this);
        child.Context = _context;

        // Inject context on IContext values during traversal
        if (childValue is App.modules.IContext contextual && _context != null)
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
    protected virtual @this? InvokeMethod(string method, string args)
    {
        var str = Value?.ToString();

        return method.ToLowerInvariant() switch
        {
            "grep" => InvokeGrep(args),
            "grepcount" => InvokeGrepCount(args),
            "maxlength" => MaxLength(str, ParseIntArg(args)),
            "trim" => new @this(Name, str?.Trim()),
            "tolower" => new @this(Name, str?.ToLowerInvariant()),
            "toupper" => new @this(Name, str?.ToUpperInvariant()),
            "replace" => Replace(str, args),
            _ => null
        };
    }

    private @this InvokeGrep(string args)
    {
        var provider = ResolveGrepProvider();
        var (pattern, contextLines) = ParseGrepArgs(args);
        return provider.Grep(this, pattern ?? "", contextLines);
    }

    private @this InvokeGrepCount(string args)
    {
        var provider = ResolveGrepProvider();
        return provider.GrepCount(this, ParseStringArg(args) ?? "");
    }

    private Providers.IGrepProvider ResolveGrepProvider()
    {
        var engine = _context?.App;
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

    private @this MaxLength(string? text, int max)
    {
        if (text == null) return new @this(Name, "");
        if (max <= 0) return new @this(Name, text); // 0 = no limit
        return new @this(Name, text.Length > max ? text[..max] + "..." : text);
    }

    private @this Replace(string? text, string args)
    {
        if (text == null) return new @this(Name, "");
        // Parse two string args: replace("old", "new")
        var parts = Regex.Matches(args, @"""([^""]*)""|'([^']*)'");
        if (parts.Count >= 2)
        {
            var oldStr = parts[0].Groups[1].Success ? parts[0].Groups[1].Value : parts[0].Groups[2].Value;
            var newStr = parts[1].Groups[1].Success ? parts[1].Groups[1].Value : parts[1].Groups[2].Value;
            return new @this(Name, text.Replace(oldStr, newStr));
        }
        return new @this(Name, text);
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
        var val = Value;

        // If Value is a Data object (e.g., DynamicData wrapping Identity),
        // navigate into the VALUE first — it's the real object
        if (val is @this dataVal)
        {
            var dataProp = dataVal.Properties[key];
            if (dataProp != null) return dataProp.Value;
            var dataChild = dataVal.GetChildValue(key);
            if (dataChild != null) return dataChild;
        }

        // Check Data subclass properties (e.g., Path.Exists, Identity.PublicKey)
        // These are declared on the subclass, not on Data itself.
        var ownProp = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (ownProp != null && ownProp.DeclaringType != typeof(@this))
            return ownProp.GetValue(this);

        // Check Data.Properties (extensible key-value pairs on the Data)
        var prop = Properties[key];
        if (prop != null) return prop.Value;

        // Navigate the Value object via registered navigator (dict, list, CLR reflection, etc.)
        if (val != null)
        {
            var navigator = _context?.App?.Navigators?.Get(val.GetType());
            var navResult = navigator?.Navigate(this, key);
            if (navResult != null) return navResult;

            // Fallback when no app context (e.g., during deserialization)
            var fallbackResult = Navigators.ValueNavigators.Navigate(val, key);
            if (fallbackResult != null) return fallbackResult;
        }

        // Fallback: whitelisted Data base properties (Success, Error, Name)
        // These are checked last so %user.name% navigates to the Value's "name"
        // property rather than Data.Name.
        if (ownProp != null && (
            key.Equals("Success", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Name", StringComparison.OrdinalIgnoreCase)))
        {
            return ownProp.GetValue(this);
        }

        return null;
    }

    /// <summary>
    /// Accesses Data's own infrastructure properties (Name, Type, Error, Success, Properties, etc.).
    /// Used when navigating with ! prefix: %user!Name%, %result!Error%, etc.
    /// Full hierarchy reflection — reaches any property on the Data class itself.
    /// </summary>
    private object? GetInfrastructureValue(string key)
    {
        var prop = typeof(@this).GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null)
            return prop.GetValue(this);

        // Also check subclass properties (e.g., Path.Exists IS infrastructure too)
        prop = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        return prop?.GetValue(this);
    }
}
