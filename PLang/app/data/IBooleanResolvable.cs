namespace app.data;

/// <summary>
/// A value that answers "am I truthy" for itself — every item does (<c>item.@this</c> implements it; the
/// default asks <c>IsTruthy</c>). A value whose answer needs I/O overrides it: <c>path</c>, where truthiness
/// is "does the resource exist", which for the http scheme is a request — hence the async signature, and
/// hence the condition-evaluation pipeline is async end to end.
///
/// <para>
/// <c>Data.ToBooleanAsync()</c> — the one truthiness door, and emptiness is its negation — reads what the Data
/// holds and asks it here.
/// </para>
///
/// Kept here next to <c>Data</c> (the asker) rather than on the value's own type so <c>Data</c> depends on
/// the marker, not on any concrete value type.
/// </summary>
public interface IBooleanResolvable
{
    /// <summary>Resolves this value to a boolean — may perform I/O, as the asker whose
    /// <paramref name="context"/> it is (a path's "does it exist" checks the asker's permission).</summary>
    System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context);
}
