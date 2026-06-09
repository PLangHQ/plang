namespace app.variable.navigator;

/// <summary>
/// Navigates a captured snapshot. <c>%snap.variables%</c> is the variable
/// namespace (passes through to the snapshot so the next segment resolves a
/// variable by name); any other key resolves that captured variable's value.
/// Delegates to the snapshot's own <c>GetVariable</c> — the behavior lives on
/// the owner, this just routes navigation to it. The write side
/// (<c>set %snap.variables.x% = 2</c>) routes to <c>SetVariable</c> via
/// <c>Variables.SetValueOnObject</c>.
/// </summary>
public sealed class Snapshot : INavigator
{
    public bool CanNavigate(global::app.data.@this data)
        => data.Materialize() is global::app.snapshot.@this;

    public global::app.data.@this Navigate(global::app.data.@this data, string key)
    {
        if (data.Materialize() is not global::app.snapshot.@this snap)
            return global::app.data.@this.NotFound(key);

        // `.variables` — the variable namespace; the next segment names the variable.
        if (string.Equals(key, "variables", StringComparison.OrdinalIgnoreCase))
            return new global::app.data.@this(key, snap, parent: data);

        var v = snap.GetVariable(key);
        return v != null
            ? new global::app.data.@this(key, v.Value, parent: data)
            : global::app.data.@this.NotFound(key);
    }
}
