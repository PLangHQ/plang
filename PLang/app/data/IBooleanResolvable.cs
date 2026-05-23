namespace app.data;

/// <summary>
/// A value that knows how to answer "am I truthy" for itself — overriding
/// <see cref="@this.ToBoolean"/>'s blind "any non-null object is true".
///
/// <para>
/// <c>Data.ToBooleanAsync()</c> dispatches to <see cref="AsBooleanAsync"/> when
/// the wrapped value implements this interface. The canonical implementer is
/// <c>path</c>: a path's truthiness is "does the resource exist", which for the
/// http scheme requires async I/O — hence the async signature, and hence the
/// condition-evaluation pipeline is async end to end.
/// </para>
///
/// Kept here next to <c>Data</c> (the dispatcher) rather than on the value's own
/// type so <c>Data</c> depends on the marker, not on any concrete value type.
/// </summary>
public interface IBooleanResolvable
{
    /// <summary>Resolves this value to a boolean — may perform I/O.</summary>
    System.Threading.Tasks.Task<bool> AsBooleanAsync();
}
