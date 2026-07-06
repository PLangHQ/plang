using System.Text;

namespace PLang.Generators.Emission.Property;

/// <summary>
/// Abstract base for action-property emitters.
/// Concrete leaves (DataProperty, CodeProperty) carry the
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

    /// <summary>
    /// Emits this property's resolution inside <c>Resolve()</c> — decode the .pr
    /// parameter's <c>%var%</c>/literal form (async) into a local, which the object
    /// initializer then binds to the fresh instance's init property. Default: nothing
    /// ([Code]/markers are set post-construction in <c>Attach()</c>).
    /// </summary>
    public virtual void EmitResolveLocal(StringBuilder sb, string settingModule, string settingAction) { }

    /// <summary>
    /// Emits this property's post-construction wiring inside <c>Attach()</c> — used by
    /// [Code] service slots (resolve provider from <c>app.Code</c>). Default: nothing
    /// (Data params are bound at construction via <see cref="EmitResolveLocal"/>).
    /// </summary>
    public virtual void EmitAttach(StringBuilder sb) { }

    /// <summary>Lowercased parameter name used in .pr lookups.</summary>
    protected string ParamName => Name.ToLowerInvariant();
    /// <summary>Internal backing field name (used by [Code] service slots).</summary>
    protected string Backing => $"__{Name}_backing";
}
