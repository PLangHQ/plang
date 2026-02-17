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
                        defaultValue = arg.Value.ToString()!.ToLowerInvariant();
                }

                // Read [VariableName] attribute if present
                var isVariableName = prop.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "VariableNameAttribute");

                // Check if type has static Resolve(string, Engine) method
                var isEngineResolvable = prop.Type is INamedTypeSymbol namedType
                    && namedType.GetMembers("Resolve")
                        .OfType<IMethodSymbol>()
                        .Any(m => m.IsStatic
                            && m.Parameters.Length == 2
                            && m.Parameters[0].Type.SpecialType == SpecialType.System_String
                            && m.Parameters[1].Type.Name == "Engine");

                properties.Add(new ActionPropertyInfo(
                    prop.Name,
                    prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    prop.NullableAnnotation == NullableAnnotation.Annotated,
                    prop.Type.IsValueType,
                    defaultValue,
                    isVariableName,
                    isEngineResolvable));
            }
        }

        return new ActionClassInfo(
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            $"{classSymbol.ContainingNamespace}.{classSymbol.Name}",
            implementsIContext,
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

        // Resolution state
        sb.AppendLine("    private List<PLang.Runtime2.Engine.Memory.Data>? __parameters;");
        sb.AppendLine("    private PLang.Runtime2.Engine.Memory.MemoryStack? __memoryStack;");
        sb.AppendLine("    private PLang.Runtime2.Engine.Engine? __engine;");
        sb.AppendLine();

        // Partial property implementations
        foreach (var prop in info.Properties)
        {
            var backingField = $"__{prop.Name}_backing";
            var setFlag = $"__{prop.Name}_set";

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
            if (prop.IsEngineResolvable)
            {
                // Engine-resolvable types: resolve raw string then call Type.Resolve(string, Engine)
                var rawStr = $"__Resolve<string>(\"{paramName}\")";
                if (prop.DefaultValue != null)
                    rawStr = $"({rawStr} ?? {prop.DefaultValue})";
                resolveExpr = $"{prop.TypeName}.Resolve({rawStr}, __engine!)!";
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

        // CodeGeneratedExecuteAsync
        sb.AppendLine("    public async System.Threading.Tasks.Task<PLang.Runtime2.Engine.Memory.Data> CodeGeneratedExecuteAsync(");
        sb.AppendLine("        List<PLang.Runtime2.Engine.Memory.Data> parameters, PLang.Runtime2.Engine.Engine engine, PLang.Runtime2.Engine.Context.PLangContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        __parameters = parameters;");
        sb.AppendLine("        __memoryStack = context.MemoryStack;");
        sb.AppendLine("        __engine = engine;");
        sb.AppendLine("        var __step = context.Step;");
        sb.AppendLine("        var __callFrames = context.CallStack?.GetFrames() ?? (System.Collections.Generic.IReadOnlyList<PLang.Runtime2.Engine.CallFrame>)System.Array.Empty<PLang.Runtime2.Engine.CallFrame>();");

        if (info.ImplementsIContext)
        {
            sb.AppendLine("        Context = context;");
        }
        sb.AppendLine();

        // Validate non-nullable, non-defaulted properties
        foreach (var prop in info.Properties)
        {
            if (prop.IsNullable || prop.DefaultValue != null || prop.IsEngineResolvable)
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
        sb.AppendLine("        if (data?.Value is string str && str.Contains('%'))");
        sb.AppendLine("        {");
        sb.AppendLine("            var fullMatch = Regex.Match(str, @\"^%([^%]+)%$\");");
        sb.AppendLine("            if (fullMatch.Success)");
        sb.AppendLine("                return (T?)PLang.Runtime2.Engine.Utility.TypeMapping.ConvertTo(");
        sb.AppendLine("                    __memoryStack!.GetValue(fullMatch.Groups[1].Value), typeof(T));");
        sb.AppendLine("            var interpolated = Regex.Replace(str, @\"%([^%]+)%\",");
        sb.AppendLine("                m => __FormatValue(__memoryStack!.GetValue(m.Groups[1].Value)));");
        sb.AppendLine("            return (T?)PLang.Runtime2.Engine.Utility.TypeMapping.ConvertTo(interpolated, typeof(T));");
        sb.AppendLine("        }");
        sb.AppendLine("        return data != null");
        sb.AppendLine("            ? (T?)PLang.Runtime2.Engine.Utility.TypeMapping.ConvertTo(data.Value, typeof(T))");
        sb.AppendLine("            : default;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool __HasParam(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        return __parameters?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false;");
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
        sb.AppendLine("        if (data?.Value is string str)");
        sb.AppendLine("            return str.Trim('%');");
        sb.AppendLine("        return data?.Value?.ToString();");
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
    public List<ActionPropertyInfo> Properties { get; }

    public ActionClassInfo(string ns, string className, string fullName,
        bool implementsIContext, List<ActionPropertyInfo> properties)
    {
        Namespace = ns;
        ClassName = className;
        FullName = fullName;
        ImplementsIContext = implementsIContext;
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

    public ActionPropertyInfo(string name, string typeName, bool isNullable,
        bool isValueType, string? defaultValue, bool isVariableName = false,
        bool isEngineResolvable = false)
    {
        Name = name;
        TypeName = typeName;
        IsNullable = isNullable;
        IsValueType = isValueType;
        DefaultValue = defaultValue;
        IsVariableName = isVariableName;
        IsEngineResolvable = isEngineResolvable;
    }
}
