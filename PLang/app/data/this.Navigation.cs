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
    /// <summary>
    /// Gets a child value by path (dot notation, index, or method call).
    /// Never returns null — returns Data.NotFound(key) when the path doesn't resolve.
    /// </summary>
    public virtual async System.Threading.Tasks.ValueTask<@this> Get(string path)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        // The path owns its tokenization (app.variable.path) — parse once into
        // typed segments, then walk. No free-function re-tokenizer per hop.
        return await Get(global::app.variable.path.@this.Parse(path));
    }

    /// <summary>
    /// Walks a parsed navigation <see cref="global::app.variable.path.@this">path</see>:
    /// navigate this value by the head segment, recurse on the tail. Each segment kind
    /// owns what its key IS — a member name, a resolved bracket index, an infrastructure
    /// plane hop, or a method call.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Get(global::app.variable.path.@this path)
    {
        if (path.IsEmpty) return this;

        // A clr owns its whole path language — hand it the tail in ONE call (the kind walks
        // path.Segments; a future jsonpath/css kind reads its own form off the path). Only for a
        // pure value-plane path — an infra (`!file`) or method (`grep(...)`) segment stays on the
        // generic per-hop walk below (which owns those planes + the rich IndexNotSet diagnostic).
        if (_item is global::app.type.clr.@this clr
            && System.Linq.Enumerable.All(path.Segments,
                   s => s is global::app.variable.path.Segment.Member or global::app.variable.path.Segment.Index))
            return await clr.Get(this, path);

        var (head, tail) = path.Split();

        // Infrastructure plane (`!file`, `!data`) reads the binding, not the value;
        // method calls (`grep(...)`) invoke on the value. Both skip the value-plane
        // context injection below (mirrors the pre-redesign returns).
        @this child;
        bool valuePlane = false;
        switch (head)
        {
            case global::app.variable.path.Segment.Infra infra:
                child = await GetInfrastructureValue(infra.Name);
                break;
            case global::app.variable.path.Segment.Call call:
                child = InvokeMethod(call.Method, call.Args);
                break;
            case global::app.variable.path.Segment.Index index:
                child = await _item.Get(this, await index.Key(_context?.Variable));
                valuePlane = true;
                // A non-literal index (`[planStep.index]`) that the container couldn't use:
                // distinguish the common, confusing cause — the index variable itself is unset
                // (resolved to null, so ResolveKey fell back to the literal key) — from "the
                // container has no such key". Names the unset index, not the whole path.
                if (!child.IsInitialized && !index.IsLiteral && _context?.Variable != null
                    && (await _context.Variable.Get(index.Inner.ToString())).Peek()
                        is null or global::app.type.item.@null.@this)
                {
                    // Report what the index expression's ROOT variable actually holds — the
                    // common confusion is the root being unset (injection never ran) vs the
                    // root holding a raw "%x%" literal (a param born as a string, not a
                    // variable reference) vs the root being a value whose member is missing.
                    var inner = index.Inner.ToString();
                    var rootCut = inner.IndexOfAny(new[] { '.', '[' });
                    var rootName = rootCut < 0 ? inner : inner[..rootCut];
                    var rootData = await _context.Variable.Get(rootName);
                    var rootPeek = rootData.Peek();
                    var rootDiag = rootData.IsInitialized
                        ? $"%{rootName}% holds {rootData.Type?.Name ?? "?"} = {(rootPeek is null ? "null" : $"'{rootPeek.ToString()?.Split('\n')[0]}'")}"
                        : $"%{rootName}% is unset";
                    return FromError(new global::app.error.Error(
                        $"cannot navigate [{index.Inner}]: the index %{index.Inner}% is not set (resolved to null) — {rootDiag}",
                        "IndexNotSet", 400));
                }
                break;
            default: // Member (plain or quoted) — the VALUE owns navigation by key
                child = await _item.Get(this, ((global::app.variable.path.Segment.Member)head!).Name);
                valuePlane = true;
                break;
        }

        if (!child.IsInitialized) return child;

        if (valuePlane)
        {
            // Inject context on IContext values during traversal. A reference
            // (file/url) injects via Peek — opening the door here would read its
            // content on a property-plane hop (`%x!file!path%` must stay read-free).
            var injectTarget = child.Peek() is (global::app.type.file.@this or global::app.type.url.@this) and { } reference
                ? reference
                : await child.Value();
            if (injectTarget is app.module.IContext contextual)
                contextual.Context = _context;
        }

        return tail.IsEmpty ? child : await child.Get(tail);
    }


    /// <summary>
    /// Write-at-path — the READ walk (<see cref="Get(global::app.variable.path.@this)"/>) for all
    /// but the last segment, then ONE <c>Set</c> at the leaf. The value owns the write exactly as
    /// it owns the read: navigate to the parent, hand its item the leaf key + the grammar's
    /// index-vs-member fact, rebind the parent when the item comes back replaced (a json host
    /// materialises into a dict; a clr host mutates in place, so identity holds).
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Set(global::app.variable.path.@this path, object? value)
    {
        if (path.IsEmpty) return this;                     // no leaf to write — nothing to do

        var parent = await Get(path.Parent);
        if (!parent.IsInitialized) return parent;          // parent absent → surface it, don't invent

        // Materialise a source-backed parent (a `%cfg%` still raw json) so the write lands on the
        // parsed value, not the raw form — the door parses + rebinds, idempotent for a live value.
        _ = await parent.Value();
        if (parent.Error?.Key == "MaterializeFailed") return _context?.Error(parent.Error) ?? parent;

        // Resolve the leaf key here (the walk owns key resolution, mirroring the read side) — a
        // bracket index resolves against the store; a member is its own name.
        var leaf = path.Last;
        string key;
        bool isIndex = leaf is global::app.variable.path.Segment.Index;
        if (leaf is global::app.variable.path.Segment.Index idx)
            key = await idx.Key(_context?.Variable);
        else
            key = ((global::app.variable.path.Segment.Member)leaf).Name;

        if (parent.Peek() is not global::app.type.item.@this target)
            return _context?.NotFound(key) ?? parent;

        var written = await target.Set(key, isIndex, value);
        if (!ReferenceEquals(written, target)) parent.SetValue(written);
        return parent;
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
    /// Accesses Data's own infrastructure properties (Name, Type, Error, Success, Properties, etc.).
    /// Used when navigating with ! prefix: %user!Name%, %result!Error%, etc.
    /// Full hierarchy reflection — reaches any property on the Data class itself.
    /// </summary>
    private async System.Threading.Tasks.ValueTask<@this> GetInfrastructureValue(string key)
    {
        // Stage 4: Properties win first — `%x!cost%` reads Properties["cost"].
        // Reflection-discovered Data infrastructure (Name, Type, Error, Success,
        // subclass properties like Llm) stays available via the same operator
        // when the key isn't a Property — keeps `%result!Llm%` working while
        // the Properties scope becomes the primary `!` namespace.
        if (Properties.ContainsKey(key))
            return new @this(key, await Properties.Value(key), parent: this);

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
        if (_item?.Facet(key) is { } facetValue)
            return new @this(key, facetValue, parent: this);

        // Property plane on the value itself — `!path`/`!host`/`!size`/`!length`
        // reach the value's own typed metadata surface without materialising
        // content (Peek). Properties were checked first above, so a user-set
        // Property still wins over a reflected member of the same name. NonPublic
        // included: the raw derivations (path.Relative/.Extension/.Absolute) are
        // internal C# but ARE the `!relative`/`!extension`/`!absolute` projections.
        var peeked = Peek();
        if (peeked != null)
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
