namespace app.type.text;

/// <summary>
/// PLang <c>text</c> type — the canonical name for textual content (replaces
/// the historical primitive <c>string</c> on the PLang surface; <c>string</c>
/// stays as an accepted alias). Mirrors <c>image</c> but text-backed instead
/// of bytes-backed.
///
/// <para>Kind is open (no advertised vocabulary): a <c>text</c> value's kind
/// comes from the file extension (<c>md</c>, <c>txt</c>, <c>csv</c>, …) via
/// the <see cref="Build"/> hook. A plain string with no extension is
/// <c>text</c> with kind <c>null</c>.</para>
///
/// <para>The wire shape is just a string — <see cref="Shape"/> says <c>string</c>.
/// The backing CLR <c>string</c> is the single source of truth; behavior
/// (length, case, contains, compare, truthiness) is a member on the wrapper, so
/// the <c>is string</c> consumer-switches collapse into method calls.</para>
///
/// <para><b>Case policy.</b> Order and value-equality are <em>ordinal,
/// case-insensitive</em> — matching the historical <c>ScalarComparer</c> string
/// behavior so <c>"abc" == "ABC"</c> and sort order are unchanged after text
/// flows native. <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>
/// align with that policy so a <c>text</c> works as a <c>HashSet</c> member /
/// dict key without surprise.</para>
///
/// <para><b>Atomicity.</b> <c>text</c> is a scalar, not a sequence of chars —
/// it deliberately does <b>not</b> implement <c>IEnumerable</c>, so
/// <c>foreach %s%</c> never char-iterates it.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "readme.md";
    public static string Shape => "string";
    /// <summary>
    /// LLM-facing teaching: text's kind comes from the file extension
    /// (`md`, `txt`, `csv`, `html`, …). The kind is a hint by default; strict
    /// is a no-op for text since plain vs markdown can't be probed from content.
    /// </summary>
    public static string Description =>
        "Textual content. Kind is set from the file extension (md, txt, csv, html, ...). "
        + "Kind is a hint by default; strict is a no-op for text (plain vs markdown is "
        + "not detectable from content).";
    // No static Kinds — text's kind is open (derived from extension at build).

    // THE backing — a private field, not a property at any visibility.
    // Content leaves text only via Write(IWriter), the typed ops, or the
    // door; a .NET edge lowers through Clr.
    private readonly string _value;

    /// <summary>The value's kind — the file-extension vocabulary (md, csv, …).
    /// An ordinary typed property stamped at creation, never after.</summary>
    public string? Kind { get; init; }

    protected internal override global::app.type.@this Mint()
        => new("text", typeof(string)) { Kind = Kind };

    /// <summary>A stamped template's answer depends on outside state (%refs%
    /// can change between uses) — never kept. Plain text caches as always.</summary>
    public override bool Cacheable => Template == null;

    /// <summary>
    /// THE door — ready means ready: a stamped template fills its holes
    /// against live variables at every use (never kept — see
    /// <see cref="Cacheable"/>). Full-match <c>%x%</c> answers with the
    /// variable's value through ITS own door (door recursion; the answer may
    /// be any type); partial (<c>"hello %name%"</c>) interpolates single-pass
    /// into fresh, unstamped text. An unset full-match ref is THIS type's own
    /// failure story — reported on the asking binding, answer absent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this asking)
    {
        if (Template == null) return this;
        var context = asking.Context;
        if (context?.Variable == null) return this;
        if (global::app.data.@this.TryFullVarMatch(_value, out var varName))
        {
            var resolved = await context.Variable.Get(varName);
            if (resolved == null || !resolved.IsInitialized)
            {
                asking.Fail(new global::app.error.Error(
                    $"%{varName}% is not set — nothing to answer for {_value}.",
                    "VariableNotFound", 404));
                return Absent;
            }
            return await resolved.Value();
        }
        var interpolated = await context.Variable.Resolve(_value);
        return new @this(interpolated);
    }

    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(_value);

    public @this(string value) { _value = value ?? string.Empty; }

    // INBOUND only — the entry lift (`.Ok("x")` constructs). The outbound
    // implicit (text → string) is gone: every site was a silent CLR exit;
    // a reader names the string face (`.Value`) at a real .NET edge.
    public static implicit operator @this(string s) => new(s);

    // Only the @this==@this overload — NOT a string overload. string is a reference
    // type, so a string overload would make `text == null` ambiguous (null fits both).
    // `text == "literal"` is written via the typed ops (Contains/AreEqual) or ToString at a display edge.
    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);

    public override string ToString() => _value;

    /// <summary>The CLR exit door — text hands its own backing string; the
    /// shared converter (strict, loud on junk) carries it to the target.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(_value, target);

    // ---- Ops (the behavioral targets of the `is string` sweep) ----

    /// <summary>Codepoint (Unicode scalar) count — surrogate pairs count once.
    /// Returns the PLang <c>number</c> (the public surface answers in PLang values).</summary>
    public global::app.type.number.@this Length
    {
        get
        {
            int count = 0;
            foreach (var _ in _value.EnumerateRunes()) count++;
            return count;
        }
    }

    public @this Upper() => new(_value.ToUpperInvariant());
    public @this Lower() => new(_value.ToLowerInvariant());
    public @this Trim() => new(_value.Trim());

    /// <summary>The item membership hook — substring, same policy as below.</summary>
    public override System.Threading.Tasks.ValueTask<bool> Contains(global::app.data.@this needle)
        => System.Threading.Tasks.ValueTask.FromResult(Contains(needle.ToString()));

    /// <summary>The item emptiness hook — whitespace-only text is empty.</summary>
    public override System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(string.IsNullOrWhiteSpace(_value));

    /// <summary>True when this is a stamped template whose WHOLE text is one
    /// live <c>%ref%</c> — the binding layer's classifier (full-match hops to
    /// the live variable; partial renders). The ref's bare name comes out.</summary>
    internal bool IsRef(out string refName)
    {
        refName = "";
        return Template != null && global::app.data.@this.TryFullVarMatch(_value, out refName);
    }

    /// <summary>True when the text contains <c>%ref%</c> holes — the authored
    /// seam's detection (deterministic code, never the LLM).</summary>
    internal bool HasHoles => System.Text.RegularExpressions.Regex.IsMatch(_value, "%[^%]+%");

    /// <summary>The authored form of this text — itself when already stamped
    /// or hole-free; a stamped copy otherwise (the template seam).</summary>
    internal @this Authored()
        => Template != null || !HasHoles ? this : new @this(_value) { Kind = Kind, Template = "plang" };

    /// <summary>A re-kinded copy — same content, the declared kind stamped
    /// (the entry-judgement fold's text arm; values immutable, never restamped
    /// in place).</summary>
    internal @this Kinded(string? kind) => new(_value) { Kind = kind, Template = Template };

    public bool Contains(string other) =>
        _value.Contains(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public bool StartsWith(string other) =>
        _value.StartsWith(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public bool EndsWith(string other) =>
        _value.EndsWith(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public int IndexOf(string other) =>
        _value.IndexOf(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);

    public @this Substring(int start, int length) => new(_value.Substring(start, length));
    public @this Replace(string oldValue, string newValue) =>
        new(_value.Replace(oldValue ?? string.Empty, newValue ?? string.Empty));

    /// <summary>Split into a native <c>list</c> of <c>text</c> values.</summary>
    public global::app.type.list.@this Split(string separator)
    {
        var list = new global::app.type.list.@this();
        var parts = string.IsNullOrEmpty(separator)
            ? new[] { _value }
            : _value.Split(separator);
        foreach (var part in parts)
            list.Add(new global::app.data.@this("", new @this(part)));
        return list;
    }

    // ---- Truthiness (item) ----

    /// <summary>Empty text is falsy; any non-empty text is truthy.</summary>
    public override bool IsTruthy() => _value.Length > 0;

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Specificity floor — every other ranked type outranks text and drives.</summary>
    internal static int CompareRank => 10;

    /// <summary>
    /// Ordinal, case-insensitive ordering in caller order. Text is the floor type,
    /// so it only drives a pair the other side couldn't claim; coercion is the
    /// plain string form of the leaf.
    /// </summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        // Text coerces the other side into its kind: the wrapper's content, a raw
        // string, an enum's NAME (`where Status equals 'Timeout'`), or a domain
        // value's canonical text form (its ToString — e.g. an Ask renders its
        // answer for exactly this comparison, a type entity its name). Containers
        // (dict/list) have no honest text form — they stay non-coercible, so
        // `%dict% == "text"` is Incomparable, not a serialization comparison.
        static string? Coerce(object? v) => v switch
        {
            @this t => t._value,
            string s => s,
            System.Enum e => e.ToString(),
            global::app.type.dict.@this or global::app.type.list.@this => null,
            System.Collections.IDictionary or System.Collections.IList => null,
            null => null,
            _ => v.ToString(),
        };
        var sa = Coerce(a);
        var sb = Coerce(b);
        if (sa == null || sb == null) return global::app.data.Comparison.Incomparable;
        var c = string.Compare(sa, sb, System.StringComparison.OrdinalIgnoreCase);
        return c < 0 ? global::app.data.Comparison.Less
             : c > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    // ---- Equality + order (ordinal, case-insensitive — see class doc) ----

    public bool AreEqual(object? other) => other switch
    {
        @this t => string.Equals(_value, t._value, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(_value, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public bool Equals(@this? other) =>
        other is not null && string.Equals(_value, other._value, System.StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(_value);
}
