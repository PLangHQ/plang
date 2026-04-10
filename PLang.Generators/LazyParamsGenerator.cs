using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PLang.Generators;

[Generator]
public class LazyParamsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // [Action]-attributed partial classes
        var actionDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsPartialClassWithActionAttribute(node),
                transform: static (ctx, _) => GetActionClassInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(actionDeclarations, static (spc, info) =>
        {
            if (info is null) return;
            var source = GenerateActionCode(info);
            spc.AddSource(SanitizeHintName($"{info.FullName}.Action.g.cs"), source);
        });
    }

    private static string SanitizeHintName(string hintName)
    {
        return hintName.Replace("@", "");
    }

    private static bool IsPartialClassWithActionAttribute(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl) return false;
        if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword)) return false;

        // Check for [Action] attribute
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == "Action" || name == "ActionAttribute"
                    || name.EndsWith(".Action") || name.EndsWith(".ActionAttribute"))
                    return true;
            }
        }
        return false;
    }

    private static ActionClassInfo? GetActionClassInfo(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null) return null;

        // Verify [Action] attribute via semantic model
        var hasActionAttr = classSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "ActionAttribute"
            && a.AttributeClass.ContainingNamespace.ToDisplayString() == "App.modules");
        if (!hasActionAttr) return null;

        // Check if it implements IContext
        var implementsIContext = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IContext"
            && i.ContainingNamespace.ToDisplayString() == "App.modules");

        // Check if it implements IChannel
        var implementsIChannel = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IChannel"
            && i.ContainingNamespace.ToDisplayString() == "App.modules");

        // Check if it implements IAction
        var implementsIAction = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IAction"
            && i.ContainingNamespace.ToDisplayString() == "App.modules");

        // Check if it implements IStep
        var implementsIStep = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IStep"
            && i.ContainingNamespace.ToDisplayString() == "App.modules");

        // Check if it implements IStatic
        var implementsIStatic = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IStatic"
            && i.ContainingNamespace.ToDisplayString() == "App.modules");

        // Find partial properties (declared by author, needing generated implementation)
        var properties = new List<ActionPropertyInfo>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IPropertySymbol prop
                && prop.IsPartialDefinition
                && prop.DeclaredAccessibility == Accessibility.Public
                && !prop.IsStatic)
            {
                // Read [Default] attribute if present
                string? defaultValue = null;
                var defaultAttr = prop.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.Name == "DefaultAttribute");
                if (defaultAttr != null && defaultAttr.ConstructorArguments.Length > 0)
                {
                    var arg = defaultAttr.ConstructorArguments[0];
                    if (arg.Value is string strVal)
                        defaultValue = "\"" + strVal.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                    else if (arg.Value != null)
                    {
                        // Enum defaults need an explicit cast: (EnumType)intValue
                        if (prop.Type.TypeKind == TypeKind.Enum)
                            defaultValue = $"({prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){arg.Value}";
                        else
                            defaultValue = arg.Value.ToString()!.ToLowerInvariant();
                    }
                }

                // Read [VariableName] attribute if present
                var isVariableName = prop.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "VariableNameAttribute");

                // Read [Provider] attribute if present
                var isProvider = prop.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "ProviderAttribute");

                // Read validation attributes
                var isInitiated = prop.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "IsInitiatedAttribute");
                var isNotNull = prop.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "IsNotNullAttribute");

                // Check if type has static Resolve(string, Context.@this) method
                var isAppResolvable = prop.Type is INamedTypeSymbol namedType
                    && namedType.GetMembers("Resolve")
                        .OfType<IMethodSymbol>()
                        .Any(m => m.IsStatic
                            && m.Parameters.Length == 2
                            && m.Parameters[0].Type.SpecialType == SpecialType.System_String
                            && (m.Parameters[1].Type.ContainingNamespace?.ToDisplayString() == "App"
                                || m.Parameters[1].Type.ContainingNamespace?.ToDisplayString() == "App.Actor.Context"));

                var rawType = prop.Type;
                if (rawType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
                    rawType = nullableType.TypeArguments[0];

                var implementsIEvent = rawType is INamedTypeSymbol evt
                    && evt.AllInterfaces.Any(i =>
                        i.Name == "IEvent"
                        && i.ContainingNamespace.ToDisplayString() == "App.modules");

                properties.Add(new ActionPropertyInfo(
                    prop.Name,
                    prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    prop.NullableAnnotation == NullableAnnotation.Annotated,
                    prop.Type.IsValueType,
                    defaultValue,
                    isVariableName,
                    isAppResolvable,
                    isProvider,
                    isInitiated,
                    isNotNull,
                    implementsIEvent));
            }
        }

        return new ActionClassInfo(
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            $"{classSymbol.ContainingNamespace}.{classSymbol.Name}",
            implementsIContext,
            implementsIChannel,
            implementsIAction,
            implementsIStep,
            implementsIStatic,
            properties);
    }

    private static string GenerateActionCode(ActionClassInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Text.RegularExpressions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {info.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"partial class {info.ClassName} : App.modules.ICodeGenerated");
        sb.AppendLine("{");

        // IContext auto-provision
        if (info.ImplementsIContext)
        {
            sb.AppendLine("    public App.Actor.Context.@this Context { get; set; } = null!;");
            sb.AppendLine();
        }

        // IChannel auto-provision
        if (info.ImplementsIChannel)
        {
            sb.AppendLine("    public App.Channels.@this Channels { get; set; } = null!;");
            sb.AppendLine();
        }

        // IAction auto-provision
        if (info.ImplementsIAction)
        {
            sb.AppendLine("    public App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;");
            sb.AppendLine();
        }

        // IStep auto-provision
        if (info.ImplementsIStep)
        {
            sb.AppendLine("    public App.Goals.Goal.Steps.Step.@this Step { get; set; } = null!;");
            sb.AppendLine();
        }

        // IStatic auto-provision
        if (info.ImplementsIStatic)
        {
            sb.AppendLine("    public System.Collections.Concurrent.ConcurrentDictionary<string, object?> Static { get; set; } = null!;");
            sb.AppendLine();
        }

        // Resolution state
        sb.AppendLine("    private App.Goals.Goal.Steps.Step.Actions.Action.@this? __action;");
        sb.AppendLine("    private App.Variables.@this? __variables;");
        sb.AppendLine("    private App.@this? __app;");
        sb.AppendLine("    private App.Data.@this? __resolutionError;");
        sb.AppendLine();

        // Partial property implementations
        foreach (var prop in info.Properties)
        {
            var backingField = $"__{prop.Name}_backing";
            var setFlag = $"__{prop.Name}_set";

            // [Provider] properties — resolved lazily from app.Providers
            // Works both via ExecuteAsync (__app) and direct test usage (Context.App)
            if (prop.IsProvider)
            {
                var engineExpr = info.ImplementsIContext ? "__app ?? Context?.App" : "__app";
                sb.AppendLine($"    private {prop.TypeName}? {backingField};");
                sb.AppendLine($"    public partial {prop.TypeName} {prop.Name}");
                sb.AppendLine("    {");
                sb.AppendLine($"        get {{ if ({backingField} == null) {{ var __e = {engineExpr}; if (__e != null) {{ var __r = __e.Providers.Get<{prop.TypeName}>(); if (__r.Success) {backingField} = __r.Value; }} }} return {backingField}!; }}");
                sb.AppendLine("    }");
                sb.AppendLine();
                continue;
            }

            if (prop.IsValueType && !prop.IsNullable)
            {
                sb.AppendLine($"    private {prop.TypeName} {backingField};");
            }
            else if (prop.IsNullable)
            {
                // Already nullable (e.g. int?, string?) — don't add another ?
                sb.AppendLine($"    private {prop.TypeName} {backingField};");
            }
            else
            {
                sb.AppendLine($"    private {prop.TypeName}? {backingField};");
            }
            sb.AppendLine($"    private bool {setFlag};");

            // Build the get expression
            var paramName = prop.Name.ToLowerInvariant();
            string resolveExpr;
            if (prop.IsAppResolvable)
            {
                // Context-resolvable types: resolve raw string then call Type.Resolve(string, Context)
                var rawStr = $"__Resolve<string>(\"{paramName}\")";
                if (prop.DefaultValue != null)
                    rawStr = $"({rawStr} ?? {prop.DefaultValue})";
                resolveExpr = $"{prop.TypeName}.Resolve({rawStr}, Context)!";
            }
            else if (prop.IsVariableName)
            {
                // [VariableName] — strip % markers instead of resolving from memory
                resolveExpr = $"__StripPercent(\"{paramName}\")!";
            }
            else if (prop.DefaultValue != null && prop.IsValueType && !prop.IsNullable)
            {
                // Value types can't use ?? — use ternary with __HasParam
                resolveExpr = $"(__HasParam(\"{paramName}\") ? __Resolve<{prop.TypeName}>(\"{paramName}\") : {prop.DefaultValue})";
            }
            else if (prop.DefaultValue != null)
            {
                // Reference/nullable types — ?? works fine
                resolveExpr = $"__Resolve<{prop.TypeName}>(\"{paramName}\") ?? ({prop.TypeName}){prop.DefaultValue}";
            }
            else if (prop.IsNullable)
            {
                resolveExpr = $"__Resolve<{prop.TypeName}>(\"{paramName}\")";
            }
            else if (prop.IsValueType)
            {
                resolveExpr = $"__Resolve<{prop.TypeName}>(\"{paramName}\")";
            }
            else
            {
                resolveExpr = $"__Resolve<{prop.TypeName}>(\"{paramName}\")!";
            }

            sb.AppendLine($"    public partial {prop.TypeName} {prop.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get {{ if (!{setFlag}) {{ {backingField} = {resolveExpr}; {setFlag} = true; }} return {backingField}!; }}");
            sb.AppendLine($"        init {{ {backingField} = value; {setFlag} = true; }}");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // ParamData() accessor — gives handler access to the underlying Data for any parameter
        // Usage: ParamData(nameof(Size))?.Error, ParamData(nameof(FilePath))?.Success
        sb.AppendLine("    private System.Collections.Generic.Dictionary<string, App.Data.@this?>? __paramData;");
        sb.AppendLine("    protected App.Data.@this? ParamData(string paramName)");
        sb.AppendLine("        => __paramData != null && __paramData.TryGetValue(paramName, out var d) ? d : null;");
        sb.AppendLine();

        // Data() / Error() convenience helpers — so handlers write Data(value) instead of App.Data.@this.Ok(value)
        // Skip if the class has a property named "Data" (would collide)
        var hasDataProperty = info.Properties.Any(p => p.Name == "Data");
        if (!hasDataProperty)
        {
            sb.AppendLine("    protected static App.Data.@this Data() => App.Data.@this.Ok();");
            sb.AppendLine("    protected static App.Data.@this Data(object? value) => App.Data.@this.Ok(value);");
            sb.AppendLine("    protected static App.Data.@this Data(object? value, App.Data.Type? type) => App.Data.@this.Ok(value, type);");
        }
        var hasErrorProperty = info.Properties.Any(p => p.Name == "Error");
        if (!hasErrorProperty)
        {
            sb.AppendLine("    protected static App.Data.@this Error(App.Errors.IError error) => App.Data.@this.FromError(error);");
        }
        sb.AppendLine();

        // ExecuteAsync
        // __action is set by App.Run (from ICodeGenerated), __action removed
        sb.AppendLine();
        sb.AppendLine("    public async System.Threading.Tasks.Task<App.Data.@this> ExecuteAsync(");
        sb.AppendLine("        App.Goals.Goal.Steps.Step.Actions.Action.@this action, App.Actor.Context.@this context)");
        sb.AppendLine("    {");
        sb.AppendLine("        __action = action;");
        sb.AppendLine("        __variables = context.Variables;");
        sb.AppendLine("        __app = context.App;");
        sb.AppendLine("        var app = __app!;");
        sb.AppendLine("        __resolutionError = null;");
        sb.AppendLine("        __paramData = new(StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine("        var __step = __action?.Step;");
        sb.AppendLine("        var __callFrames = context.CallStack?.GetFrames() ?? (System.Collections.Generic.IReadOnlyList<App.CallStack.CallFrame>)System.Array.Empty<App.CallStack.CallFrame>();");

        if (info.ImplementsIContext)
        {
            sb.AppendLine("        Context = context;");
        }
        if (info.ImplementsIChannel)
        {
            sb.AppendLine("        Channels = (context.Actor ?? app.User).Channels;");
        }
        if (info.ImplementsIAction)
        {
            sb.AppendLine("        Action = __action;");
        }
        if (info.ImplementsIStep)
        {
            sb.AppendLine("        Step = __action?.Step;");
        }
        if (info.ImplementsIStatic)
        {
            // Module namespace: e.g., "App.modules.timer" → "timer"
            var moduleNs = info.Namespace;
            var prefix = "App.modules.";
            var moduleName = moduleNs.StartsWith(prefix) ? moduleNs.Substring(prefix.Length).Split('.')[0] : moduleNs;
            sb.AppendLine($"        Static = context.GetModuleStatic(\"{moduleName}\");");
        }

        // Push callstack frame for this action (only when dispatched from .pr)
        sb.AppendLine("        var __frame = __action != null ? context.CallStack?.Push(__action) : null;");
        sb.AppendLine();

        // Save and set context.Step/Goal/Event — restored in finally after Run()
        sb.AppendLine("        var __previousStep = context.Step;");
        sb.AppendLine("        var __previousGoal = context.Goal;");
        sb.AppendLine("        var __previousEvent = context.Event;");
        sb.AppendLine("        context.Step = __action?.Step;");
        sb.AppendLine("        if (context.Step != null) context.Step.Context = context;");
        sb.AppendLine("        context.Goal = __action?.Step?.Goal;");
        sb.AppendLine();

        // Resolve [Provider] properties from app.Providers
        foreach (var prop in info.Properties)
        {
            if (!prop.IsProvider) continue;
            var backingField = $"__{prop.Name}_backing";
            sb.AppendLine($"        var __{prop.Name}_result = app.Providers.Get<{prop.TypeName}>();");
            sb.AppendLine($"        if (!__{prop.Name}_result.Success) return __{prop.Name}_result;");
            sb.AppendLine($"        {backingField} = __{prop.Name}_result.Value!;");
            sb.AppendLine();
        }

        // Check for IEvent on resolved properties — set context.Event if present
        // The object tells us at runtime if it carries event context
        foreach (var prop in info.Properties)
        {
            if (prop.IsProvider || prop.IsVariableName || prop.IsValueType) continue;
            if (prop.TypeName is "string" or "global::System.String") continue;
            // Only emit for types that implement IEvent (compile-time safe)
            if (!prop.ImplementsIEvent) continue;
            sb.AppendLine($"        if ({prop.Name}?.Event != null)");
            sb.AppendLine($"            context.Event = {prop.Name}.Event;");
        }
        sb.AppendLine();

        // Validate non-nullable, non-defaulted properties
        foreach (var prop in info.Properties)
        {
            if (prop.IsNullable || prop.DefaultValue != null || prop.IsAppResolvable || prop.IsProvider)
                continue;

            if (!prop.IsValueType)
            {
                if (prop.TypeName == "string" || prop.TypeName == "global::System.String")
                {
                    sb.AppendLine($"        if (string.IsNullOrEmpty({prop.Name}))");
                }
                else
                {
                    sb.AppendLine($"        if ({prop.Name} == null)");
                }
                sb.AppendLine($"            return App.Data.@this.FromError(new App.Errors.ServiceError(");
                sb.AppendLine($"                \"'{prop.Name.ToLowerInvariant()}' is required\", __step, __callFrames, \"MissingParameter\", 400));");
            }
        }

        // Validate [IsNotNull] — check the Data parameter's .Value directly
        // Only when dispatched from .pr (__action?.Parameters set). For C# composition, properties are set via init.
        if (info.Properties.Any(p => p.IsNotNull && !p.IsValueType))
        {
            sb.AppendLine("        if (__action?.Parameters != null)");
            sb.AppendLine("        {");
            foreach (var prop in info.Properties)
            {
                if (prop.IsNotNull && !prop.IsValueType)
                {
                    var paramName = prop.Name.ToLowerInvariant();
                    sb.AppendLine($"            if (__action?.Parameters.FirstOrDefault(d => string.Equals(d.Name, \"{paramName}\", StringComparison.OrdinalIgnoreCase))?.Value == null)");
                    sb.AppendLine($"                return App.Data.@this.FromError(new App.Errors.ServiceError(");
                    sb.AppendLine($"                    \"'{paramName}' must have a value\", __step, __callFrames, \"ValueRequired\", 400));");
                }
            }
            sb.AppendLine("        }");
        }

        sb.AppendLine("        if (__resolutionError != null) { context.Step = __previousStep; context.Goal = __previousGoal; return __resolutionError; }");
        sb.AppendLine();

        // Actor switching — if the class has an Actor property, wrap Run() with save/switch/restore
        var hasActorProperty = info.Properties.Any(p => p.Name == "Actor" && p.TypeName.Contains("Actor"));
        if (hasActorProperty)
        {
            sb.AppendLine("        var __previousActor = app.CurrentActor;");
            sb.AppendLine("        if (Actor != null && Actor != context.Actor)");
            sb.AppendLine("            app.CurrentActor = Actor;");
        }

        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            return await Run();");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (System.Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return App.Data.@this.FromError(new App.Errors.ServiceError(");
        sb.AppendLine("                ex.Message, __step, __callFrames, \"ServiceError\", 400) { Exception = ex });");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        if (hasActorProperty)
        {
            sb.AppendLine("            app.CurrentActor = __previousActor;");
        }
        sb.AppendLine("            __frame?.SnapshotVariables(context.Variables);");
        sb.AppendLine("            if (context.CallStack != null) context.CallStack.PopAsync().GetAwaiter().GetResult();");
        sb.AppendLine("            context.Step = __previousStep;");
        sb.AppendLine("            context.Goal = __previousGoal;");
        sb.AppendLine("            context.Event = __previousEvent;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // __Resolve<T> helper
        sb.AppendLine("    private T? __Resolve<T>(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var data = __action?.Parameters?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        data ??= __action?.Defaults?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        if (data?.Value is string str && str.Contains('%'))");
        sb.AppendLine("        {");
        sb.AppendLine("            var fullMatch = Regex.Match(str, @\"^%([^%]+)%$\");");
        sb.AppendLine("            if (fullMatch.Success)");
        sb.AppendLine("            {");
        sb.AppendLine("                var __resolved = __variables!.Get(fullMatch.Groups[1].Value);");
        sb.AppendLine("                __paramData?[name] = __resolved;");
        sb.AppendLine("                if (__resolved != null && !__resolved.Success)");
        sb.AppendLine("                {");
        sb.AppendLine("                    __resolutionError = __resolved;");
        sb.AppendLine("                    return default;");
        sb.AppendLine("                }");
        sb.AppendLine("                return __TryConvert<T>(__resolved?.Value, name);");
        sb.AppendLine("            }");
        sb.AppendLine("            var interpolated = Regex.Replace(str, @\"%([^%]+)%\", m => {");
        sb.AppendLine("                var __r = __variables!.Get(m.Groups[1].Value);");
        sb.AppendLine("                // Error Data passes through — format the error instead of aborting");
        sb.AppendLine("                if (__r != null && !__r.Success) return __r.ToString();");
        sb.AppendLine("                return __FormatValue(__r?.Value);");
        sb.AppendLine("            });");
        sb.AppendLine("            return __TryConvert<T>(interpolated, name);");
        sb.AppendLine("        }");
        sb.AppendLine("        if (data?.Value is System.Collections.IList || data?.Value is System.Collections.IDictionary)");
        sb.AppendLine("        {");
        sb.AppendLine("            var __deepResolved = __variables!.ResolveDeep(data.Value);");
        sb.AppendLine("            __paramData?[name] = data;");
        sb.AppendLine("            return __TryConvert<T>(__deepResolved, name);");
        sb.AppendLine("        }");
        sb.AppendLine("        __paramData?[name] = data;");
        sb.AppendLine("        return data != null");
        sb.AppendLine("            ? __TryConvert<T>(data.Value, name)");
        sb.AppendLine("            : default;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private T? __TryConvert<T>(object? value, string paramName)");
        sb.AppendLine("    {");
        sb.AppendLine("        var (__result, __error) = App.Utils.TypeMapping.TryConvertTo(value, typeof(T));");
        sb.AppendLine("        if (__error != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            __resolutionError = App.Data.@this.FromError(");
        sb.AppendLine("                new App.Errors.ActionError(");
        sb.AppendLine("                    $\"Parameter '{paramName}': {__error.Message}\",");
        sb.AppendLine("                    \"ConversionError\", __error.StatusCode)");
        sb.AppendLine("                    { FixSuggestion = __error.FixSuggestion });");
        sb.AppendLine("            return default;");
        sb.AppendLine("        }");
        sb.AppendLine("        // Stamp GoalCalls with the action so goal resolution can navigate action → step → goal");
        sb.AppendLine("        if (__result is App.Goals.Goal.GoalCall __gc && __gc.Action == null)");
        sb.AppendLine("            __gc.Action = __action;");
        sb.AppendLine("        return (T?)__result;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool __HasParam(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        return (__action?.Parameters?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false)");
        sb.AppendLine("            || (__action?.Defaults?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static string __FormatValue(object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value == null) return \"\";");
        sb.AppendLine("        if (value is string s) return s;");
        sb.AppendLine("        if (value is System.Collections.IDictionary || value is System.Collections.IList)");
        sb.AppendLine("            return System.Text.Json.JsonSerializer.Serialize(value);");
        sb.AppendLine("        return value.ToString() ?? \"\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private string? __StripPercent(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var data = __action?.Parameters?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        data ??= __action?.Defaults?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        if (data?.Value is string str)");
        sb.AppendLine("            return str.Trim('%');");
        sb.AppendLine("        return data?.Value?.ToString();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("}");

        return sb.ToString();
    }
}

internal class ActionClassInfo
{
    public string Namespace { get; }
    public string ClassName { get; }
    public string FullName { get; }
    public bool ImplementsIContext { get; }
    public bool ImplementsIChannel { get; }
    public bool ImplementsIAction { get; }
    public bool ImplementsIStep { get; }
    public bool ImplementsIStatic { get; }
    public List<ActionPropertyInfo> Properties { get; }

    public ActionClassInfo(string ns, string className, string fullName,
        bool implementsIContext, bool implementsIChannel, bool implementsIAction, bool implementsIStep,
        bool implementsIStatic, List<ActionPropertyInfo> properties)
    {
        Namespace = ns;
        ClassName = className;
        FullName = fullName;
        ImplementsIContext = implementsIContext;
        ImplementsIChannel = implementsIChannel;
        ImplementsIAction = implementsIAction;
        ImplementsIStep = implementsIStep;
        ImplementsIStatic = implementsIStatic;
        Properties = properties;
    }
}

internal class ActionPropertyInfo
{
    public string Name { get; }
    public string TypeName { get; }
    public bool IsNullable { get; }
    public bool IsValueType { get; }
    public string? DefaultValue { get; }
    public bool IsVariableName { get; }
    public bool IsAppResolvable { get; }
    public bool IsProvider { get; }
    public bool IsInitiated { get; }
    public bool IsNotNull { get; }
    public bool ImplementsIEvent { get; }

    public ActionPropertyInfo(string name, string typeName, bool isNullable,
        bool isValueType, string? defaultValue, bool isVariableName = false,
        bool isAppResolvable = false, bool isProvider = false,
        bool isInitiated = false, bool isNotNull = false, bool implementsIEvent = false)
    {
        Name = name;
        TypeName = typeName;
        IsNullable = isNullable;
        IsValueType = isValueType;
        DefaultValue = defaultValue;
        IsVariableName = isVariableName;
        IsAppResolvable = isAppResolvable;
        IsProvider = isProvider;
        IsInitiated = isInitiated;
        IsNotNull = isNotNull;
        ImplementsIEvent = implementsIEvent;
    }
}
