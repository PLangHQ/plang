using System.Text;
using Base = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Emission.Property.Code;

/// <summary>
/// Emits a [Code]-attributed property — a getter-only partial property that lazily
/// resolves its service provider from app.Code on first access, cached in the `field`
/// keyword's compiler-managed store. Backing-free (no hand-written field). Not a
/// parameter: no .pr value, no snapshot entry, nothing to do in Attach.
/// </summary>
public sealed record @this(
    string Name,
    string TypeName,
    bool ImplementsIContext)
    : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        // [Code] keeps a small service-cache field, filled by Attach (which surfaces the
        // "provider not registered" error) — NOT the param machinery. The getter surfaces the
        // OTHER lifecycle miss: accessed before Attach ran (an action reached outside the
        // pipeline), naming it instead of a bare NRE.
        sb.AppendLine($"    private {TypeName}? {Backing};");
        sb.AppendLine($"    public partial {TypeName} {Name} => {Backing} ?? throw new global::System.InvalidOperationException(");
        sb.AppendLine($"        \"{TypeName} is not attached — Attach(context) did not run on this action. \"");
        sb.AppendLine($"        + \"Actions run through the pipeline, which attaches [Code] providers before Run().\");");
        sb.AppendLine();
    }

    /// <summary>Resolves the [Code] provider into the field in Attach; surfaces the
    /// "not registered" error instead of a later NRE.</summary>
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
