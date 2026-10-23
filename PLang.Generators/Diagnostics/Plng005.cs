using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PLang.Generators.Diagnostics;

/// <summary>
/// PLNG005 — a <c>choice&lt;T&gt;</c> draws on a class marked a choice. The types read the classes marked
/// <c>[Choice]</c> when an app is born, never every property of every class, so a choice over a class that isn't
/// marked is one whose options no app knows. Fires at every named <c>app.type.item.choice.@this&lt;T&gt;</c> whose
/// <c>T</c> is a class that doesn't carry <c>app.Attributes.ChoiceAttribute</c> — in plang and in any program built
/// with this generator. A generic <c>T</c> (the choice's own code) and an unbound <c>choice&lt;&gt;</c> are not a use.
/// A choice of a value family's kinds (<c>choice&lt;kind&lt;size&gt;&gt;</c>) draws on the family, which carries the mark.
/// </summary>
public static class Plng005
{
    public const string DiagnosticId = "PLNG005";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: DiagnosticId,
        title: "A choice draws on a class marked [Choice]",
        messageFormat: "{0}",
        category: "PLang.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The types read the classes marked [Choice] when an app is born. Mark the set a choice<T> draws on — the enum, the class naming its own options, the kind family's base — with [Choice].");

    public record struct Finding(string FilePath, int StartLine, int StartChar, int EndLine, int EndChar, string Message);

    /// <summary>Cheap prefilter: a generic name <c>@this&lt;X&gt;</c> with one written argument.</summary>
    public static bool IsCandidate(SyntaxNode node)
        => node is GenericNameSyntax { Identifier.ValueText: "this", TypeArgumentList.Arguments: { Count: 1 } arguments }
           && arguments[0] is not OmittedTypeArgumentSyntax;

    public static Finding? Analyze(GeneratorSyntaxContext ctx)
    {
        var filePath = ctx.Node.SyntaxTree.FilePath;
        if (filePath.Replace('\\', '/').Contains("/obj/") || filePath.EndsWith(".g.cs")) return null;
        if (ctx.SemanticModel.GetSymbolInfo(ctx.Node).Symbol is not INamedTypeSymbol { TypeArguments.Length: 1 } choice
            || choice.TypeArguments[0] is not INamedTypeSymbol set
            || choice.OriginalDefinition.ToDisplayString() != "app.type.item.choice.@this<T>")
            return null;
        if (Marked(set)) return null;
        // a choice of a value family's kinds (kind<size>) draws on the family, marked
        if (set.OriginalDefinition.ToDisplayString() == "app.type.kind.@this<T>"
            && set.TypeArguments[0] is INamedTypeSymbol family && Marked(family)) return null;

        var span = ctx.Node.GetLocation().GetLineSpan();
        return new Finding(filePath, span.StartLinePosition.Line, span.StartLinePosition.Character,
            span.EndLinePosition.Line, span.EndLinePosition.Character,
            $"choice<{set.Name}> draws on '{set.ToDisplayString()}', which isn't marked [Choice] — mark the set a choice draws on");
    }

    private static bool Marked(INamedTypeSymbol set)
        => set.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "app.Attributes.ChoiceAttribute");

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
