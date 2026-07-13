namespace app.type.item.@null;

/// <summary>
/// PLang <c>null</c> value — the null <em>value</em> (a present value that is
/// null), hosted as a process-wide <b>singleton</b> (<see cref="Instance"/>) so
/// the scattered <c>is null</c> / <c>== null</c> value-switches dissolve onto a
/// type like every other scalar. There is one null in the world; it is never
/// per-value allocated.
///
/// <para><b>The null value, not the absence of a Data.</b> A <c>Data</c> whose
/// value is null carries this singleton (a present null). A <em>missing</em>
/// variable / <c>NotFound</c> / uninitialised read is a null <c>data</c>
/// <em>reference</em> (no box, <c>IsInitialized = false</c>) — a different axis
/// that stays a C# null. <c>null.@this</c> must not represent "no Data."</para>
///
/// <para>Always falsy; <c>null == null</c> true and equal to nothing else;
/// equality-only (no <see cref="global::app.data.IOrderableValue"/> — the
/// sort-last policy lives on <c>Compare</c>, not the wrapper). Bare wire form is
/// <c>null</c>.</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "null";
    public static string Shape => "null";

    /// <summary>The one typeless null value in the world.</summary>
    public static @this Instance { get; } = new();

    private @this() { }

    // A TYPED null — a declared slot that holds no value yet (`set %x% as path`
    // with no value, a tool-definition parameter the LLM will fill, a typed null
    // result). It is still null (IsNull, falsy, empty, renders/serialises null);
    // the declaration only surfaces through Mint/Type/Kind. Absorbs the former
    // `item.absent` — a value-less slot is just a null that remembers its type.
    private readonly string? _typeName;
    private readonly string? _kind;

    internal @this(string typeName, string? kind = null)
    {
        _typeName = string.IsNullOrWhiteSpace(typeName) ? null : typeName;
        _kind = kind;
    }

    /// <summary>A typeless null mints "null"; a typed null mints its declared
    /// type/kind so an empty <c>path</c> slot still answers <c>path</c>.</summary>
    protected internal override global::app.type.@this Type
        => _typeName == null
            ? base.Type
            : new global::app.type.@this(_typeName) { Kind = _kind is { } k ? new global::app.type.kind.@this(k) : null };

    /// <summary>
    /// Is this raw value "null" in PLang's sense — a C# null reference OR the born-native
    /// null singleton? A literal <c>set %x% = null</c> stores a C# null in Data.Value while a
    /// json/navigated null stores this singleton; both are the same null value. Null-ness
    /// dissolves onto the type, so callers ask here instead of writing <c>== null || is @this</c>.
    /// </summary>
    public static bool IsNullValue(object? value) => value is null or @this;

    /// <summary>The null citizen — the one value that IS null.</summary>
    public override bool IsNull => true;

    // ---- Comparison — the null citizen owns null policy (see app.data.Comparison) ----

    /// <summary>Highest rank — null always drives, so its <see cref="Order"/> answers
    /// <c>%x% == null</c> for every value: the policy lives on the citizen, not in the
    /// reconcile.</summary>
    public override int Rank => 1000;

    /// <summary>Null policy: <c>null == null → Equal</c>; <c>null vs any value → NotEqual</c>
    /// (never Incomparable — anything is equality-comparable to null). Ordering against null
    /// has no order, so <c>%x% &lt; null</c> errors at the boundary, as it should.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
        => new(other is @this ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual);

    /// <summary>Null is always falsy.</summary>
    public override bool IsTruthy() => false;

    /// <summary>The item emptiness hook — null is empty.</summary>
    public override System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(true);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Null();

    /// <summary>The raw form of null is C# null.</summary>

    /// <summary>The CLR exit door — null converts to the absent value of any target.</summary>
    internal override object? Clr(System.Type target) => null;

    /// <summary>Bare <c>null</c> — the serializer renders this.</summary>
    public override string ToString() => "null";

    /// <summary><c>null == null</c> only — a C# null reference or the singleton; nothing else.</summary>
    public bool AreEqual(object? other) => other is null || other is @this;

    public override bool Equals(object? obj) => obj is null || obj is @this;
    public override int GetHashCode() => 0;
}
