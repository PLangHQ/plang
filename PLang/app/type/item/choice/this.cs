namespace app.type.item.choice;

/// <summary>
/// Non-generic view of a <see cref="@this{T}"/> — lets the serializer and Normalize
/// recognize any choice without knowing the closed option type. The bare wire form
/// is the chosen option's symbol.
/// </summary>
public interface IChoice
{
    /// <summary>The chosen option's symbol — the bare wire form (an enum member's name,
    /// a named-set registry key like "==").</summary>
    string Symbol { get; }
}

/// <summary>
/// PLang <c>choice</c> value — a value picked from a fixed set of named options
/// (the layperson's "enum"). Generic over ANY named-set type, not just CLR enums:
/// <list type="bullet">
///   <item>a CLR <c>enum</c> — options are its member names (GetNames / Parse);</item>
///   <item>a class that declares a closed set — a static <c>Choices(context?)</c>
///   method (the <c>[Choices]</c> vocabulary) plus a <c>ctor(string)</c> that
///   resolves a name (e.g. <c>condition.Operator</c>, which keeps its per-option
///   behavior on the instance).</item>
/// </list>
/// The handler keeps the typed value (<c>HttpMethod m = action.Method?.Clr<string>()</c> via
/// the implicit operator) while the language sees a validated choice. The name is the
/// value's <see cref="object.ToString"/> — uniform across enums and named-set classes.
/// </summary>
[global::app.Attributes.PlangType("choice")]
public sealed class @this<T> : global::app.type.item.@this, global::app.type.item.ICreate<@this<T>>,
    IChoice
    where T : notnull
{
    public T Value { get; }

    /// <summary>The option <paramref name="value"/>, born holding <paramref name="type"/> — the app's
    /// <c>{choice, &lt;set&gt;}</c>, handed in by its maker.</summary>
    public @this(T value, global::app.type.@this type) : base(type) { Value = value; }

    // The T → choice operator's birth: it has no app to ask, so it holds {choice, <set>} built from the set's name
    // (its T's own [PlangType]; a T with no name is a bare choice type) — until the implicit-operator branch.
    private @this(T value)
        : base(new global::app.type.@this(typeof(@this<T>),
            (System.Attribute.GetCustomAttribute(typeof(T), typeof(global::app.Attributes.PlangTypeAttribute), inherit: false)
                as global::app.Attributes.PlangTypeAttribute)?.Name)) { Value = value; }

    public static implicit operator T(@this<T> c) => c.Value;
    // T → choice (so `.Ok(HttpMethod.GET)` and a `[Default(HttpMethod.GET)]` cast construct).
    public static implicit operator @this<T>(T value) => new(value);

    /// <summary>The CLR exit door — choice hands its enum backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    /// <summary>A choice's raw face is its name, as it writes itself — a template compares and prints
    /// `Promote`, never the enum's number. Typed CLR asks still get the enum (<see cref="Clr"/>).</summary>
    internal override object? Backing => ToString();
    public override string ToString() => Value.ToString() ?? "";
    public override bool IsTruthy() => true;
    public override bool IsLeaf => true;
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter w) { w.String(ToString()); return default; }

    string IChoice.Symbol => ToString();


    /// <summary>The one symbol→choice resolution home — the wire form is the option's SYMBOL
    /// (an enum member's name, a named-set registry key like "=="). Shared by the ICreate core
    /// and the wire reader. THROWS FormatException on an unknown symbol (no data.Fail in scope;
    /// the born path turns it into MaterializeFailed named to the binding).</summary>
    /// <summary>Reads one option off the wire. The written form is the option's SYMBOL, but a
    /// NUMBER also appears on disk: every .pr built before this field became a choice carries the
    /// enum's ordinal, and 790 of them still do. <see cref="Parse"/> accepts an ordinal in its
    /// text form, so both land in one place — the type that owns the wire form.</summary>
    public static @this<T> Read<TReader>(ref TReader reader, global::app.type.@this type)
        where TReader : global::app.type.format.IReader, allows ref struct
        => Parse(reader.Peek() == global::app.type.format.TokenKind.Number
            ? reader.Number().ToString() ?? ""
            : reader.String(), type);

    /// <summary>The option <paramref name="symbol"/> names, born holding <paramref name="type"/>. THROWS
    /// FormatException naming the options when it names none.</summary>
    public static @this<T> Parse(string symbol, global::app.type.@this type)
    {
        // the set — the type's kind, the app's {choice, <set>} — answers the member its symbol names: an enum's, a
        // family's kind, a named set's
        var set = type.kind as set.@this
            ?? throw new System.InvalidOperationException($"choice<{typeof(T).Name}> is parsed against {type.Name}, which is no closed set.");
        try { return new((T)set.Member(symbol), type); }
        catch (System.ArgumentException ex)
        {
            throw new System.FormatException(
                $"'{symbol}' is not a {set.Name} option. Valid: {string.Join(", ", set.Values ?? [])}", ex);
        }
    }

    /// <summary>THE PURE CORE — pass-through; a raw member wraps; a string face parses
    /// (malformed throws per the error policy), each born holding <paramref name="type"/>; anything else declines.</summary>
    public static @this<T>? Create(object? raw, global::app.type.@this type)
    {
        if (raw is @this<T> self) return self;
        if (raw is T member) return new(member, type);
        var symbol = raw as string ?? (raw as global::app.type.item.@this)?.RawText;
        return symbol is null ? null : Parse(symbol, type);
    }

    /// <summary>The courier — converts Parse's throw to data.Fail with the option list;
    /// declines fail typed. A value is born holding the declared type, else the app's for its set.</summary>
    public static @this<T>? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this<T> self) return self;
        type ??= data.Context.App.type.list[typeof(@this<T>)];
        try { if (Create(raw, type) is { } choice) return choice; }
        catch (System.FormatException ex)
        {
            data.Fail(new global::app.error.@this(ex.Message, "ChoiceInvalid", 400)
                { FixSuggestion = $"Valid options: {string.Join(", ", type.Values ?? [])}" });
            return null;
        }
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name} — choice<{typeof(T).Name}> cannot be created from it.",
            "CreateItemDeclined", 400));
        return null;
    }

    // ---- Comparison (the unified hook — see app.type.compare; statics on the
    // generic exist per closed type, so discovery via the catalog's closed
    // choice<T> finds them) ----

    /// <summary>Outranks text — the name string coerces into the choice.</summary>
    public override int Rank => 150;

    /// <summary>Equality-only: <c>Equal</c>/<c>NotEqual</c> by value or by name
    /// (choice/text), never an order. This choice drives; the other coerces via
    /// <see cref="AreEqual"/> (a name string / choice matches its member).</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
        => new(AreEqual(other) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual);

    // Equality by value, and by name against a choice/text/string (so
    // `where method equals 'GET'` reconciles).
    public bool AreEqual(object? other) => other switch
    {
        @this<T> c => Value.Equals(c.Value),
        T v => Value.Equals(v),
        IChoice ic => string.Equals(ToString(), ic.Symbol, System.StringComparison.OrdinalIgnoreCase),
        global::app.type.item.text.@this t => t.AreEqual(ToString()),
        string s => string.Equals(ToString(), s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override bool Equals(object? obj) => AreEqual(obj);
    public override int GetHashCode() => Value.GetHashCode();
}
