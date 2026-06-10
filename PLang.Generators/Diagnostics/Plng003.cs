using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PLang.Generators.Diagnostics;

/// <summary>
/// PLNG003 — the public-surface typing gate (Stage 7 of the typed value
/// model): a PUBLIC instance member of an <c>app.type.item.@this</c> subtype
/// that returns raw CLR (<c>string</c>/<c>int</c>/<c>long</c>/<c>bool</c>/
/// <c>byte[]</c>/<c>Dictionary</c>/<c>List</c>/…) is flagged — the
/// <c>!</c> plane a plang developer reaches must answer in PLang values
/// (<c>text</c>, <c>number</c>, <c>@bool</c>, <c>list</c>, …).
///
/// <para>Scope is the line that keeps it sane:</para>
/// <list type="bullet">
///   <item>Only PUBLIC INSTANCE members — private/internal/protected C# is
///   untouched; engine-internal plumbing goes <c>internal</c> rather than
///   exempted.</item>
///   <item>Only declared members (not inherited), on classes assignable to
///   <c>app.type.item.@this</c>.</item>
///   <item>Statics are out of scope — the catalog conventions
///   (<c>Example</c>/<c>Shape</c>/<c>Description</c>/<c>Kinds</c>, hooks like
///   <c>Compare</c>/<c>Convert</c>/<c>FromWire</c>, implicit operators) are
///   reflection/registry surfaces, not the developer-navigable plane.</item>
///   <item><c>object</c> overrides (<c>ToString</c>/<c>Equals</c>/
///   <c>GetHashCode</c>) and interface implementations whose signature is
///   pinned elsewhere (<c>Write(IWriter)</c>, <c>LoadAsync</c>) don't fire —
///   only members RETURNING a flagged raw type do, and those signatures
///   return void/Task.</item>
/// </list>
///
/// <para>Severity: WARNING while the surface converts (the warning list IS
/// the worklist); flips to error once clean — the PLNG002 trajectory.</para>
/// </summary>
public static class Plng003
{
    public const string DiagnosticId = "PLNG003";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: DiagnosticId,
        title: "Public value-type surface must return PLang types",
        messageFormat: "{0}",
        category: "PLang.Generators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A public instance member of an app.type.item.@this subtype returning raw CLR leaks the untyped plane; return the PLang equivalent (text/number/@bool/list/...) or make the member internal.");

    public record struct Finding(string FilePath, int StartLine, int StartChar, int EndLine, int EndChar, string Message);

    public static bool IsCandidate(SyntaxNode node)
    {
        // Public instance properties/methods with a non-void return — cheap
        // syntactic prefilter; the semantic walk does the real scoping.
        return node switch
        {
            PropertyDeclarationSyntax p =>
                p.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
                && !p.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                && !p.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)),
            MethodDeclarationSyntax m =>
                m.Modifiers.Any(mm => mm.IsKind(SyntaxKind.PublicKeyword))
                && !m.Modifiers.Any(mm => mm.IsKind(SyntaxKind.StaticKeyword))
                && !m.Modifiers.Any(mm => mm.IsKind(SyntaxKind.OverrideKeyword)),
            _ => false,
        };
    }

    public static Finding? Analyze(GeneratorSyntaxContext ctx)
    {
        var filePath = ctx.Node.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(filePath)) return null;
        var p = filePath.Replace('\\', '/');
        if (!p.Contains("/PLang/app/")) return null;
        if (p.Contains("/obj/") || p.EndsWith(".g.cs")) return null;

        ISymbol? declared = ctx.Node switch
        {
            PropertyDeclarationSyntax prop => ctx.SemanticModel.GetDeclaredSymbol(prop),
            MethodDeclarationSyntax method => ctx.SemanticModel.GetDeclaredSymbol(method),
            _ => null,
        };
        if (declared == null) return null;
        if (declared.DeclaredAccessibility != Accessibility.Public || declared.IsStatic || declared.IsOverride) return null;
        // Explicit interface implementations carry pinned signatures.
        if (declared is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 }) return null;
        if (declared is IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 }) return null;

        var containing = declared.ContainingType;
        if (containing == null || !DerivesFromItem(containing)) return null;

        var returnType = declared switch
        {
            IPropertySymbol ps => ps.Type,
            IMethodSymbol ms => UnwrapTask(ms.ReturnType),
            _ => null,
        };
        if (returnType == null || !IsRawClr(returnType)) return null;

        var loc = ctx.Node switch
        {
            PropertyDeclarationSyntax prop => prop.Identifier.GetLocation(),
            MethodDeclarationSyntax method => method.Identifier.GetLocation(),
            _ => ctx.Node.GetLocation(),
        };
        var span = loc.GetLineSpan();
        var owner = containing.Name == "@this" || containing.Name == "this"
            ? containing.ContainingNamespace?.ToDisplayString() ?? containing.Name
            : containing.Name;
        var message = $"Public member '{owner}.{declared.Name}' returns raw CLR '{returnType.ToDisplayString()}' — return the PLang equivalent (text/number/@bool/list/...) or make it internal";
        return new Finding(filePath, span.StartLinePosition.Line, span.StartLinePosition.Character,
            span.EndLinePosition.Line, span.EndLinePosition.Character, message);
    }

    private static bool DerivesFromItem(INamedTypeSymbol type)
    {
        for (var b = type.BaseType; b != null; b = b.BaseType)
        {
            if ((b.Name == "@this" || b.Name == "this")
                && b.ContainingNamespace?.ToDisplayString() == "app.type.item")
                return true;
        }
        return false;
    }

    private static ITypeSymbol UnwrapTask(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { TypeArguments.Length: 1 } nt
            && nt.Name is "Task" or "ValueTask")
            return nt.TypeArguments[0];
        return type;
    }

    private static bool IsRawClr(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_DateTime:
                return true;
        }
        if (type is IArrayTypeSymbol arr)
            return arr.ElementType.SpecialType == SpecialType.System_Byte;
        if (type is INamedTypeSymbol named)
        {
            if (named.Name is "Nullable" && named.TypeArguments.Length == 1)
                return IsRawClr(named.TypeArguments[0]);
            var ns = named.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns == "System" && named.Name is "DateTimeOffset" or "TimeSpan" or "Guid")
                return true;
            if (ns.StartsWith("System.Collections", System.StringComparison.Ordinal)
                && named.Name is "Dictionary" or "List" or "IDictionary" or "IList"
                    or "IEnumerable" or "IReadOnlyList" or "IReadOnlyDictionary" or "HashSet")
            {
                // A collection OF PLang values is typed enough for this gate —
                // only raw-element collections fire.
                return named.TypeArguments.Length == 0
                    || named.TypeArguments.Any(IsRawClr);
            }
        }
        return false;
    }

    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var findings = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => IsCandidate(n),
                transform: static (ctx, _) => Analyze(ctx))
            .Where(static f => f.HasValue)
            .Select(static (f, _) => f!.Value);
        context.RegisterSourceOutput(findings, static (spc, f) => spc.ReportDiagnostic(ToDiagnostic(f)));
    }

    public static Diagnostic ToDiagnostic(Finding f)
    {
        var location = Location.Create(
            f.FilePath,
            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(0, 0),
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                new Microsoft.CodeAnalysis.Text.LinePosition(f.StartLine, f.StartChar),
                new Microsoft.CodeAnalysis.Text.LinePosition(f.EndLine, f.EndChar)));
        return Diagnostic.Create(Descriptor, location, f.Message);
    }
}
