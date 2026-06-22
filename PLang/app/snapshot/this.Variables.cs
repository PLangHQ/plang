namespace app.snapshot;

/// <summary>
/// Snapshot — captured-variable access. The snapshot owns its variables (its
/// "Variables" section), so reading/editing one is behavior here, on the owner —
/// not a wrapper reaching in. Editing a captured variable before <c>Resume</c> is
/// the durable-execution fix-and-replay loop: <c>set %snapshot.variables.x% = 2</c>
/// then resume. The edit lands on the same list <c>Restore</c> reads, so it flows
/// into resumed execution.
///
/// <para>Interim home: under the agreed App-as-snapshot reframe these collapse
/// into <c>app.Variables</c> access (a snapshot is the App frozen at a step) —
/// see <c>Documentation/v0.2/app-as-snapshot-proposal.md</c>.</para>
/// </summary>
public sealed partial class @this
{
    private List<data.@this>? VariableList()
        => HasSection("Variables") ? Section("Variables").Read<List<data.@this>>("variables") : null;

    /// <summary>The captured variable named <paramref name="name"/>, or null when absent.</summary>
    public data.@this? GetVariable(string name)
        => VariableList()?.FirstOrDefault(v =>
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A snapshot owns its child read. <c>%snap.variables%</c> is the variable
    /// namespace — it passes through so the next segment names a variable; any
    /// other key resolves that captured variable's value. (The write side,
    /// <c>set %snap.variables.x% = 2</c>, routes to <see cref="SetVariable"/>.)
    /// </summary>
    public override System.Threading.Tasks.ValueTask<data.@this> Navigate(data.@this parent, string key)
    {
        if (string.Equals(key, "variables", StringComparison.OrdinalIgnoreCase))
            return new(new data.@this(key, this, parent: parent));

        var v = GetVariable(key);
        return new(v != null
            ? new data.@this(key, v.Peek(), parent: parent)
            : data.@this.NotFound(key));
    }

    /// <summary>Sets a captured variable's value in place (or appends one).</summary>
    public void SetVariable(string name, object? value)
    {
        var section = Section("Variables");
        var list = section.Read<List<data.@this>>("variables");
        if (list == null)
        {
            list = new List<data.@this>();
            section.Write("variables", list);
        }
        var existing = list.FirstOrDefault(v =>
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.SetValue(value);
        else list.Add(new data.@this(name, value));
    }
}
