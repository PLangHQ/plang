using System.Text.RegularExpressions;
using app.error;

namespace app.data;

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
    /// Never returns null — returns Data.NotFound(key) when the path doesn't resolve.
    /// </summary>
    public virtual @this GetChild(string path, int depth = 0)
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

        // Quoted-key segment: "manual-checkpoint" or 'foo' — strip quotes and treat as a
        // literal dictionary key. Lets paths like Tags."key.with.dots" reach a key that
        // would otherwise be split on the inner dot. Bypasses method-call and `!`
        // infrastructure parsing (a quoted segment can't be either).
        bool isQuoted = segment.Length >= 2
            && ((segment[0] == '"' && segment[^1] == '"')
                || (segment[0] == '\'' && segment[^1] == '\''));
        if (isQuoted)
            segment = segment[1..^1];

        // Check for method call: segment contains (...)
        if (!isQuoted)
        {
            var parenIndex = segment.IndexOf('(');
            if (parenIndex > 0 && segment.EndsWith(')'))
            {
                var methodName = segment[..parenIndex];
                var argsStr = segment[(parenIndex + 1)..^1]; // strip ( and )
                var result = InvokeMethod(methodName, argsStr);
                if (!result.IsInitialized) return result;

                if (string.IsNullOrEmpty(remaining))
                    return result;
                return result.GetChild(remaining, depth + 1);
            }
        }

        // ! prefix = Data infrastructure access (Name, Error, Success, Type, Properties)
        // . prefix (default) = domain/value navigation
        bool isInfrastructure = !isQuoted && segment.StartsWith('!');
        if (isInfrastructure)
            segment = segment[1..]; // strip the !

        var child = isInfrastructure
            ? GetInfrastructureValue(segment)
            : GetChildValue(segment);
        if (!child.IsInitialized) return child;

        // Inject context on IContext values during traversal
        if (child.Value is app.module.IContext contextual)
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
    protected virtual @this InvokeMethod(string method, string args)
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
            _ => NotFound(method)
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

    private app.data.code.IGrep ResolveGrepProvider()
    {
        var app = _context?.App;
        if (app != null)
        {
            var result = app.Code.Get<app.data.code.IGrep>();
            if (result?.Value is app.data.code.IGrep provider) return provider;
        }
        return new app.data.code.Default();
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

    /// <summary>
    /// Navigates into the Data's value by key. Checks Properties, subclass properties,
    /// navigators, and whitelisted base properties. Returns Data — never null.
    /// </summary>
    private @this GetChildValue(string key)
    {
        var val = Value;

        // If Value is a Data object (e.g., DynamicData wrapping Identity),
        // navigate into the VALUE first — it's the real object
        if (val is @this dataVal)
        {
            var dataChild = dataVal.GetChildValue(key);
            if (dataChild.IsInitialized) return dataChild;
        }

        // Check Data subclass properties (e.g., DynamicData)
        // These are declared on the subclass, not on Data itself.
        var ownProp = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (ownProp != null && ownProp.DeclaringType != typeof(@this))
            return new @this(key, ownProp.GetValue(this), parent: this);

        // Lazy type conversion — if value is a string with a typed Data, convert on first navigation
        if (val is string && _type != null)
        {
            ConvertValue();
            val = Value;
        }

        // Navigate the Value object via registered navigator (dict, list, CLR reflection, etc.)
        // Value wins over Properties on the dot path — Stage 4 made `!` the Properties operator,
        // dot stays on the Value graph. Properties[key] is kept as a fallback below so existing
        // call sites that read e.g. `%!data.branchIndex%` (Properties metadata via dot) still
        // resolve when Value has no matching child.
        if (val != null)
        {
            var navigator = _context?.App?.Navigator?.Get(val.GetType());
            if (navigator != null)
            {
                var navResult = navigator.Navigate(this, key);
                if (navResult.IsInitialized) return navResult;
            }

            // Fallback when no app context (e.g., during deserialization)
            var fallbackResult = global::app.variable.navigator.ValueNavigators.Navigate(this, key);
            if (fallbackResult.IsInitialized) return fallbackResult;
        }

        // Properties fallback — only reached when Value-graph navigation produced
        // no initialized child. Lets the legacy `%!data.branchIndex%` shape keep
        // working while the canonical Properties access path stays `!`.
        var prop = Properties[key];
        if (prop != null) return new @this(key, prop, parent: this);

        // Fallback: whitelisted Data base properties (Success, Error, Name, Type).
        // Checked last so %user.name% navigates to the Value's "name" property
        // rather than Data.Name. `Type` joins the whitelist post-Stage-4 so
        // %x.Type.Name% / %x.Type.Kind% reads the entity from any Data.
        if (ownProp != null && (
            key.Equals("Success", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Name", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Type", StringComparison.OrdinalIgnoreCase)))
        {
            return new @this(key, ownProp.GetValue(this), parent: this);
        }

        return NotFound(key);
    }

    /// <summary>
    /// Accesses Data's own infrastructure properties (Name, Type, Error, Success, Properties, etc.).
    /// Used when navigating with ! prefix: %user!Name%, %result!Error%, etc.
    /// Full hierarchy reflection — reaches any property on the Data class itself.
    /// </summary>
    private @this GetInfrastructureValue(string key)
    {
        // Stage 4: Properties win first — `%x!cost%` reads Properties["cost"].
        // Reflection-discovered Data infrastructure (Name, Type, Error, Success,
        // subclass properties like Llm) stays available via the same operator
        // when the key isn't a Property — keeps `%result!Llm%` working while
        // the Properties scope becomes the primary `!` namespace.
        if (Properties.ContainsKey(key))
            return new @this(key, Properties[key], parent: this);

        var prop = typeof(@this).GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null)
            return new @this(key, prop.GetValue(this), parent: this);

        // Also check subclass properties (e.g., Path.Exists IS infrastructure too)
        prop = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null)
            return new @this(key, prop.GetValue(this), parent: this);

        return NotFound(key);
    }
}
