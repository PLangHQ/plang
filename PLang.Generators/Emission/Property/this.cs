using System.Text;

namespace PLang.Generators.Emission.Property;

/// <summary>
/// Abstract base for action-property emitters.
/// Concrete leaves (DataProperty, ProviderProperty) carry the
/// per-property metadata they need and emit two slots: the partial property body
/// (EmitProperty) and the per-property __SnapshotParams entry (EmitSnapshotEntry).
///
/// Records use only primitive/string fields so Roslyn's incremental cache treats
/// instances as value-equal — no IPropertySymbol references leak in.
/// </summary>
public abstract record @this(string Name, string TypeName)
{
    /// <summary>Emits the property declaration (backing fields + partial property body).</summary>
    public abstract void EmitProperty(StringBuilder sb);

    /// <summary>Emits one block for __SnapshotParams covering this property's snapshot entry.</summary>
    public abstract void EmitSnapshotEntry(StringBuilder sb);

    /// <summary>Lowercased parameter name used in .pr lookups.</summary>
    protected string ParamName => Name.ToLowerInvariant();
    /// <summary>Internal backing field name.</summary>
    protected string Backing => $"__{Name}_backing";
    /// <summary>Internal "set" flag used by snapshot to know if the property was touched.</summary>
    protected string SetFlag => $"__{Name}_set";
}
