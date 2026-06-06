namespace app.type.@null;

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
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue
{
    public static string Example => "null";
    public static string Shape => "null";

    /// <summary>The one null value in the world.</summary>
    public static @this Instance { get; } = new();

    private @this() { }

    /// <summary>Null is always falsy.</summary>
    public override bool IsTruthy() => false;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Null();

    /// <summary>The raw form of null is C# null.</summary>
    public override object? ToRaw() => null;

    /// <summary>Bare <c>null</c> — the serializer renders this.</summary>
    public override string ToString() => "null";

    /// <summary><c>null == null</c> only — a C# null reference or the singleton; nothing else.</summary>
    public bool AreEqual(object? other) => other is null || other is @this;

    public override bool Equals(object? obj) => obj is null || obj is @this;
    public override int GetHashCode() => 0;
}
