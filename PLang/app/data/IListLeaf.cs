namespace app.data;

/// <summary>
/// A value that DISSOLVES into its containing list — its leaves become the list's
/// leaves (the transparency rule of the row/chunk model). When such a value is an
/// element ("row") of a list, the list counts and walks <em>its</em> leaves instead
/// of treating it as one item.
///
/// <para>The only implementer is <c>list</c>. A <c>dict</c>, <c>table</c>, or scalar
/// element is one whole item (weight 1) even though it may own its own <c>.count</c>
/// — that count answers "how many do I hold", a different question from "do I
/// dissolve into my parent". So <c>[10,20,30] + add [40,50]</c> flattens, but
/// <c>[table1, table2]</c> stays two tables.</para>
///
/// Kept next to <c>Data</c> — mirrors <see cref="IBooleanResolvable"/> /
/// <see cref="IEquatableValue"/>.
/// </summary>
public interface IListLeaf
{
    /// <summary>How many leaves this value contributes to its container list (its flattened count).</summary>
    int LeafCount { get; }

    /// <summary>This value's leaves, in order — the items the container list yields in its place.</summary>
    IReadOnlyList<@this> Leaves { get; }
}
