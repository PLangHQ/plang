namespace app.Attributes;

/// <summary>
/// Override the PLang catalog name when the desired name diverges from what
/// the source generator and Registry would derive from the class name.
///
/// <para>
/// Most types do <strong>not</strong> need this attribute — class names
/// lowercase cleanly to PLang vocabulary (<c>Results</c> → "results",
/// <c>MockHandle</c> → "mockhandle"), and <c>@this</c> classes use the
/// last namespace segment. The attribute exists only for the rare cases
/// where the class name cannot encode the wanted PLang name:
/// </para>
///
/// <list type="bullet">
///   <item><c>GoalCall</c> wants <c>goal.call</c> — the desired name contains a dot.</item>
///   <item><c>Schema.@this</c> wants <c>catalog</c> — fully divergent.</item>
/// </list>
///
/// <para>
/// Catalog metadata (example value, description, scalar shape) used to live
/// on this attribute as <c>Shape</c>/<c>Example</c>/<c>Description</c>
/// parameters; those moved to a static-property convention read via
/// reflection by <c>app.types.@this.BuildTypeEntries</c> — declare
/// <c>public static string Example =&gt; "…";</c> on the type itself.
/// </para>
/// </summary>
[System.AttributeUsage(
    System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Enum,
    AllowMultiple = false)]
public sealed class PlangTypeAttribute : System.Attribute
{
    /// <summary>
    /// Explicit name override. Null means "derive from class name lowercased
    /// (or last-namespace-segment for <c>@this</c> classes)" — the common
    /// case. Set this only when the desired PLang name can't be expressed
    /// through that derivation (e.g. <c>goal.call</c> with a dot,
    /// <c>catalog</c> fully divergent from class name).
    /// </summary>
    public string? Name { get; }

    public PlangTypeAttribute() { }
    public PlangTypeAttribute(string name) { Name = name; }
}
