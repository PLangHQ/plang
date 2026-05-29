using System.Text;
using Base = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Emission.Property.Data;

/// <summary>
/// Emits a parameter property whose declared type is Data&lt;T&gt; or plain Data
/// (Data&lt;object&gt;-equivalent). Resolution flows through Action.GetParameter +
/// Data.As&lt;T&gt;(Context) — the v4 uniform shape.
/// </summary>
public sealed record @this(
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsPlainData,    // true when declared as `Data.@this` (no <T>)
    string? InnerType,    // T inside Data<T>; null for plain Data
    string? DefaultValue, // [Default(...)] literal expression; null when absent
    bool IsSensitive,    // [Sensitive] — masks PrValue/FinalValue in __SnapshotParams
    bool IsRawNameResolvable)  // T : IRawNameResolvable — emit MissingRequiredParameter guard on null .Value
    : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        // Backing field — Data<T>? regardless of property nullability so init can set it
        sb.AppendLine($"    private {TypeName}? {Backing};");
        sb.AppendLine($"    private bool {SetFlag};");
        sb.AppendLine($"    public partial {TypeName} {Name}");
        sb.AppendLine("    {");

        // After resolution, capture FromError-Data (cycle / depth-trip / type-conversion failure)
        // into __resolutionError so the post-Run check in ExecuteAsync can surface it. Without
        // this the FromError-Data lives silently on the backing field with Value=default(T).
        if (IsPlainData)
        {
            // Plain Data slot — return the CANONICAL Data, not a wrapped Data<object>. For full-match
            // %var%, that's the live variable Data; for literal values, the parameter Data itself.
            // Pattern A handlers (list.add/remove/sort/...) read .Value as the live ref so mutation
            // is visible to the variable. Architect Phase 2 Rule 4.
            sb.AppendLine($"        get {{ if (!{SetFlag}) {{ {Backing} = __ResolveData(\"{ParamName}\").AsCanonical(Context); if (!{Backing}.Success) __resolutionError = {Backing}; {SetFlag} = true; }} return {Backing}!; }}");
        }
        else if (IsNullable)
        {
            sb.AppendLine($"        get {{ if ({Backing} == null && !{SetFlag}) {{ var __d = __ResolveData(\"{ParamName}\"); {Backing} = __d.IsEmpty ? null : __d.As<{InnerType}>(Context); if ({Backing} != null && !{Backing}.Success) __resolutionError = {Backing}; {SetFlag} = true; }} return {Backing}; }}");
        }
        else if (DefaultValue != null)
        {
            sb.AppendLine($"        get {{ if ({Backing} == null) {{ var __d = __ResolveData(\"{ParamName}\"); {Backing} = __d.IsEmpty ? new global::app.data.@this<{InnerType}>(\"{ParamName}\", ({InnerType}){DefaultValue}) : __d.As<{InnerType}>(Context); if (!{Backing}.Success) __resolutionError = {Backing}; {SetFlag} = true; }} return {Backing}!; }}");
        }
        else
        {
            sb.AppendLine($"        get {{ if ({Backing} == null) {{ {Backing} = __ResolveData(\"{ParamName}\").As<{InnerType}>(Context); if (!{Backing}.Success) __resolutionError = {Backing}; {SetFlag} = true; }} return {Backing}!; }}");
        }

        sb.AppendLine($"        init {{ {Backing} = value; {SetFlag} = true; }}");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public override void EmitSnapshotEntry(StringBuilder sb)
    {
        // TypeName comes from the type system — no quote/backslash escapes needed.
        var declaredType = TypeName.Replace("global::", "");
        var prValueExpr = IsSensitive
            ? "__pr?.Value != null ? \"******\" : null"
            : "__pr?.Value";
        // Sensitive masking matches PrValue's null-guard pattern: a property accessed but
        // resolved to a null inner value reports null FinalValue, not '******'. Distinguishes
        // 'accessed-and-null' from 'accessed-and-redacted' for post-mortem analysis. {Backing}
        // is Data<T>? — the wrapper can exist with .Value=null after a null-resolving As<T>;
        // we mask only when there's an actual resolved value to redact.
        var finalValueExpr = IsSensitive
            ? $"{SetFlag} ? ({Backing}?.Value != null ? (object?)\"******\" : null) : null"
            : $"{SetFlag} ? (object?){Backing} : null";
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var __pr = __action?.Parameters?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __pr ??= __action?.Defaults?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __list.Add(new global::app.error.ParamSnapshot {{");
        sb.AppendLine($"                Name = \"{Name}\",");
        sb.AppendLine($"                DeclaredType = \"{declaredType}\",");
        sb.AppendLine($"                PrValue = {prValueExpr},");
        sb.AppendLine($"                PrType = __pr?.Type?.Value,");
        sb.AppendLine($"                FinalValue = {finalValueExpr},");
        sb.AppendLine($"                WasAccessed = {SetFlag}");
        sb.AppendLine($"            }});");
        sb.AppendLine($"        }}");
    }
}
