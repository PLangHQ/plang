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
    string? DefaultValue) // [Default(...)] literal expression; null when absent
    : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        // Backing field — Data<T>? regardless of property nullability so init can set it
        sb.AppendLine($"    private {TypeName}? {Backing};");
        sb.AppendLine($"    private bool {SetFlag};");
        sb.AppendLine($"    public partial {TypeName} {Name}");
        sb.AppendLine("    {");

        if (IsPlainData)
        {
            // Plain Data resolves as Data<object> so %var% references walk through.
            sb.AppendLine($"        get {{ if (!{SetFlag}) {{ {Backing} = __ResolveData(\"{ParamName}\").As<object>(Context); {SetFlag} = true; }} return {Backing}!; }}");
        }
        else if (IsNullable)
        {
            sb.AppendLine($"        get {{ if ({Backing} == null && !{SetFlag}) {{ var __d = __ResolveData(\"{ParamName}\"); {Backing} = __d.IsEmpty ? null : __d.As<{InnerType}>(Context); {SetFlag} = true; }} return {Backing}; }}");
        }
        else if (DefaultValue != null)
        {
            sb.AppendLine($"        get {{ if ({Backing} == null) {{ var __d = __ResolveData(\"{ParamName}\"); {Backing} = __d.IsEmpty ? new global::App.Data.@this<{InnerType}>(\"{ParamName}\", ({InnerType}){DefaultValue}) : __d.As<{InnerType}>(Context); {SetFlag} = true; }} return {Backing}!; }}");
        }
        else
        {
            sb.AppendLine($"        get {{ if ({Backing} == null) {{ {Backing} = __ResolveData(\"{ParamName}\").As<{InnerType}>(Context); {SetFlag} = true; }} return {Backing}!; }}");
        }

        sb.AppendLine($"        init {{ {Backing} = value; {SetFlag} = true; }}");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public override void EmitSnapshotEntry(StringBuilder sb)
    {
        // TypeName comes from the type system — no quote/backslash escapes needed.
        var declaredType = TypeName.Replace("global::", "");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var __pr = __action?.Parameters?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __pr ??= __action?.Defaults?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __list.Add(new global::App.Errors.ParamSnapshot {{");
        sb.AppendLine($"                Name = \"{Name}\",");
        sb.AppendLine($"                DeclaredType = \"{declaredType}\",");
        sb.AppendLine($"                PrValue = __pr?.Value,");
        sb.AppendLine($"                PrType = __pr?.Type?.Value,");
        sb.AppendLine($"                FinalValue = {SetFlag} ? (object?){Backing} : null,");
        sb.AppendLine($"                WasAccessed = {SetFlag}");
        sb.AppendLine($"            }});");
        sb.AppendLine($"        }}");
    }
}
