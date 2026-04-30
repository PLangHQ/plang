using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PLang.Generators.Emission.Property;
using DataProperty = PLang.Generators.Emission.Property.Data.@this;
using ProviderProperty = PLang.Generators.Emission.Property.Provider.@this;
using LegacyProperty = PLang.Generators.Emission.Property.Legacy.@this;
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
    /// v4 contract: every action property must be Data&lt;T&gt;, plain Data, [Provider]-attributed,
    /// or [VariableName]-attributed string (the latter is a transitional carve-out for handlers
    /// that work with variable identity rather than value — variable.set, list.*).
    /// </summary>
    internal static readonly DiagnosticDescriptor RawScalarPropertyDescriptor = new(
        id: "PLNG001",
        title: "Action property must be Data<T> or [Provider]",
        messageFormat: "Property '{0}' on action '{1}' must be Data<T>, [Provider], or [VariableName] string. Raw scalars are not permitted.",
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
            && a.AttributeClass.ContainingNamespace.ToDisplayString() == "App.modules");
        if (!hasActionAttr) return null;

        bool ImplementsModule(string ifaceName) =>
            classSymbol.AllInterfaces.Any(i =>
                i.Name == ifaceName
                && i.ContainingNamespace.ToDisplayString() == "App.modules");

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
            // [Provider], or [VariableName] string is rejected per v4 contract.
            if (!IsValidActionProperty(prop))
            {
                var loc = prop.Locations.FirstOrDefault();
                diagnostics.Add(new DiagnosticInfo(
                    prop.Name, classSymbol.Name,
                    loc?.SourceTree?.FilePath ?? string.Empty,
                    loc?.GetLineSpan().StartLinePosition.Line ?? 0,
                    loc?.GetLineSpan().StartLinePosition.Character ?? 0));
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
            new EquatableArray<RawScalarValidation>(ScanRawScalarValidations(classSymbol)),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    /// <summary>
    /// v4 contract gate: action property is valid iff it is Data, Data&lt;T&gt;,
    /// [Provider]-attributed, or [VariableName]-attributed string. Anything else
    /// reports a build-time diagnostic.
    /// </summary>
    private static bool IsValidActionProperty(IPropertySymbol prop)
    {
        if (prop.GetAttributes().Any(a => a.AttributeClass?.Name == "ProviderAttribute"))
            return true;

        if (prop.GetAttributes().Any(a => a.AttributeClass?.Name == "VariableNameAttribute"))
            return true;

        // Data<T> or plain Data. Roslyn's IPropertySymbol.Name returns the bare identifier
        // without the leading @ — so we only need to check "this".
        if (prop.Type is INamedTypeSymbol dt
            && dt.OriginalDefinition.Name == "this"
            && dt.OriginalDefinition.ContainingNamespace.ToDisplayString() == "App.Data")
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
        // [Provider] takes priority — these aren't parameter-sourced.
        var isProvider = prop.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "ProviderAttribute");
        if (isProvider)
        {
            var typeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            // ImplementsIContext is used for the engine-resolution expression — read off the parent class.
            var parentImplsCtx = prop.ContainingType.AllInterfaces.Any(i =>
                i.Name == "IContext" && i.ContainingNamespace.ToDisplayString() == "App.modules");
            return (new ProviderProperty(prop.Name, typeName, parentImplsCtx), false);
        }

        // [Default] literal expression
        string? defaultValue = ReadDefaultValueExpression(prop);

        // [VariableName] flag (legacy)
        var isVariableName = prop.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "VariableNameAttribute");

        // App-resolvable: type has static Resolve(string, Context.@this)
        var isAppResolvable = prop.Type is INamedTypeSymbol named
            && named.GetMembers("Resolve")
                .OfType<IMethodSymbol>()
                .Any(m => m.IsStatic
                    && m.Parameters.Length == 2
                    && m.Parameters[0].Type.SpecialType == SpecialType.System_String
                    && (m.Parameters[1].Type.ContainingNamespace?.ToDisplayString() == "App"
                        || m.Parameters[1].Type.ContainingNamespace?.ToDisplayString() == "App.Actor.Context"));

        // Strip Nullable<T> wrap for IEvent detection
        var rawType = prop.Type;
        if (rawType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            rawType = nullable.TypeArguments[0];
        var implementsIEvent = rawType is INamedTypeSymbol evt
            && evt.AllInterfaces.Any(i =>
                i.Name == "IEvent"
                && i.ContainingNamespace.ToDisplayString() == "App.modules");

        // Detect Data<T> and plain Data
        var typeNameStr = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isNullable = prop.NullableAnnotation == NullableAnnotation.Annotated;

        var namedType = prop.Type as INamedTypeSymbol;
        var isAppDataThis = namedType?.OriginalDefinition.Name == "this"
            && namedType.OriginalDefinition.ContainingNamespace.ToDisplayString() == "App.Data";
        var isDataWrapped = isAppDataThis && namedType!.IsGenericType;
        var isPlainData = isAppDataThis && !namedType!.IsGenericType;

        if (isDataWrapped || isPlainData)
        {
            string? innerType = null;
            if (isDataWrapped)
            {
                var ltIdx = typeNameStr.IndexOf('<');
                innerType = ltIdx >= 0 ? typeNameStr.Substring(ltIdx + 1, typeNameStr.Length - ltIdx - 2) : "object";
            }
            return (new DataProperty(prop.Name, typeNameStr, isNullable, isPlainData, innerType, defaultValue), implementsIEvent);
        }

        // Everything else falls into legacy scalar emission (raw string / int / etc.)
        return (new LegacyProperty(
            prop.Name, typeNameStr, isNullable, prop.Type.IsValueType,
            isAppResolvable, isVariableName, defaultValue), implementsIEvent);
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

    /// <summary>List of properties that need the legacy raw-scalar non-null validation in ExecuteAsync.</summary>
    private static List<RawScalarValidation> ScanRawScalarValidations(INamedTypeSymbol classSymbol)
    {
        var result = new List<RawScalarValidation>();
        foreach (var prop in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (!prop.IsPartialDefinition) continue;
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.IsStatic) continue;
            if (prop.GetAttributes().Any(a => a.AttributeClass?.Name == "ProviderAttribute")) continue;
            if (prop.GetAttributes().Any(a => a.AttributeClass?.Name == "DefaultAttribute")) continue;
            if (prop.NullableAnnotation == NullableAnnotation.Annotated) continue;
            if (prop.Type.IsValueType) continue;

            // Skip Data / Data<T>
            var typeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (typeName.Contains("App.Data.@this")) continue;

            // Skip App-resolvable types — they have their own resolve path
            if (prop.Type is INamedTypeSymbol named
                && named.GetMembers("Resolve").OfType<IMethodSymbol>()
                    .Any(m => m.IsStatic && m.Parameters.Length == 2
                              && m.Parameters[0].Type.SpecialType == SpecialType.System_String))
                continue;

            var isString = typeName == "string" || typeName == "global::System.String";
            result.Add(new RawScalarValidation(prop.Name, isString));
        }
        return result;
    }
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
    EquatableArray<RawScalarValidation> RawScalarValidations,
    EquatableArray<DiagnosticInfo> Diagnostics);

public sealed record RawScalarValidation(string PropertyName, bool IsString);

/// <summary>
/// Value-equal diagnostic info for the IIncrementalGenerator cache —
/// captures property + class names plus location so the source-output stage
/// can build a Roslyn Diagnostic.
/// </summary>
public sealed record DiagnosticInfo(
    string PropertyName, string ClassName, string FilePath, int Line, int Character);
