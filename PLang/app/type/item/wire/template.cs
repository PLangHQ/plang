namespace app.type.item.wire;

/// <summary>
/// A still-encoded slice the builder marked a template — a <c>.pr</c> row's container holding
/// <c>%variables%</c>, with the variables its row lists. Its slice names the variables, not their values. It
/// writes as written, as any wire does (its slice into its own format); using it — decoding it so its parts
/// render their variables — is the view's, as for every value that holds variables. Read and lowered as any
/// wire is.
/// </summary>
public sealed class template : @this
{
    public template(string slice, global::app.type.@this type, kind.plang.@this reader, global::app.View view,
        IReadOnlyList<global::app.type.item.variable.@this> variable, bool built = false)
        : base(slice, type, reader, view, variable, built) { }
}
