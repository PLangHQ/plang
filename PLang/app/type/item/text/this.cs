namespace app.type.item.text;

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

    protected internal override global::app.type.@this Type
        => new("text", typeof(string)) { Kind = Kind is { } k ? new global::app.type.kind.@this(k) : null, Template = Template };

    /// <summary>
    /// THE PURE CORE — "text, make yourself from this value, or decline." A <c>text</c> passes
    /// through; a structured item (dict/list/json DOM) renders its canonical JSON TEXT (that is what
    /// <c>text/json</c> means); a scalar stringifies invariantly. An opaque domain object has no
    /// honest textual form → <c>null</c> (decline). Shared by the ICreate courier and comparison.
    /// </summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        // Native dict/list ITEMS aren't IDictionary/IEnumerable, but they render their
        // canonical {}/[] textual form (text/json means json TEXT) — checked on the item.
        if (raw is global::app.type.item.dict.@this or global::app.type.item.list.@this)
            return (@this)System.Text.Json.JsonSerializer.Serialize(raw);
        // An item of another type unwraps to its clr (a read); a raw CLR value is already its clr.
        object? clr = raw is global::app.type.item.@this it ? it.Clr<object>() : raw;
        return clr switch
        {
            string s => (@this)s,
            System.Text.Json.JsonElement or System.Text.Json.Nodes.JsonNode
                or System.Collections.IDictionary => (@this)System.Text.Json.JsonSerializer.Serialize(clr),
            System.Collections.IEnumerable and not byte[] => (@this)System.Text.Json.JsonSerializer.Serialize(clr),
            System.IConvertible c => (@this)(System.Convert.ToString(c, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; a value with no textual form
    /// declines with the reason on <paramref name="data"/>. text's kind is a hint (extension), not a
    /// construction switch, so the core needs no kind.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail(new global::app.error.Error(
            $"Cannot bind a {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to text — it has no textual form.", "TypeConversionFailed", 400));
        return null;
    }

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
    /// failure story — reported on the data binding, answer absent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (Template == null) return this;
        var context = data.Context;
        if (context?.Variable == null) return this;
        if (global::app.data.@this.TryFullVarMatch(_value, out var varName))
        {
            var resolved = await context.Variable.Get(varName);
            if (resolved == null || !resolved.IsInitialized)
            {
                data.Fail(new global::app.error.Error(
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

    /// <summary>A stamped template whose WHOLE content is one <c>%ref%</c> IS a reference
    /// (it resolves to the named binding, not renders). A partial template
    /// (<c>"hello %name%"</c>) is content — it renders, so it is NOT a variable.</summary>
    public override bool IsVariable
        => Template != null && global::app.data.@this.TryFullVarMatch(_value, out _);

    /// <inheritdoc/>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => await ctx.Variable.Get(_value);   // _value is the raw "%name%"; the store strips the %

    public override void Write(global::app.channel.serializer.IWriter w) => w.String(_value);

    /// <summary>
    /// Text has no by-key structure — navigating it (<c>%x.port%</c>) is an authoring
    /// error. A real input is typed by its mimetype at the boundary (object/json,
    /// table/csv), so a value reaching navigation is already structured; a bare text
    /// here means the author navigated a string. Method calls (<c>%x.grep("..")%</c>)
    /// go through InvokeMethod, not here, so they are unaffected.
    /// </summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
    {
        var who = string.IsNullOrEmpty(parent.Name) ? "value" : $"%{parent.Name}%";
        var err = parent.Context.Error(new global::app.error.Error(
            $"cannot navigate .{key}: {who} is text", "CantNavigateText", 400));
        err.Name = key;
        return System.Threading.Tasks.ValueTask.FromResult(err);
    }

    public @this(string value) { _value = value ?? string.Empty; }

    /// <summary>
    /// Construction with a template mode. <paramref name="template"/> is the
    /// authored-content mode the reader carries — <c>"plang"</c> when the bytes are
    /// developer-authored (a goal/<c>.pr</c>), null for runtime-ingest. Text decides
    /// for itself whether there is actually a template to stamp: only a value with a
    /// <c>%var%</c> (<see cref="HasVariable"/>) keeps the mode, so a plain string
    /// never reports as a variable reference. Resolution stays lazy — nothing renders
    /// until the door (<see cref="Value"/>). The trust is the mode, not the content:
    /// a structural text (a dict key, a type name) or a runtime-ingest value is born
    /// with a null mode and prints literally.
    /// </summary>
    public @this(string value, string? template)
    {
        _value = value ?? string.Empty;
        // Container inner slots (list/dict entries) have no per-slot .pr flag; the authored
        // read mode + a %var% in the content marks them. This gate stays until the builder
        // stamps per-slot inside containers (Documentation/v0.2/todos.md 2026-07-01) —
        // flagging literal slots changes their canonicalization/signing. Top-level params
        // are flagged by the builder (a holey value passes this gate anyway).
        if (template != null && HasVariable(_value)) Template = template;
    }

    /// <summary>
    /// Construction from a raw form — a string is the value as-is; binary bytes
    /// off I/O decode as UTF-8. Text is only ever born from a string, so this is
    /// the one place bytes become that string (a reader handed raw stream bytes
    /// reaches the text here). <paramref name="template"/> as above.
    /// </summary>
    public @this(object raw, string? template = null)
        : this(raw switch
        {
            byte[] b => System.Text.Encoding.UTF8.GetString(b),
            string s => s,
            _ => raw?.ToString() ?? string.Empty,
        }, template)
    { }

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
    public global::app.type.item.number.@this Length
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

    private static readonly System.Text.RegularExpressions.Regex RefRx =
        new("%[^%]+%", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>THE detector: true when <paramref name="s"/> contains a <c>%var%</c>
    /// reference — the authored-template seam (deterministic code, never the LLM). One
    /// home; every %var% check routes here.</summary>
    internal static bool HasVariable(string s) => RefRx.IsMatch(s);

    /// <summary>A re-kinded copy — same content, the declared kind stamped
    /// (values immutable, never restamped in place).</summary>
    public override global::app.type.item.@this Kinded(string? kind) => new @this(_value) { Kind = kind, Template = Template };

    /// <summary>text's raw string face — its characters.</summary>
    public override string? RawText => _value;

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

    // ---- Truthiness (item) ----

    /// <summary>Empty text is falsy; any non-empty text is truthy.</summary>
    public override bool IsTruthy() => _value.Length > 0;

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Specificity floor — every other ranked type outranks text and drives.</summary>
    public override int Rank => 100;

    /// <summary>
    /// Ordinal, case-insensitive ordering in caller order. Text is the floor type,
    /// so it only drives a pair the other side couldn't claim (text vs text). The other
    /// side coerces into text through the pure <c>Create</c> core — the wrapper's content,
    /// a raw string, an enum's NAME, or a domain value's canonical text form; a container
    /// has no honest text form so <c>%dict% == "text"</c> is Incomparable.
    /// </summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        var b = other as @this ?? Create(other);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = string.Compare(_value, b._value, System.StringComparison.OrdinalIgnoreCase);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
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
