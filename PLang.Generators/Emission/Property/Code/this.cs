using System.Text;
using Base = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Emission.Property.Code;

/// <summary>
/// Emits a [Code]-attributed property — eagerly assigned in ExecuteAsync
/// from app.Code.Get&lt;T&gt;(). Lazy access pattern allows direct test
/// usage too (Context.App.Code.Get is invoked on first read if not pre-set).
/// </summary>
public sealed record @this(
    string Name,
    string TypeName,
    bool ImplementsIContext)
    : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        var engineExpr = ImplementsIContext ? "__app ?? Context?.App" : "__app";
        sb.AppendLine($"    private {TypeName}? {Backing};");
        sb.AppendLine($"    public partial {TypeName} {Name}");
        sb.AppendLine("    {");
        sb.AppendLine($"        get {{ if ({Backing} == null) {{ var __e = {engineExpr}; if (__e != null) {Backing} = __e.Code.Get<{TypeName}>().Provider; }} return {Backing}!; }}");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public override void EmitSnapshotEntry(StringBuilder sb)
    {
        // [Code] slots are not parameter-sourced — no PrValue, no FinalValue. Skip the snapshot entry.
    }
}
