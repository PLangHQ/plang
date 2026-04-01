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
            && a.AttributeClass.ContainingNamespace.ToDisplayString() == "PLang.Runtime2.modules");
        if (!hasActionAttr) return null;

        // Check if it implements IContext
        var implementsIContext = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IContext"
            && i.ContainingNamespace.ToDisplayString() == "PLang.Runtime2.modules");

        // Check if it implements IChannel
        var implementsIChannel = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IChannel"
            && i.ContainingNamespace.ToDisplayString() == "PLang.Runtime2.modules");

        // Check if it implements IAction
        var implementsIAction = classSymbol.AllInterfaces.Any(i =>
            i.Name == "IAction"
            && i.ContainingNamespace.ToDisplayString() == "PLang.Runtime2.modules");

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

                // Check if type has static Resolve(string, PLangContext) method
                var isEngineResolvable = prop.Type is INamedTypeSymbol namedType
                    && namedType.GetMembers("Resolve")
                        .OfType<IMethodSymbol>()
                        .Any(m => m.IsStatic
                            && m.Parameters.Length == 2
                            && m.Parameters[0].Type.SpecialType == SpecialType.System_String
                            && (m.Parameters[1].Type.ContainingNamespace?.ToDisplayString() == "PLang.Runtime2.Engine"
                                || m.Parameters[1].Type.ContainingNamespace?.ToDisplayString() == "PLang.Runtime2.Engine.Context"));

                // Check if property type is Data (pass through without unwrapping .Value)
                var rawType = prop.Type;
                if (rawType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
                    rawType = nullableType.TypeArguments[0];
                var isDataType = rawType.Name == "Data"
                    && rawType.ContainingNamespace?.ToDisplayString() == "PLang.Runtime2.Engine.Memory";

                properties.Add(new ActionPropertyInfo(
                    prop.Name,
                    prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    prop.NullableAnnotation == NullableAnnotation.Annotated,
                    prop.Type.IsValueType,
                    defaultValue,
                    isVariableName,
                    isEngineResolvable,
                    isProvider,
                    isDataType,
                    isInitiated,
                    isNotNull));
            }
        }

        return new ActionClassInfo(
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            $"{classSymbol.ContainingNamespace}.{classSymbol.Name}",
            implementsIContext,
            implementsIChannel,
            implementsIAction,
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
        sb.AppendLine($"partial class {info.ClassName} : PLang.Runtime2.modules.ICodeGenerated");
        sb.AppendLine("{");

        // IContext auto-provision
        if (info.ImplementsIContext)
        {
            sb.AppendLine("    public PLang.Runtime2.Engine.Context.PLangContext Context { get; set; } = null!;");
            sb.AppendLine();
        }

        // IChannel auto-provision
        if (info.ImplementsIChannel)
        {
            sb.AppendLine("    public PLang.Runtime2.Engine.Channels.@this Channels { get; set; } = null!;");
            sb.AppendLine();
        }

        // IAction auto-provision
        if (info.ImplementsIAction)
        {
            sb.AppendLine("    public PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;");
            sb.AppendLine();
        }

        // Resolution state
        sb.AppendLine("    private List<PLang.Runtime2.Engine.Memory.Data>? __parameters;");
        sb.AppendLine("    private List<PLang.Runtime2.Engine.Memory.Data>? __defaults;");
        sb.AppendLine("    private PLang.Runtime2.Engine.Memory.MemoryStack? __memoryStack;");
        sb.AppendLine("    private PLang.Runtime2.Engine.@this? __engine;");
        sb.AppendLine("    private PLang.Runtime2.Engine.Memory.Data? __resolutionError;");
        sb.AppendLine();

        // Partial property implementations
        foreach (var prop in info.Properties)
        {
            var backingField = $"__{prop.Name}_backing";
            var setFlag = $"__{prop.Name}_set";

            // [Provider] properties — resolved lazily from engine.Providers
            // Works both via ExecuteAsync (__engine) and direct test usage (Context.Engine)
            if (prop.IsProvider)
            {
                var engineExpr = info.ImplementsIContext ? "__engine ?? Context?.Engine" : "__engine";
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
            if (prop.IsDataType)
            {
                // Data-typed properties: pass the Data object through, don't unwrap .Value
                resolveExpr = $"__ResolveData(\"{paramName}\")";
            }
            else if (prop.IsEngineResolvable)
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
            sb.AppendLine($"        get => {setFlag} ? {backingField}! : {resolveExpr};");
            sb.AppendLine($"        init {{ {backingField} = value; {setFlag} = true; }}");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // ExecuteAsync
        sb.AppendLine("    private PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this? __action;");
        sb.AppendLine();
        sb.AppendLine("    public async System.Threading.Tasks.Task<PLang.Runtime2.Engine.Memory.Data> ExecuteAsync(");
        sb.AppendLine("        PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this action, PLang.Runtime2.Engine.@this engine, PLang.Runtime2.Engine.Context.PLangContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        __action = action;");
        sb.AppendLine("        __parameters = action.Parameters;");
        sb.AppendLine("        __defaults = action.Defaults;");
        sb.AppendLine("        __memoryStack = context.MemoryStack;");
        sb.AppendLine("        __engine = engine;");
        sb.AppendLine("        __resolutionError = null;");
        sb.AppendLine("        var __step = action.Step;");
        sb.AppendLine("        var __callFrames = context.CallStack?.GetFrames() ?? (System.Collections.Generic.IReadOnlyList<PLang.Runtime2.Engine.CallStack.CallFrame>)System.Array.Empty<PLang.Runtime2.Engine.CallStack.CallFrame>();");

        if (info.ImplementsIContext)
        {
            sb.AppendLine("        Context = context;");
        }
        if (info.ImplementsIChannel)
        {
            sb.AppendLine("        Channels = (context.Actor ?? engine.User).Channels;");
        }
        if (info.ImplementsIAction)
        {
            sb.AppendLine("        Action = action;");
        }
        sb.AppendLine();

        // Resolve [Provider] properties from engine.Providers
        foreach (var prop in info.Properties)
        {
            if (!prop.IsProvider) continue;
            var backingField = $"__{prop.Name}_backing";
            sb.AppendLine($"        var __{prop.Name}_result = engine.Providers.Get<{prop.TypeName}>();");
            sb.AppendLine($"        if (!__{prop.Name}_result.Success) return __{prop.Name}_result;");
            sb.AppendLine($"        {backingField} = __{prop.Name}_result.Value!;");
            sb.AppendLine();
        }

        // Validate non-nullable, non-defaulted properties
        foreach (var prop in info.Properties)
        {
            if (prop.IsNullable || prop.DefaultValue != null || prop.IsEngineResolvable || prop.IsProvider)
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
                sb.AppendLine($"            return PLang.Runtime2.Engine.Memory.Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(");
                sb.AppendLine($"                \"'{prop.Name.ToLowerInvariant()}' is required\", __step, __callFrames, \"MissingParameter\", 400));");
            }
        }

        // Validate [IsInitiated] and [IsNotNull] attributes
        foreach (var prop in info.Properties)
        {
            if (prop.IsInitiated || prop.IsNotNull)
            {
                if (prop.IsDataType)
                {
                    if (prop.IsNotNull)
                    {
                        // IsNotNull implies IsInitiated — check both
                        sb.AppendLine($"        if (!{prop.Name}.IsInitialized || {prop.Name}.Value == null)");
                        sb.AppendLine($"            return PLang.Runtime2.Engine.Memory.Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(");
                        sb.AppendLine($"                \"'{prop.Name.ToLowerInvariant()}' must have a value\", __step, __callFrames, \"ValueRequired\", 400));");
                    }
                    else
                    {
                        // IsInitiated only
                        sb.AppendLine($"        if (!{prop.Name}.IsInitialized)");
                        sb.AppendLine($"            return PLang.Runtime2.Engine.Memory.Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(");
                        sb.AppendLine($"                \"'{prop.Name.ToLowerInvariant()}' must be provided\", __step, __callFrames, \"ParameterRequired\", 400));");
                    }
                }
                else if (prop.IsNotNull && !prop.IsValueType)
                {
                    sb.AppendLine($"        if ({prop.Name} == null)");
                    sb.AppendLine($"            return PLang.Runtime2.Engine.Memory.Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(");
                    sb.AppendLine($"                \"'{prop.Name.ToLowerInvariant()}' must have a value\", __step, __callFrames, \"ValueRequired\", 400));");
                }
            }
        }

        sb.AppendLine("        if (__resolutionError != null) return __resolutionError;");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            return await Run();");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (System.Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return PLang.Runtime2.Engine.Memory.Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(");
        sb.AppendLine("                ex.Message, __step, __callFrames, \"ServiceError\", 400) { Exception = ex });");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // __Resolve<T> helper
        sb.AppendLine("    private T? __Resolve<T>(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var data = __parameters?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        data ??= __defaults?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        if (data?.Value is string str && str.Contains('%'))");
        sb.AppendLine("        {");
        sb.AppendLine("            var fullMatch = Regex.Match(str, @\"^%([^%]+)%$\");");
        sb.AppendLine("            if (fullMatch.Success)");
        sb.AppendLine("            {");
        sb.AppendLine("                var __resolved = __memoryStack!.Get(fullMatch.Groups[1].Value);");
        sb.AppendLine("                if (__resolved != null && !__resolved.Success)");
        sb.AppendLine("                {");
        sb.AppendLine("                    __resolutionError = __resolved;");
        sb.AppendLine("                    return default;");
        sb.AppendLine("                }");
        sb.AppendLine("                return __TryConvert<T>(__resolved?.Value, name);");
        sb.AppendLine("            }");
        sb.AppendLine("            var __interpolationError = false;");
        sb.AppendLine("            var interpolated = Regex.Replace(str, @\"%([^%]+)%\", m => {");
        sb.AppendLine("                var __r = __memoryStack!.Get(m.Groups[1].Value);");
        sb.AppendLine("                if (__r != null && !__r.Success)");
        sb.AppendLine("                {");
        sb.AppendLine("                    __resolutionError = __r;");
        sb.AppendLine("                    __interpolationError = true;");
        sb.AppendLine("                    return \"\";");
        sb.AppendLine("                }");
        sb.AppendLine("                return __FormatValue(__r?.Value);");
        sb.AppendLine("            });");
        sb.AppendLine("            if (__interpolationError) return default;");
        sb.AppendLine("            return __TryConvert<T>(interpolated, name);");
        sb.AppendLine("        }");
        sb.AppendLine("        if (data?.Value is System.Collections.IList || data?.Value is System.Collections.IDictionary)");
        sb.AppendLine("        {");
        sb.AppendLine("            var __deepResolved = __memoryStack!.ResolveDeep(data.Value);");
        sb.AppendLine("            return __TryConvert<T>(__deepResolved, name);");
        sb.AppendLine("        }");
        sb.AppendLine("        return data != null");
        sb.AppendLine("            ? __TryConvert<T>(data.Value, name)");
        sb.AppendLine("            : default;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private T? __TryConvert<T>(object? value, string paramName)");
        sb.AppendLine("    {");
        sb.AppendLine("        var (__result, __error) = PLang.Runtime2.Engine.Utility.TypeMapping.TryConvertTo(value, typeof(T));");
        sb.AppendLine("        if (__error != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            __resolutionError = PLang.Runtime2.Engine.Memory.Data.FromError(");
        sb.AppendLine("                new PLang.Runtime2.Engine.Errors.ActionError(");
        sb.AppendLine("                    $\"Parameter '{paramName}': {__error.Message}\",");
        sb.AppendLine("                    \"ConversionError\", __error.StatusCode)");
        sb.AppendLine("                    { FixSuggestion = __error.FixSuggestion });");
        sb.AppendLine("            return default;");
        sb.AppendLine("        }");
        sb.AppendLine("        // Stamp GoalCalls with the action so goal resolution can navigate action → step → goal");
        sb.AppendLine("        if (__result is PLang.Runtime2.Engine.Goals.Goal.GoalCall __gc && __gc.Action == null)");
        sb.AppendLine("            __gc.Action = __action;");
        sb.AppendLine("        return (T?)__result;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool __HasParam(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        return (__parameters?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false)");
        sb.AppendLine("            || (__defaults?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false);");
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
        sb.AppendLine("        var data = __parameters?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        data ??= __defaults?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        if (data?.Value is string str)");
        sb.AppendLine("            return str.Trim('%');");
        sb.AppendLine("        return data?.Value?.ToString();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private PLang.Runtime2.Engine.Memory.Data? __ResolveData(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var data = __parameters?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        data ??= __defaults?.FirstOrDefault(");
        sb.AppendLine("            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine("        if (data?.Value is string str && str.Contains('%'))");
        sb.AppendLine("        {");
        sb.AppendLine("            var fullMatch = Regex.Match(str, @\"^%([^%]+)%$\");");
        sb.AppendLine("            if (fullMatch.Success)");
        sb.AppendLine("            {");
        sb.AppendLine("                var __resolved = __memoryStack!.Get(fullMatch.Groups[1].Value);");
        sb.AppendLine("                // Data properties pass through regardless of Success — the Data IS the value");
        sb.AppendLine("                return __resolved;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        return data;");
        sb.AppendLine("    }");

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
    public List<ActionPropertyInfo> Properties { get; }

    public ActionClassInfo(string ns, string className, string fullName,
        bool implementsIContext, bool implementsIChannel, bool implementsIAction, List<ActionPropertyInfo> properties)
    {
        Namespace = ns;
        ClassName = className;
        FullName = fullName;
        ImplementsIContext = implementsIContext;
        ImplementsIChannel = implementsIChannel;
        ImplementsIAction = implementsIAction;
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
    public bool IsEngineResolvable { get; }
    public bool IsProvider { get; }
    public bool IsDataType { get; }
    public bool IsInitiated { get; }
    public bool IsNotNull { get; }

    public ActionPropertyInfo(string name, string typeName, bool isNullable,
        bool isValueType, string? defaultValue, bool isVariableName = false,
        bool isEngineResolvable = false, bool isProvider = false, bool isDataType = false,
        bool isInitiated = false, bool isNotNull = false)
    {
        Name = name;
        TypeName = typeName;
        IsNullable = isNullable;
        IsValueType = isValueType;
        DefaultValue = defaultValue;
        IsVariableName = isVariableName;
        IsEngineResolvable = isEngineResolvable;
        IsProvider = isProvider;
        IsDataType = isDataType;
        IsInitiated = isInitiated;
        IsNotNull = isNotNull;
    }
}
