using System.Text;
using Base = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Emission.Property.Code;

/// <summary>
/// Emits a [Code]-attributed property — a getter-only partial property backed by a
/// field that Attach() fills from app.Code.Get&lt;T&gt;(). Not a parameter: no .pr
/// value, no snapshot entry. Attach writes the backing field directly (the property
/// is getter-only), so the provider resolves once per execution with error handling.
/// </summary>
public sealed record @this(
    string Name,
    string TypeName,
    bool ImplementsIContext)
    : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        sb.AppendLine($"    private {TypeName}? {Backing};");
        sb.AppendLine($"    public partial {TypeName} {Name} => {Backing}!;");
        sb.AppendLine();
    }

    /// <summary>
    /// Resolves the [Code] service provider into the backing field inside Attach(). A
    /// resolution failure short-circuits Attach with the error.
    /// </summary>
    public override void EmitAttach(StringBuilder sb)
    {
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var (__prov, __err) = app.Code.Get<{TypeName}>();");
        sb.AppendLine($"            if (__err != null) return __err;");
        sb.AppendLine($"            {Backing} = __prov!;");
        sb.AppendLine($"        }}");
    }

    public override void EmitSnapshotEntry(StringBuilder sb)
    {
        // [Code] slots are not parameter-sourced — no PrValue, no FinalValue. Skip the snapshot entry.
    }
}
