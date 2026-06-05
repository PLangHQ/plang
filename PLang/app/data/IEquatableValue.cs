namespace app.data;

/// <summary>
/// A value that knows how to answer "am I equal to that value" for itself —
/// owning its own equality instead of letting <c>Compare</c> re-derive it with a
/// type-switch. The canonical implementers are the collections (<c>dict</c>,
/// <c>list</c>), whose equality is structural.
///
/// <para><c>Compare.AreEqualValues</c> dispatches here when the left value
/// implements the interface; the implementer compares each child by calling
/// <em>back</em> into <c>Compare.AreEqualValues</c> (the recursion contract) so a
/// nested number still widens and nested text still compares case-insensitive —
/// the collection owns how to walk itself, the leaves stay on the one path.</para>
///
/// Kept here next to <c>Data</c> (the dispatcher) rather than on the value's own
/// type so <c>Data</c> depends on the marker, not on any concrete value type —
/// mirrors <see cref="IBooleanResolvable"/>.
/// </summary>
public interface IEquatableValue
{
    /// <summary>True when <paramref name="other"/>'s value equals this value (structurally for collections).</summary>
    bool AreEqual(object? other);
}
