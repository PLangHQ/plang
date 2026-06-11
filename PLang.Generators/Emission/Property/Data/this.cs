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
        var nullable = IsNullable ? "?" : "";
        // Optional (`?`) params keep their nullable TYPE but are never actually null —
        // the getter binds Data.Uninitialized for an absent slot. [NotNull] tells flow
        // analysis the truth so `await Mime.Value()` doesn't trip CS8602.
        if (IsNullable)
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.NotNull]");
        sb.AppendLine($"    public partial {TypeName}{nullable} {Name}");
        sb.AppendLine("    {");

        // The getter is a plain backing read — resolution happens at DISPATCH
        // (__ResolveParameters, awaited by ExecuteAsync/SetAction before Run/Build),
        // landing the resolved Data in the backing field: the handler instance is the
        // per-execution home, so the shared .pr parameter is never written to. The
        // sync fallback below only fires for direct C# composition (no action ran):
        // absent optional -> non-null Uninitialized (null model), [Default] -> literal.
        string fallback = IsPlainData
            ? $"global::app.data.@this.Uninitialized(\"{ParamName}\")"
            : DefaultValue != null
                ? $"new global::app.data.@this<{InnerType}>(\"{ParamName}\", {DefaultExpr})"
                : $"global::app.data.@this<{InnerType}>.Uninitialized(\"{ParamName}\")";
        sb.AppendLine($"        get {{ if (!{SetFlag}) {{ {Backing} = {fallback}; {SetFlag} = true; }} return {Backing}!; }}");
        sb.AppendLine($"        init {{ {Backing} = value; {SetFlag} = true; }}");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // For choice<X>, a [Default(X.Member)] arrives as X's underlying value (an int
    // for an enum). Cast through X so int -> X -> choice<X> chains: (choice<X>)(X)(0).
    private string DefaultExpr
    {
        get
        {
            const string ChoicePrefix = "global::app.type.choice.@this<";
            return (InnerType != null && InnerType.StartsWith(ChoicePrefix, System.StringComparison.Ordinal))
                ? $"({InnerType})({InnerType.Substring(ChoicePrefix.Length, InnerType.Length - ChoicePrefix.Length - 1)})({DefaultValue})"
                : $"({InnerType})({DefaultValue})";
        }
    }

    public override void EmitDispatchResolve(StringBuilder sb)
    {
        // Dispatch-time resolution: decode the .pr parameter's %var%/literal form once
        // per execution, in this execution's context, into the backing field. The
        // `!set` guard respects init-supplied values (direct C# composition).
        // Resolution failures land on the resolved Data's Error; ExecuteAsync's
        // __resolutionError guard surfaces them before Run().
        sb.AppendLine($"        if (!{SetFlag})");
        sb.AppendLine("        {");
        if (IsPlainData)
        {
            // Plain Data slot — the CANONICAL Data: live variable for full-match %var%,
            // the parameter itself for a literal.
            sb.AppendLine($"            {Backing} = await __ResolveData(\"{ParamName}\").AsCanonical(Context);");
            sb.AppendLine($"            if (!{Backing}.Success) __resolutionError = {Backing};");
        }
        else if (IsNullable)
        {
            sb.AppendLine($"            var __d = __ResolveData(\"{ParamName}\");");
            sb.AppendLine($"            {Backing} = await __d.IsEmpty() ? global::app.data.@this<{InnerType}>.Uninitialized(\"{ParamName}\") : __d.ShallowClone<{InnerType}>(await __d.Value<{InnerType}>());");
            sb.AppendLine($"            if (!{Backing}.Success) __resolutionError = {Backing};");
        }
        else if (DefaultValue != null)
        {
            // [Default] fires on an absent slot AND on a null-resolving value
            // (`mime: %unsetVar%` lands on the default too).
            sb.AppendLine($"            var __d = __ResolveData(\"{ParamName}\");");
            sb.AppendLine($"            {Backing} = await __d.IsEmpty() ? new global::app.data.@this<{InnerType}>(\"{ParamName}\", {DefaultExpr}) : __d.ShallowClone<{InnerType}>(await __d.Value<{InnerType}>());");
            sb.AppendLine($"            if (!{Backing}.Success) __resolutionError = {Backing};");
            sb.AppendLine($"            else if ({Backing}.Peek() is global::app.type.@null.@this or global::app.type.item.absent) {Backing} = new global::app.data.@this<{InnerType}>(\"{ParamName}\", {DefaultExpr});");
        }
        else
        {
            sb.AppendLine($"            var __d = __ResolveData(\"{ParamName}\");");
            sb.AppendLine($"            {Backing} = __d.ShallowClone<{InnerType}>(await __d.Value<{InnerType}>());");
            sb.AppendLine($"            if (!{Backing}.Success) __resolutionError = {Backing};");
        }
        sb.AppendLine($"            {SetFlag} = true;");
        sb.AppendLine("        }");
    }

    public override void EmitSnapshotEntry(StringBuilder sb)
    {
        // TypeName comes from the type system — no quote/backslash escapes needed.
        var declaredType = TypeName.Replace("global::", "");
        // Diagnostic snapshot reads the param's current rung (Peek — no resolve, no
        // await) since there is no public sync .Value door; the value door is async.
        var prValueExpr = IsSensitive
            ? "__pr?.Peek() != null ? \"******\" : null"
            : "__pr?.Peek()";
        // Sensitive masking matches PrValue's null-guard pattern: a property accessed but
        // resolved to a null inner value reports null FinalValue, not '******'. Distinguishes
        // 'accessed-and-null' from 'accessed-and-redacted' for post-mortem analysis. {Backing}
        // is Data<T>? — the wrapper can exist with a null inner value after a null-resolving As<T>;
        // we mask only when there's an actual resolved value to redact.
        var finalValueExpr = IsSensitive
            ? $"{SetFlag} ? ({Backing}?.Peek() != null ? (object?)\"******\" : null) : null"
            : $"{SetFlag} ? (object?){Backing} : null";
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var __pr = __action?.Parameters?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __pr ??= __action?.Defaults?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __list.Add(new global::app.error.ParamSnapshot {{");
        sb.AppendLine($"                Name = \"{Name}\",");
        sb.AppendLine($"                DeclaredType = \"{declaredType}\",");
        sb.AppendLine($"                PrValue = {prValueExpr},");
        sb.AppendLine($"                PrType = __pr?.Type?.Name,");
        sb.AppendLine($"                FinalValue = {finalValueExpr},");
        sb.AppendLine($"                WasAccessed = {SetFlag}");
        sb.AppendLine($"            }});");
        sb.AppendLine($"        }}");
    }
}
