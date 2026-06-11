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
    public virtual async System.Threading.Tasks.ValueTask<@this> GetChild(string path, int depth = 0)
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
                return await result.GetChild(remaining, depth + 1);
            }
        }

        // ! prefix = Data infrastructure access (Name, Error, Success, Type, Properties)
        // . prefix (default) = domain/value navigation
        bool isInfrastructure = !isQuoted && segment.StartsWith('!');
        if (isInfrastructure)
            segment = segment[1..]; // strip the !

        var child = isInfrastructure
            ? GetInfrastructureValue(segment)
            : await GetChildValue(segment);
        if (!child.IsInitialized) return child;

        // Inject context on IContext values during traversal. A reference
        // (file/url) injects via Peek — opening the door here would read its
        // content on a property-plane hop (`%x!file!path%` must stay read-free).
        var injectTarget = child.Peek() is (global::app.type.file.@this or global::app.type.url.@this) and { } reference
            ? reference
            : await child.Value();
        if (injectTarget is app.module.IContext contextual)
            contextual.Context = _context;

        if (string.IsNullOrEmpty(remaining))
            return child;

        return await child.GetChild(remaining, depth + 1);
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

            // Chain-wide `!`: a second bang starts the next property-plane hop —
            // "!file!path" → ("!file", "!path"). The leading bang (i == 0) is the
            // current segment's own prefix, never a split point.
            if (c == '!' && depth == 0 && i > 0 && !inQuote)
            {
                return (path[..i], path[i..]);
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
        var str = Peek()?.ToString();

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
            if (app.Code.Get<app.data.code.IGrep>().Provider is { } g) return g;
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
    private async System.Threading.Tasks.ValueTask<@this> GetChildValue(string key)
    {
        // A value whose door does real work (file/url/source/computed) answers
        // Type metadata from its stamp WITHOUT opening the door — reading the
        // {type, kind} stamp is never an examination, so a later scalar use of
        // %x% stays unparsed. Scoped to door-owners so in-memory values keep
        // "value wins over Data infrastructure" (a dict's own "type" key still
        // wins for those).
        if (key.Equals("Type", StringComparison.OrdinalIgnoreCase)
            && _type != null && global::app.type.item.@this.OwnsDoor(_type))
            return new @this(key, Type, parent: this);

        // Navigation IS examination — the value door parses an un-narrowed
        // reference (file/url) or source form through the instance's own
        // door, and the Data rebinds; the navigators below see the
        // dict/list/table, not the reference. A parse failure surfaces HERE as
        // the actionable error (the developer navigating malformed JSON must
        // see the real cause, not a generic NotFound) — one of the two seams
        // that consume the door's failure; everywhere else it propagates loud.
        global::app.type.item.@this val;
        try
        {
            val = await Value();
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            var real = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            var entity = _type.Mint();
            Error = new global::app.error.Error(
                $"failed to read %{Name}% as {entity.Kind ?? entity.Name}: {real.Message}",
                "MaterializeFailed", 400) { Exception = real };
            return FromError(Error);
        }

        // A nested Data riding the rung-2 carrier (the SetValueDirect courier
        // debt) — navigate into the inner Data, it's the real object.
        if (val is global::app.type.item.clr { Value: @this dataVal })
        {
            var dataChild = await dataVal.GetChildValue(key);
            if (dataChild.IsInitialized) return dataChild;
        }

        // Check Data subclass properties (e.g., DynamicData)
        // These are declared on the subclass, not on Data itself.
        var ownProp = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (ownProp != null && ownProp.DeclaringType != typeof(@this))
            return new @this(key, ownProp.GetValue(this), parent: this);


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
                var navResult = await navigator.Navigate(this, key);
                if (navResult.IsInitialized) return navResult;
            }

            // Fallback when no app context (e.g., during deserialization)
            var fallbackResult = await global::app.variable.navigator.ValueNavigators.Navigate(this, key);
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

        // Access-driven resolution: navigating by key into text is NOT a
        // guess — no content sniffing. Text can't be walked by key, so the
        // developer gets a clear, actionable error pointing at the fix
        // (`as object/json`) rather than a silent null. A structured value
        // (object/json) materialized above, so it never reaches here.
        if (val is global::app.type.text.@this)
            return TypeUnknownError(key);

        return NotFound(key);
    }

    /// <summary>
    /// The type-unknown navigation error — the contract surface for "you tried
    /// to navigate a value that has no type; tell PLang how to read it." The
    /// wording is the LLM teaching surface; <c>add `as &lt;type&gt;`</c> names
    /// the fix.
    /// </summary>
    private @this TypeUnknownError(string key)
    {
        var nameHint = string.IsNullOrEmpty(Name) ? "value" : $"%{Name}%";
        var isText = _type is global::app.type.text.@this;
        var what = isText ? "is text" : "has no type";
        var err = FromError(new global::app.error.Error(
            $"cannot navigate .{key}: {nameHint} {what}; add `as <type>` (e.g. `as object/json`) to navigate it",
            "TypeUnknown", 400));
        err.Name = key;
        return err;
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

        // Chain-wide facet: `%x!file%` reaches the reference facet whether or not
        // the value narrowed. The instance's own chain answers — pre-narrow the
        // value IS the facet; post-narrow the prior chain holds the
        // location-only reference (the parse stamped it).
        if (_type?.Facet(key) is { } facetValue)
            return new @this(key, facetValue, parent: this);

        // Property plane on the value itself — `!path`/`!host`/`!size` reach the
        // value's own metadata surface without materialising content (Peek).
        // NonPublic included: the raw derivations (path.Relative/.Extension/
        // .Absolute) are internal C# but ARE the `!relative`/`!extension`/
        // `!absolute` projections on this plane.
        var peeked = Peek();
        if (peeked != null && peeked is not global::app.type.text.@this)
        {
            var vp = peeked.GetType().GetProperty(key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (vp != null)
                return new @this(key, vp.GetValue(peeked), parent: this);
        }

        return NotFound(key);
    }
}
