using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PLang.Generators.Diagnostics;

/// <summary>
/// PLNG004 — bans the System.Text.Json SERIALIZER layer under <c>PLang/app/**</c>.
/// A value crosses the wire through its own <c>Write(IWriter)</c> + a
/// <c>serializer/Reader.cs</c> (format-agnostic); the STJ serializer layer —
/// <c>JsonSerializer</c>, <c>JsonSerializerOptions</c>, <c>JsonConverter</c>,
/// <c>JsonConverterFactory</c> — reflects a graph and bypasses that discipline.
///
/// <para>The TOKENIZER is untouched: <c>Utf8JsonReader</c>/<c>Utf8JsonWriter</c>,
/// <c>JsonDocument</c>, <c>JsonElement</c>, <c>JsonNode</c>, <c>JsonException</c>,
/// <c>JsonWriterOptions</c> — the byte codec <c>json.Reader</c>/<c>json.Writer</c>
/// are built on — is not banned. Only the four reflection/policy types above fire,
/// resolved by their <c>System.Text.Json[.Serialization]</c> namespace.</para>
///
/// <para>Severity: WARNING while the separate-concern render sites (diagnostics,
/// spec/formal, error/debug rendering, text conversion) still use it — the warning
/// list IS that "never format in C#" worklist. Flips to error once the worklist is
/// clean, the PLNG002/PLNG003 trajectory. No file-path allowlist: the Data-wire path
/// is already clean, and a new reach on it should surface, not hide behind an entry.</para>
/// </summary>
public static class Plng004
{
    public const string DiagnosticId = "PLNG004";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: DiagnosticId,
        title: "System.Text.Json serializer layer is banned (use Write(IWriter) + serializer/Reader.cs)",
        messageFormat: "{0}",
        category: "PLang.Generators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "JsonSerializer / JsonSerializerOptions / JsonConverter / JsonConverterFactory are the STJ serializer layer — a value crosses the wire through its own Write(IWriter) and serializer/Reader.cs. The Utf8/JsonDocument/JsonElement tokenizer is fine; the serializer layer is not.");

    /// <summary>The four serializer-layer type names. The tokenizer is deliberately absent.</summary>
    private static readonly HashSet<string> Banned = new(System.StringComparer.Ordinal)
    {
        "JsonSerializer", "JsonSerializerOptions", "JsonConverter", "JsonConverterFactory"
    };

    public record struct Finding(string FilePath, int StartLine, int StartChar, int EndLine, int EndChar, string Message);

    /// <summary>Files scanned at all — under <c>PLang/app/**</c>, minus generated / generator meta.</summary>
    public static bool IsScannedFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var p = filePath!.Replace('\\', '/');
        if (!p.Contains("/PLang/app/")) return false;
        if (p.Contains("/PLang.Generators/")) return false;
        if (p.Contains("/obj/") || p.EndsWith(".g.cs")) return false;
        return true;
    }

    /// <summary>
    /// Cheap prefilter — any simple name (identifier or generic) whose text is a banned
    /// type name. Catches every use shape: <c>JsonSerializer.Serialize</c> (member-access
    /// LHS), <c>new JsonSerializerOptions()</c> (object creation), <c>: JsonConverter&lt;T&gt;</c>
    /// (base type), a <c>JsonSerializerOptions</c> parameter (type reference).
    /// </summary>
    public static bool IsCandidate(SyntaxNode node) => node switch
    {
        GenericNameSyntax g => Banned.Contains(g.Identifier.Text),
        IdentifierNameSyntax id => Banned.Contains(id.Identifier.Text),
        _ => false,
    };

    public static Finding? Analyze(GeneratorSyntaxContext ctx)
    {
        var filePath = ctx.Node.SyntaxTree.FilePath;
        if (!IsScannedFile(filePath)) return null;

        var info = ctx.SemanticModel.GetSymbolInfo(ctx.Node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        // The name may resolve to the type itself (type ref / base type / ctor target) or,
        // for `JsonSerializer.Serialize`, to a member whose containing type is the banned one.
        var type = symbol switch
        {
            INamedTypeSymbol t => t,
            IMethodSymbol m => m.ContainingType,
            IPropertySymbol p => p.ContainingType,
            IFieldSymbol f => f.ContainingType,
            _ => null,
        };
        if (type == null || !Banned.Contains(type.Name)) return null;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (!ns.StartsWith("System.Text.Json", System.StringComparison.Ordinal)) return null;

        var loc = ctx.Node.GetLocation();
        var span = loc.GetLineSpan();
        var message = $"'{type.Name}' is the System.Text.Json serializer layer — cross the wire via Write(IWriter) + serializer/Reader.cs (the Utf8/JsonDocument tokenizer is fine)";
        return new Finding(filePath, span.StartLinePosition.Line, span.StartLinePosition.Character,
            span.EndLinePosition.Line, span.EndLinePosition.Character, message);
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
