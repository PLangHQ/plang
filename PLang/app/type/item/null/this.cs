namespace app.type.item.@null;

/// <summary>
/// PLang <c>null</c> value — the null <em>value</em> (a present value that is
/// null), a type like every other scalar so the scattered <c>is null</c> /
/// <c>== null</c> value-switches dissolve onto it. Each null is born holding its
/// app's null type (or the declared type it is the null of); there is no shared one.
///
/// <para><b>The null value, and the absent item.</b> A <c>Data</c> whose value is
/// null carries its app's null (a present null, <c>IsInitialized = true</c>). A
/// <em>missing</em> variable / <c>NotFound</c> / uninitialised Data
/// (<c>IsInitialized = false</c>) holds it too, so the item answers for the absence:
/// not truthy, empty, <c>== null</c>. Whether the Data was given is Data's own
/// question (<c>HasValue</c>, <c>IsInitialized</c>), never this item's.</para>
///
/// <para>Always falsy; <c>null == null</c> true and equal to nothing else;
/// equality-only: its <see cref="Order"/> answers equal to null, not equal to anything
/// else. Bare wire form is
/// <c>null</c>.</para>
/// </summary>
[global::app.Attributes.PlangType("null")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "null";

    /// <summary>A null of <paramref name="type"/> — born holding the type as declared, its class with it.</summary>
    internal @this(global::app.type.@this type) : base(type) { }

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
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
        => new(other is @this ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual);

    /// <summary>Null is never truthy — so it is empty; that is what a value that doesn't exist answers
    /// (an absent Data holds this item).</summary>
    public override bool IsTruthy() => false;

    public override bool IsLeaf => true;
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter w) { w.Null(); return default; }

    /// <summary>The raw form of null is C# null.</summary>

    /// <summary>The CLR exit door — null converts to the absent value of any target.</summary>
    internal override object? Clr(System.Type target) => null;

    /// <summary>Bare <c>null</c> — the serializer renders this.</summary>
    public override string ToString() => "null";

    /// <summary>A diagnostic shows the absence plainly, apart from the text "null".</summary>
    public override System.Threading.Tasks.ValueTask<string> Debug(global::app.actor.context.@this context) => new("(null)");

    /// <summary><c>null == null</c> only — a C# null reference or the singleton; nothing else.</summary>
    public bool AreEqual(object? other) => other is null || other is @this;

    public override bool Equals(object? obj) => obj is null || obj is @this;
    public override int GetHashCode() => 0;
}
