using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PLang.Generators.Diagnostics;

/// <summary>
/// PLNG002 — bans <c>System.IO.*</c> reaches and <c>Data&lt;string&gt;</c>
/// path-named properties under <c>PLang/app/**</c>.
///
/// Two scan surfaces:
/// <list type="number">
///   <item>Any qualified <c>System.IO.*</c> name expression (calls,
///   constructors, type references) outside the allowlist.</item>
///   <item>Action-handler partial properties of shape
///   <c>Data&lt;string&gt;</c> with a path-like name
///   (Path, PrPath, Source, Destination, Directory, Folder, FilePath).</item>
/// </list>
///
/// <para>Two narrow carve-outs, both visible at the use site:</para>
/// <list type="bullet">
///   <item><b><c>System.IO.Path.*</c></b> (pure name math) is allowed only
///   from <c>PLang/app/Utils/PathHelper.cs</c> — the single forwarder type.
///   Everywhere else routes through <c>app.Utils.PathHelper</c>.</item>
///   <item><b><c>System.IO.File</c> / <c>Directory</c> / <c>FileInfo</c> /
///   <c>DirectoryInfo</c> / <c>FileStream</c> / …</b> (actual IO) is allowed
///   only under <c>PLang/app/types/path/**</c> — the verb surface that gates
///   every disk touch through <c>AuthGate</c>.</item>
/// </list>
///
/// <para>Also exempts <c>PLang.Generators/**</c> (meta, not runtime),
/// <c>obj/</c> and <c>.g.cs</c> (generated). No other file-path exemptions
/// remain — every runtime <c>System.IO.*</c> reach lives in one of the two
/// carve-out sites above.</para>
/// </summary>
public static class Plng002
{
    public const string DiagnosticId = "PLNG002";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: DiagnosticId,
        title: "System.IO is banned in PLang action code (use app.type.path verbs)",
        messageFormat: "{0}. Use app.type.path.@this verbs (ReadText/WriteText/List/Stat/...) — they route through AuthGate.",
        category: "PLang.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Direct System.IO reaches bypass FilePath.AuthGate, the only thing stopping out-of-root reads/writes.");

    /// <summary>
    /// Path-like names that, when paired with <c>Data&lt;string&gt;</c> on an
    /// [Action] handler property, indicate a string-typed filesystem slot.
    /// </summary>
    private static readonly HashSet<string> PathLikeNames = new(System.StringComparer.Ordinal)
    {
        "Path", "PrPath", "Source", "Destination", "Directory", "Folder", "FilePath"
    };

    public record struct Finding(string FilePath, int StartLine, int StartChar, int EndLine, int EndChar, string Message);

    /// <summary>
    /// True for files under <c>PLang/app/**</c> that are scanned at all. Files
    /// returning false here are exempt from BOTH the <c>System.IO.Path.*</c>
    /// and <c>System.IO.File/Directory/…</c> bans — used for files that have
    /// no other recourse (PathHelper itself, the verb surface, bootstrap).
    /// The narrower split — <c>Path.*</c>-only vs. IO-only exemption — is in
    /// <see cref="IsPathTypeSurface"/> and <see cref="IsPathHelperFile"/>.
    /// </summary>
    public static bool IsScannedFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        // Normalize separators for cross-platform substring tests.
        var p = filePath!.Replace('\\', '/');
        if (!p.Contains("/PLang/app/") && !p.Contains("/PLang.Generators/")) return false;
        // Exempt generators — they're meta, not app code.
        if (p.Contains("/PLang.Generators/")) return false;
        // Exempt generated source.
        if (p.Contains("/obj/") || p.EndsWith(".g.cs")) return false;
        return true;
    }

    /// <summary>
    /// True for files under <c>PLang/app/types/path/**</c> — the gated verb
    /// surface that legitimately owns <c>System.IO.File/Directory/FileInfo/
    /// FileStream/...</c>. These files are NOT exempt for <c>System.IO.Path.*</c>
    /// (pure name math); they route through <see cref="app.Utils.PathHelper"/>
    /// like everyone else.
    /// </summary>
    private static bool IsPathTypeSurface(string normalizedPath)
        => normalizedPath.Contains("/PLang/app/type/path/");

    /// <summary>
    /// True for the single <c>PathHelper.cs</c> forwarder. PathHelper IS the
    /// allowed bridge to <c>System.IO.Path.*</c> — its body legitimately
    /// imports those members.
    /// </summary>
    private static bool IsPathHelperFile(string normalizedPath)
        => normalizedPath.EndsWith("/PLang/app/Utils/PathHelper.cs");

    /// <summary>
    /// Predicate for the SyntaxProvider — keeps both invocation-style and
    /// type-reference-style System.IO reaches.
    /// </summary>
    public static bool IsCandidateMemberAccess(SyntaxNode node)
    {
        if (node is not MemberAccessExpressionSyntax ma) return false;
        // Only the outermost member access in a chain — skip when this node is
        // the LHS of another MemberAccess (e.g. inner `System.IO.File` of
        // `System.IO.File.Exists`). Lets the outermost `.Exists` fire alone.
        if (ma.Parent is MemberAccessExpressionSyntax parent && parent.Expression == ma) return false;
        // Cheap text-level prefilter — must contain "System.IO" somewhere.
        var text = ma.ToString();
        return text.IndexOf("System.IO", System.StringComparison.Ordinal) >= 0
            || (ma.Expression is IdentifierNameSyntax id
                && (id.Identifier.Text == "File" || id.Identifier.Text == "Directory"
                    || id.Identifier.Text == "FileInfo" || id.Identifier.Text == "DirectoryInfo"));
    }

    /// <summary>
    /// Returns a finding when the member access resolves to a banned System.IO
    /// symbol. Returns null otherwise (compiles silently, no fire).
    /// </summary>
    public static Finding? Analyze(GeneratorSyntaxContext ctx)
    {
        var ma = (MemberAccessExpressionSyntax)ctx.Node;
        var loc = ma.GetLocation();
        var filePath = loc.SourceTree?.FilePath;
        if (!IsScannedFile(filePath)) return null;

        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(ma);
        var symbol = symbolInfo.Symbol;
        if (symbol == null) return null;

        // Walk up to the containing type — only System.IO types matter.
        var containingType = symbol switch
        {
            IMethodSymbol m => m.ContainingType,
            IPropertySymbol p => p.ContainingType,
            IFieldSymbol f => f.ContainingType,
            INamedTypeSymbol nt => nt,
            _ => null
        };
        if (containingType == null) return null;

        var ns = containingType.ContainingNamespace?.ToDisplayString();
        if (ns == null || (!ns.StartsWith("System.IO", System.StringComparison.Ordinal) && ns != "System.IO")) return null;

        // Two narrow carve-outs (see type-level XML doc):
        // - System.IO.Path.* (pure name math) → PathHelper.cs only.
        // - System.IO.File/Directory/FileInfo/... (actual IO) → path-types only.
        // Anything outside both carve-outs fires.
        var normalized = filePath!.Replace('\\', '/');
        var isPathMember = containingType.Name == "Path";
        if (isPathMember)
        {
            if (IsPathHelperFile(normalized)) return null;
        }
        else
        {
            if (IsPathTypeSurface(normalized)) return null;
        }

        var span = loc.GetLineSpan();
        var message = isPathMember
            ? $"'Path.{symbol.Name}' must route through app.Utils.PathHelper — direct System.IO.Path use is banned outside PathHelper"
            : $"'{containingType.Name}.{symbol.Name}' under System.IO bypasses FilePath.AuthGate";
        return new Finding(
            filePath!,
            span.StartLinePosition.Line, span.StartLinePosition.Character,
            span.EndLinePosition.Line, span.EndLinePosition.Character,
            message);
    }

    /// <summary>
    /// Predicate for the property-scan SyntaxProvider — only partial property
    /// declarations on classes with the [Action] attribute under scanned paths.
    /// </summary>
    public static bool IsCandidatePropertyDeclaration(SyntaxNode node)
    {
        if (node is not PropertyDeclarationSyntax prop) return false;
        if (!prop.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))) return false;
        // Cheap prefilter — property name must be in PathLikeNames.
        return PathLikeNames.Contains(prop.Identifier.Text);
    }

    public static Finding? AnalyzeProperty(GeneratorSyntaxContext ctx)
    {
        var prop = (PropertyDeclarationSyntax)ctx.Node;
        var filePath = prop.SyntaxTree.FilePath;
        if (!IsScannedFile(filePath)) return null;

        var declaring = ctx.SemanticModel.GetDeclaredSymbol(prop) as IPropertySymbol;
        if (declaring == null) return null;

        // Container must be an [Action] class.
        var cls = declaring.ContainingType;
        if (cls == null) return null;
        var isAction = cls.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "ActionAttribute"
            && a.AttributeClass.ContainingNamespace?.ToDisplayString() == "app.module");
        if (!isAction) return null;

        // Type must be Data<string>.
        if (declaring.Type is not INamedTypeSymbol nt) return null;
        if (nt.Name != "@this" && nt.Name != "this") return null;
        if (nt.ContainingNamespace?.ToDisplayString() != "app.data") return null;
        if (nt.TypeArguments.Length != 1) return null;
        if (nt.TypeArguments[0].SpecialType != SpecialType.System_String) return null;

        var loc = prop.Identifier.GetLocation();
        var span = loc.GetLineSpan();
        var msg = $"Action property '{declaring.Name}' should be Data<path>, not Data<string> — string paths bypass AuthGate";
        return new Finding(
            filePath,
            span.StartLinePosition.Line, span.StartLinePosition.Character,
            span.EndLinePosition.Line, span.EndLinePosition.Character,
            msg);
    }

    /// <summary>
    /// Builds the <see cref="Diagnostic"/> from a finding. Used by the
    /// generator entry point's <c>RegisterSourceOutput</c>.
    /// </summary>
    /// <summary>
    /// Wires both PLNG002 pipelines into the source generator's incremental
    /// pipeline — invocation-style System.IO reaches + Data&lt;string&gt; Path
    /// action properties.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var ioReaches = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => IsCandidateMemberAccess(n),
                transform: static (ctx, _) => Analyze(ctx))
            .Where(static f => f.HasValue)
            .Select(static (f, _) => f!.Value);
        context.RegisterSourceOutput(ioReaches, static (spc, f) => spc.ReportDiagnostic(ToDiagnostic(f)));

        var pathProps = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => IsCandidatePropertyDeclaration(n),
                transform: static (ctx, _) => AnalyzeProperty(ctx))
            .Where(static f => f.HasValue)
            .Select(static (f, _) => f!.Value);
        context.RegisterSourceOutput(pathProps, static (spc, f) => spc.ReportDiagnostic(ToDiagnostic(f)));
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
