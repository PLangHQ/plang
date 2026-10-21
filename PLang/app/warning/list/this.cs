namespace app.warning.list;

/// <summary>
/// A program node's build diagnostics — a plang list of <see cref="app.warning.@this"/> values
/// (<c>list&lt;warning&gt;</c>), exposed behind the singular <c>.Warning</c> property on goal/step. PROGRAM STRUCTURE:
/// born context-free (the graph is shared across runs), it stores no context.
/// </summary>
public sealed class @this : global::app.type.item.list.bare.@this<global::app.warning.@this>
{
    /// <summary>An empty warning list, born holding its type from <paramref name="types"/>, the app's type list.</summary>
    public @this(global::app.type.list.@this types) : base(new System.Collections.Generic.List<object?>(), types[typeof(@this)]) { }
}
