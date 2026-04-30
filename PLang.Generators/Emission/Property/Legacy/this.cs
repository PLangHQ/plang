using System.Text;
using Base = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Emission.Property.Legacy;

/// <summary>
/// Emission for legacy raw-scalar properties — `partial string ListName`,
/// [VariableName] strings, value types without the Data&lt;T&gt; wrapper.
///
/// v4 deletes these in Phase 5 (build-time check rejects non-Data&lt;T&gt;
/// non-Provider properties). This class exists during the migration sweep so
/// the existing handlers under App/modules/list/, App/modules/loop/, and
/// App/modules/variable/ keep building until they're converted. Phase 5
/// removes this file along with the [VariableName] attribute.
/// </summary>
public sealed record @this(
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsValueType,
    bool IsAppResolvable,
    bool IsVariableName,
    string? DefaultValue,
    bool IsSensitive) : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        // Backing field — type depends on whether the property type is already nullable.
        if (IsValueType && !IsNullable)
        {
            sb.AppendLine($"    private {TypeName} {Backing};");
        }
        else
        {
            sb.AppendLine($"    private {TypeName}{(IsNullable ? "" : "?")} {Backing};");
        }
        sb.AppendLine($"    private bool {SetFlag};");

        string resolveExpr;
        if (IsAppResolvable)
        {
            var rawStr = $"__Resolve<string>(\"{ParamName}\")";
            if (DefaultValue != null) rawStr = $"({rawStr} ?? {DefaultValue})";
            resolveExpr = $"{TypeName}.Resolve({rawStr}, Context)!";
        }
        else if (IsVariableName)
        {
            resolveExpr = $"__StripPercent(\"{ParamName}\")!";
        }
        else if (DefaultValue != null && IsValueType && !IsNullable)
        {
            resolveExpr = $"(__HasParam(\"{ParamName}\") ? __Resolve<{TypeName}>(\"{ParamName}\") : {DefaultValue})";
        }
        else if (DefaultValue != null)
        {
            resolveExpr = $"__Resolve<{TypeName}>(\"{ParamName}\") ?? ({TypeName}){DefaultValue}";
        }
        else if (IsNullable || IsValueType)
        {
            resolveExpr = $"__Resolve<{TypeName}>(\"{ParamName}\")";
        }
        else
        {
            resolveExpr = $"__Resolve<{TypeName}>(\"{ParamName}\")!";
        }

        sb.AppendLine($"    public partial {TypeName} {Name}");
        sb.AppendLine("    {");
        sb.AppendLine($"        get {{ if (!{SetFlag}) {{ {Backing} = {resolveExpr}; {SetFlag} = true; }} return {Backing}!; }}");
        sb.AppendLine($"        init {{ {Backing} = value; {SetFlag} = true; }}");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public override void EmitSnapshotEntry(StringBuilder sb)
    {
        var declaredType = TypeName.Replace("global::", "");
        var prValueExpr = IsSensitive
            ? "__pr?.Value != null ? \"******\" : null"
            : "__pr?.Value";
        // Sensitive masking matches PrValue's null-guard pattern: a property accessed but
        // resolved to null reports null FinalValue, not '******'. Distinguishes 'accessed-
        // and-null' from 'accessed-and-redacted' for post-mortem analysis. Non-nullable
        // value types can't be null, so skip the guard there (avoids CS0472).
        string finalValueExpr;
        if (IsSensitive && IsValueType && !IsNullable)
            finalValueExpr = $"{SetFlag} ? (object?)\"******\" : null";
        else if (IsSensitive)
            finalValueExpr = $"{SetFlag} ? ({Backing} != null ? (object?)\"******\" : null) : null";
        else
            finalValueExpr = $"{SetFlag} ? (object?){Backing} : null";
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var __pr = __action?.Parameters?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __pr ??= __action?.Defaults?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __list.Add(new global::App.Errors.ParamSnapshot {{");
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
