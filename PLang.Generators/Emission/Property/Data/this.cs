using System.Text;
using Base = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Emission.Property.Data;

/// <summary>
/// Emits a parameter property whose declared type is Data&lt;T&gt; or plain Data
/// (Data&lt;object&gt;-equivalent). The property is a plain <c>init</c> auto-property;
/// Resolve() decodes the .pr parameter into a local and binds it via the object
/// initializer. Inline C# composition (prebound) sets the params it needs; the
/// field-initializer fallback covers an absent slot.
/// </summary>
public sealed record @this(
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsPlainData,    // true when declared as `Data.@this` (no <T>)
    string? InnerType,    // T inside Data<T>; null for plain Data
    string? DefaultValue, // [Default(...)] literal expression; null when absent
    bool IsSensitive,    // [Sensitive] — masks PrValue/FinalValue in __SnapshotParams
    bool IsName)  // T : app.variable.IName — emit MissingRequiredParameter guard on null .Value
    : Base(Name, TypeName)
{
    public override void EmitProperty(StringBuilder sb)
    {
        var nullable = IsNullable ? "?" : "";
        // Optional (`?`) params keep their nullable TYPE but are never actually null.
        // [NotNull] tells flow analysis the truth so `await Mime.Value()` doesn't trip CS8602.
        if (IsNullable)
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.NotNull]");
        // Backing-free: the `field` keyword's compiler store — no hand-written field, no
        // flag / fallback / reset machinery. The field-initializer covers an absent slot
        // (Uninitialized / [Default] literal); Resolve() binds the value via the object initializer.
        sb.AppendLine($"    public partial {TypeName}{nullable} {Name} {{ get => field!; init => field = value; }} = {Fallback};");
        sb.AppendLine();
    }

    /// <summary>Field-initializer / absent-slot value: Uninitialized, or the [Default] literal.</summary>
    private string Fallback => IsPlainData
        ? $"global::app.data.@this.Uninitialized(\"{ParamName}\")"
        : DefaultValue != null
            ? $"new global::app.data.@this<{InnerType}>(\"{ParamName}\", {DefaultExpr})"
            : $"global::app.data.@this<{InnerType}>.Uninitialized(\"{ParamName}\")";

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

    /// <summary>Local variable name the resolved Data lands in inside Resolve().</summary>
    private string Local => $"__{Name}";

    /// <summary>Object-initializer fragment binding the resolved local to the init property.</summary>
    public string InitAssignment => $"{Name} = {Local}";

    /// <summary>
    /// Emits this param's resolution inside Resolve(): decode the .pr parameter's
    /// %var%/literal form (async, in this execution's context) into a local. A resolution
    /// failure short-circuits Resolve with the prefixed error. The object initializer
    /// then binds <see cref="InitAssignment"/> onto the fresh instance.
    /// </summary>
    public override void EmitResolveLocal(StringBuilder sb)
    {
        if (IsPlainData)
        {
            // Plain Data slot — hand over the Data reference as-is. No eager resolve: a
            // %var%/template resolves lazily on its own door (await Value()), so the handler
            // decides (variable.set stores the ref verbatim; a reader renders it). Data flows.
            sb.AppendLine($"        {TypeName} {Local} = __ResolveData(action, \"{ParamName}\", context);");
            sb.AppendLine($"        if (!{Local}.Success) return (null, __PrefixActionContext({Local}.Error!, action));");
        }
        else if (IsNullable)
        {
            sb.AppendLine($"        {TypeName} {Local};");
            sb.AppendLine("        {");
            sb.AppendLine($"            var __d = __ResolveData(action, \"{ParamName}\", context);");
            sb.AppendLine($"            {Local} = await __d.IsEmpty() ? global::app.data.@this<{InnerType}>.Uninitialized(\"{ParamName}\") : __d.As<{InnerType}>();");
            sb.AppendLine($"            if (!{Local}.Success) return (null, __PrefixActionContext({Local}.Error!, action));");
            sb.AppendLine("        }");
        }
        else if (DefaultValue != null)
        {
            // [Default] fires on an absent slot AND on a null-resolving value
            // (`mime: %unsetVar%` lands on the default too).
            sb.AppendLine($"        {TypeName} {Local};");
            sb.AppendLine("        {");
            sb.AppendLine($"            var __d = __ResolveData(action, \"{ParamName}\", context);");
            sb.AppendLine($"            {Local} = await __d.IsEmpty() ? new global::app.data.@this<{InnerType}>(\"{ParamName}\", {DefaultExpr}) : __d.As<{InnerType}>();");
            sb.AppendLine($"            if (!{Local}.Success) return (null, __PrefixActionContext({Local}.Error!, action));");
            sb.AppendLine($"            else if ({Local}.Peek() is global::app.type.@null.@this) {Local} = new global::app.data.@this<{InnerType}>(\"{ParamName}\", {DefaultExpr});");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        {TypeName} {Local};");
            sb.AppendLine("        {");
            sb.AppendLine($"            var __d = __ResolveData(action, \"{ParamName}\", context);");
            sb.AppendLine($"            {Local} = __d.As<{InnerType}>();");
            sb.AppendLine($"            if (!{Local}.Success) return (null, __PrefixActionContext({Local}.Error!, action));");
            sb.AppendLine("        }");
        }
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
        // The instance is always fully populated (every param is bound at construction),
        // so the final value reads straight off the property. Sensitive params mask a
        // present value; a null inner value reports null (accessed-and-null, not redacted).
        var finalValueExpr = IsSensitive
            ? $"({Name}.Peek() != null ? (object?)\"******\" : null)"
            : $"(object?){Name}";
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var __pr = __action?.Parameters?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __pr ??= __action?.Defaults?.FirstOrDefault(p => string.Equals(p.Name, \"{Name}\", System.StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine($"            __list.Add(new global::app.error.ParamSnapshot {{");
        sb.AppendLine($"                Name = \"{Name}\",");
        sb.AppendLine($"                DeclaredType = \"{declaredType}\",");
        sb.AppendLine($"                PrValue = {prValueExpr},");
        sb.AppendLine($"                PrType = __pr?.Type?.Name,");
        sb.AppendLine($"                FinalValue = {finalValueExpr},");
        sb.AppendLine($"                WasAccessed = true");
        sb.AppendLine($"            }});");
        sb.AppendLine($"        }}");
    }
}
