namespace app.snapshot;

/// <summary>
/// Snapshot — captured-variable navigate/get/set.
///
/// <para>DISABLED (2026-07-09) — these doors throw. They existed only for the
/// <c>%snap.variables.x%</c> fix-and-replay developer feature (edit a captured
/// variable, then resume) and have no production plang consumer — only snapshot unit
/// tests exercise them, which are expected to fail while this is disabled. They embed
/// variable-domain knowledge INTO snapshot, the wrong owner. The real model is
/// <c>ISnapshot</c> — each app property snapshots/restores itself, snapshot becomes a
/// dumb serializable container. That redesign is its own branch; see
/// <c>Documentation/v0.2/todos.md</c> (2026-07-09 — Snapshot redesign).</para>
///
/// <para>Kept as throwing stubs (not deleted) so the doors stay named and any caller
/// fails LOUD with this pointer rather than degrading to a silent <c>NotFound</c>.</para>
/// </summary>
public sealed partial class @this
{
    private const string ReplayDisabled =
        "snapshot captured-variable navigate/replay is disabled pending the ISnapshot redesign — " +
        "see Documentation/v0.2/todos.md (2026-07-09 — Snapshot redesign).";

    /// <summary>Was: <c>%snap.variables%</c> navigation. Throws — see the class remarks.</summary>
    public override System.Threading.Tasks.ValueTask<data.@this> Navigate(data.@this parent, string key)
        => throw new System.NotSupportedException(ReplayDisabled);

    /// <summary>
    /// Was: in-place set of a captured variable (<c>set %snap.variables.x% = 2</c>).
    /// Throws — see the class remarks.
    /// </summary>
    public void SetVariable(string name, object? value)
        => throw new System.NotSupportedException(ReplayDisabled);
}
