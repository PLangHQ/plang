namespace app.data;

/// <summary>
/// A value that knows how to order itself against another — owning its own
/// natural order instead of letting <c>Compare</c> re-derive it from a
/// <c>Family()</c> switch + an <c>Orderable</c> set. A value type implements this
/// only when a total order consistent with its equality exists; an equality-only
/// type (<c>dict</c>) does not, so <c>Compare.Order</c> throws
/// <see cref="Compare.NotOrderableException"/> for it.
///
/// <para><c>list</c> implements this as lexicographic order — item by item, the
/// first differing pair decides, a prefix sorts first — recursing back through
/// <c>Compare.Order</c> (the recursion contract). Scalars don't implement it; the
/// shared scalar comparer handles their order.</para>
///
/// Kept next to <c>Data</c> — mirrors <see cref="IBooleanResolvable"/> /
/// <see cref="IEquatableValue"/>.
/// </summary>
public interface IOrderableValue
{
    /// <summary>
    /// Negative / zero / positive as this value sorts before / equal to / after
    /// <paramref name="other"/>. Throws <see cref="Compare.NotOrderableException"/>
    /// when <paramref name="other"/> isn't an orderable peer of this value.
    /// </summary>
    int Order(object? other);
}
