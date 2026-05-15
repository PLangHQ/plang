using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PLang.Generators.Emission.Property;
using DataProperty = PLang.Generators.Emission.Property.Data.@this;
using CodeProperty = PLang.Generators.Emission.Property.Code.@this;
using PropertyBase = PLang.Generators.Emission.Property.@this;

namespace PLang.Generators.Discovery;

/// <summary>
/// Roslyn discovery — predicate, GetActionClassInfo, ActionClassInfo type,
/// and the property factory that picks the right ActionProperty leaf per declared property.
/// </summary>
public static class @this
{
    public static bool IsActionPartialClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl) return false;
        if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword)) return false;

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

    /// <summary>
    /// Diagnostic descriptor for raw-scalar partial properties on [Action] handlers.
    /// Post-v5 contract: every action property must be <c>Data&lt;T&gt;</c>, plain <c>Data</c>,
    /// or <c>[Code]</c>-attributed. Variable-name slots use <c>Data&lt;Variable&gt;</c>
    /// (App.Variables.Variable) — the former <c>[VariableName] string</c> carve-out is gone.
    /// </summary>
    internal static readonly DiagnosticDescriptor RawScalarPropertyDescriptor = new(
        id: "PLNG001",
        title: "Action property must be Data<T> or [Code]",
        messageFormat: "Property '{0}' on action '{1}' must be Data<T> or [Code] T. Raw scalars are not permitted.",
        category: "PLang.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static ActionClassInfo? GetActionClassInfo(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null) return null;

        var hasActionAttr = classSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "ActionAttribute"
            && a.AttributeClass.ContainingNamespace.ToDisplayString() == "app.modules");
        if (!hasActionAttr) return null;

        bool ImplementsModule(string ifaceName) =>
            classSymbol.AllInterfaces.Any(i =>
                i.Name == ifaceName
                && i.ContainingNamespace.ToDisplayString() == "app.modules");

        var implementsIContext = ImplementsModule("IContext");
        var implementsIChannel = ImplementsModule("IChannel");
        var implementsIAction = ImplementsModule("IAction");
        var implementsIStep = ImplementsModule("IStep");
        var implementsIStatic = ImplementsModule("IStatic");

        var properties = new List<PropertyBase>();
        var ievents = new List<string>();
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop
                || !prop.IsPartialDefinition
                || prop.DeclaredAccessibility != Accessibility.Public
                || prop.IsStatic)
                continue;

            var (actionProp, implementsIEvent) = BuildProperty(prop);
            if (actionProp != null) properties.Add(actionProp);
            if (implementsIEvent) ievents.Add(prop.Name);

            // Raw-scalar diagnostic: anything that doesn't qualify as Data<T>, plain Data,
            // or [Code] T is rejected. Variable-name slots are Data<Variable>.
            if (!IsValidActionProperty(prop))
            {
                var loc = prop.Locations.FirstOrDefault();
                var span = loc?.GetLineSpan();
                diagnostics.Add(new DiagnosticInfo(
                    prop.Name, classSymbol.Name,
                    loc?.SourceTree?.FilePath ?? string.Empty,
                    span?.StartLinePosition.Line ?? 0,
                    span?.StartLinePosition.Character ?? 0,
                    span?.EndLinePosition.Line ?? 0,
                    span?.EndLinePosition.Character ?? 0));
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
            new EquatableArray<PropertyBase>(properties),
            new EquatableArray<string>(ievents),
            HasIsNotNull(classSymbol),
            new EquatableArray<string>(ScanIsNotNullProperties(classSymbol)),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    /// <summary>
    /// Post-v5 contract gate: action property is valid iff it is plain <c>Data</c>,
    /// <c>Data&lt;T&gt;</c>, or <c>[Code] T</c>. Anything else (raw scalars,
    /// the deleted <c>[VariableName] string</c>) reports a build-time diagnostic.
    /// </summary>
    private static bool IsValidActionProperty(IPropertySymbol prop)
    {
        if (prop.GetAttributes().Any(a => a.AttributeClass?.Name == "CodeAttribute"))
            return true;

        // Data<T> or plain Data. Roslyn's IPropertySymbol.Name returns the bare identifier
        // without the leading @ — so we only need to check "this".
        if (prop.Type is INamedTypeSymbol dt
            && dt.OriginalDefinition.Name == "this"
            && dt.OriginalDefinition.ContainingNamespace.ToDisplayString() == "app.Data")
            return true;

        return false;
    }

    /// <summary>
    /// Picks the right ActionProperty leaf for a Roslyn IPropertySymbol.
    /// Returns (property, implementsIEvent) — the latter is needed by Action emission
    /// for context.Event wiring.
    /// </summary>
    private static (PropertyBase? Prop, bool ImplementsIEvent) BuildProperty(IPropertySymbol prop)
    {
        // [Code] takes priority — these aren't parameter-sourced.
        var isCode = prop.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "CodeAttribute");
        if (isCode)
        {
            var typeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            // ImplementsIContext is used for the engine-resolution expression — read off the parent class.
            var parentImplsCtx = prop.ContainingType.AllInterfaces.Any(i =>
                i.Name == "IContext" && i.ContainingNamespace.ToDisplayString() == "app.modules");
            return (new CodeProperty(prop.Name, typeName, parentImplsCtx), false);
        }

        // [Default] literal expression
        string? defaultValue = ReadDefaultValueExpression(prop);

        // [Sensitive] — masks PrValue/FinalValue in __SnapshotParams. Same convention as
        // SensitivePropertyFilter applies during JSON serialization. The snapshot path
        // feeds Error.Params, which prints to logs/CI artefacts, so secrets must be masked.
        var isSensitive = prop.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "SensitiveAttribute");

        // Strip Nullable<T> wrap for IEvent detection
        var rawType = prop.Type;
        if (rawType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            rawType = nullable.TypeArguments[0];
        var implementsIEvent = rawType is INamedTypeSymbol evt
            && evt.AllInterfaces.Any(i =>
                i.Name == "IEvent"
                && i.ContainingNamespace.ToDisplayString() == "app.modules");

        // Detect Data<T> and plain Data
        var typeNameStr = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isNullable = prop.NullableAnnotation == NullableAnnotation.Annotated;

        var namedType = prop.Type as INamedTypeSymbol;
        var isAppDataThis = namedType?.OriginalDefinition.Name == "this"
            && namedType.OriginalDefinition.ContainingNamespace.ToDisplayString() == "app.Data";
        var isDataWrapped = isAppDataThis && namedType!.IsGenericType;
        var isPlainData = isAppDataThis && !namedType!.IsGenericType;

        if (isDataWrapped || isPlainData)
        {
            string? innerType = null;
            var isRawNameResolvable = false;
            if (isDataWrapped)
            {
                var ltIdx = typeNameStr.IndexOf('<');
                innerType = ltIdx >= 0 ? typeNameStr.Substring(ltIdx + 1, typeNameStr.Length - ltIdx - 2) : "object";

                // Detect whether T : IRawNameResolvable. Slots whose T is a name-like type
                // (Variable today) carry the missing-parameter contract: a missing/null slot
                // must surface a MissingRequiredParameter ServiceError, not bubble through
                // as an NRE when the handler reads .Value.
                if (namedType!.TypeArguments.Length > 0
                    && namedType.TypeArguments[0] is INamedTypeSymbol innerNamed
                    && innerNamed.AllInterfaces.Any(i =>
                        i.Name == "IRawNameResolvable"
                        && i.ContainingNamespace.ToDisplayString() == "app.Variables"))
                {
                    isRawNameResolvable = true;
                }
            }
            return (new DataProperty(prop.Name, typeNameStr, isNullable, isPlainData, innerType, defaultValue, isSensitive, isRawNameResolvable), implementsIEvent);
        }

        // No leaf matches — PLNG001 has already flagged this property; emit nothing
        // so the build error surfaces without a follow-on NRE elsewhere.
        return (null, implementsIEvent);
    }

    private static string? ReadDefaultValueExpression(IPropertySymbol prop)
    {
        var defaultAttr = prop.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Name == "DefaultAttribute");
        if (defaultAttr == null || defaultAttr.ConstructorArguments.Length == 0) return null;

        var arg = defaultAttr.ConstructorArguments[0];
        if (arg.Value is string strVal)
            return "\"" + strVal.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        if (arg.Value == null) return null;
        // For enum and primitive defaults, return the bare literal value. Emission wraps
        // it in ({InnerType})… so a separate cast here would produce ({T})({T})value.
        if (prop.Type.TypeKind == TypeKind.Enum)
            return arg.Value.ToString();
        return arg.Value.ToString()!.ToLowerInvariant();
    }

    private static bool HasIsNotNull(INamedTypeSymbol classSymbol) =>
        classSymbol.GetMembers().OfType<IPropertySymbol>()
            .Any(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "IsNotNullAttribute")
                      && !p.Type.IsValueType);

    /// <summary>List of property names that carry [IsNotNull] AND aren't value types.</summary>
    private static List<string> ScanIsNotNullProperties(INamedTypeSymbol classSymbol) =>
        classSymbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "IsNotNullAttribute")
                        && !p.Type.IsValueType)
            .Select(p => p.Name)
            .ToList();

}

/// <summary>
/// Per-handler metadata produced by Discovery and consumed by the Action emitter.
/// Record with EquatableArray collections: every field has structural equality, so
/// the IIncrementalGenerator cache hits when two semantically identical inputs come
/// through successive compilations.
/// </summary>
public sealed record ActionClassInfo(
    string Namespace,
    string ClassName,
    string FullName,
    bool ImplementsIContext,
    bool ImplementsIChannel,
    bool ImplementsIAction,
    bool ImplementsIStep,
    bool ImplementsIStatic,
    EquatableArray<PropertyBase> Properties,
    EquatableArray<string> IEventPropertyNames,
    bool HasAnyIsNotNull,
    EquatableArray<string> IsNotNullProperties,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>
/// Value-equal diagnostic info for the IIncrementalGenerator cache —
/// captures property + class names plus the full identifier location span so the
/// source-output stage can rebuild a Roslyn Diagnostic that underlines the offending
/// identifier (rather than a synthetic one-character span).
/// </summary>
public sealed record DiagnosticInfo(
    string PropertyName, string ClassName, string FilePath,
    int StartLine, int StartCharacter, int EndLine, int EndCharacter);
