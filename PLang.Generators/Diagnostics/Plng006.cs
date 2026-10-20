using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PLang.Generators.Diagnostics;

/// <summary>
/// PLNG006 — a value is written through its output kind. A plang value written into a writer goes through the
/// writer's kind (<c>writer.Kind.pair.Output(value, writer, mode, context)</c>), which has the value's pair when that
/// kind shows it its own way (a list in formal, a type in plang). Calling <c>x.Output(writer, …)</c> on a value
/// skips its pair. Fires at every call of <c>app.type.item.@this.Output</c> (or an override) whose receiver is a
/// value other than <c>this</c>/<c>base</c> — except in the door itself (<c>app.type.kind.pair.list.@this</c>) and in
/// a pair writing the value it was handed (an <c>IOutput.Output</c>'s own value parameter).
/// </summary>
public static class Plng006
{
    public const string DiagnosticId = "PLNG006";

    private const string Item = "app.type.item.@this";
    private const string Door = "app.type.kind.pair.list.@this";
    private const string Pair = "app.type.format.IOutput";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: DiagnosticId,
        title: "A value is written through its output kind",
        messageFormat: "{0}",
        category: "PLang.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A value written into a writer goes through the writer's kind: writer.Kind.pair.Output(value, writer, mode, context). Calling value.Output(writer, …) directly skips the pair its output kind has for it.");

    public record struct Finding(string FilePath, int StartLine, int StartChar, int EndLine, int EndChar, string Message);

    /// <summary>Cheap prefilter: a call of a member named <c>Output</c> on something.</summary>
    public static bool IsCandidate(SyntaxNode node)
        => node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Output" } };

    public static Finding? Analyze(GeneratorSyntaxContext ctx)
    {
        var filePath = ctx.Node.SyntaxTree.FilePath;
        if (filePath.Replace('\\', '/').Contains("/obj/") || filePath.EndsWith(".g.cs")) return null;
        var call = (InvocationExpressionSyntax)ctx.Node;
        var access = (MemberAccessExpressionSyntax)call.Expression;
        if (access.Expression is ThisExpressionSyntax or BaseExpressionSyntax) return null;
        if (ctx.SemanticModel.GetSymbolInfo(call).Symbol is not IMethodSymbol method || !IsItemOutput(method)) return null;

        var writing = Writing(ctx.SemanticModel.GetEnclosingSymbol(call.SpanStart));
        if (writing?.ContainingType?.ToDisplayString() == Door) return null;
        // a pair writes the value it was handed: that is the value's own write
        if (writing is { Name: "Output", Parameters.Length: > 0 }
            && writing.ContainingType.AllInterfaces.Any(i => i.ToDisplayString() == Pair)
            && ctx.SemanticModel.GetSymbolInfo(access.Expression).Symbol is IParameterSymbol handed
            && SymbolEqualityComparer.Default.Equals(handed, writing.Parameters[0]))
            return null;

        var span = call.GetLocation().GetLineSpan();
        return new Finding(filePath, span.StartLinePosition.Line, span.StartLinePosition.Character,
            span.EndLinePosition.Line, span.EndLinePosition.Character,
            $"'{access.Expression}' is written around its output kind — write it through the writer's kind: writer.Kind.pair.Output({access.Expression}, writer, mode, context)");
    }

    // an item's Output, or an override of it
    private static bool IsItemOutput(IMethodSymbol method)
    {
        for (var m = method; m != null; m = m.OverriddenMethod)
            if (m.Name == "Output" && m.ContainingType?.ToDisplayString() == Item) return true;
        return false;
    }

    // the member the call is written in — past any lambda or local function inside it
    private static IMethodSymbol? Writing(ISymbol? symbol)
    {
        for (var s = symbol; s != null; s = s.ContainingSymbol)
            if (s is IMethodSymbol { MethodKind: not (MethodKind.AnonymousFunction or MethodKind.LocalFunction) } m) return m;
        return null;
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
