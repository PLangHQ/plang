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
/// <para>Path-based filtering: only fires under <c>PLang/app/**</c>, exempts
/// <c>PLang/app/types/path/**</c> (the verb surface owns <c>System.IO.*</c>),
/// and exempts <c>PLang.Generators/**</c>. Stage 1 lands in warning mode;
/// Stage 6 will flip the severity to error.</para>
///
/// <para>Allowlist: <c>System.IO.Path.DirectorySeparatorChar</c> /
/// <c>AltDirectorySeparatorChar</c> — separator constants, not IO.</para>
/// </summary>
public static class Plng002
{
    public const string DiagnosticId = "PLNG002";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: DiagnosticId,
        title: "System.IO is banned in PLang action code (use app.types.path verbs)",
        messageFormat: "{0}. Use app.types.path.@this verbs (ReadText/WriteText/List/Stat/...) — they route through AuthGate.",
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

    private static readonly HashSet<string> AllowedSystemIoPathMembers = new(System.StringComparer.Ordinal)
    {
        // Separator constants — not IO.
        "DirectorySeparatorChar", "AltDirectorySeparatorChar",
        "PathSeparator", "VolumeSeparatorChar",
        // Pure string-arithmetic methods — they NEVER touch the filesystem.
        // The architect plan flags these because they signal "this code is
        // operating on raw string paths" — but the actual gate concern is
        // System.IO.File/Directory and the rooted-path resolvers. Pure
        // name math doesn't bypass any gate.
        "Combine", "GetDirectoryName", "GetFileName",
        "GetFileNameWithoutExtension", "GetExtension", "GetRelativePath",
        "ChangeExtension", "GetInvalidFileNameChars",
        "GetInvalidPathChars", "HasExtension",
        "IsPathRooted", "IsPathFullyQualified", "GetFullPath",
        "TrimEndingDirectorySeparator", "GetPathRoot", "EndsInDirectorySeparator",
        "Join"
    };

    public record struct Finding(string FilePath, int StartLine, int StartChar, int EndLine, int EndChar, string Message);

    /// <summary>
    /// True for files under <c>PLang/app/**</c> that are NOT under
    /// <c>PLang/app/types/path/**</c> and NOT generated.
    /// </summary>
    public static bool IsScannedFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        // Normalize separators for cross-platform substring tests.
        var p = filePath!.Replace('\\', '/');
        if (!p.Contains("/PLang/app/") && !p.Contains("/PLang.Generators/")) return false;
        // Exempt the path-types namespace — the verb surface legitimately owns System.IO.
        if (p.Contains("/PLang/app/types/path/")) return false;
        // Exempt the markdown teaching loader — bootstrap-time discovery of
        // static, repo-shipped teaching .md files. Not runtime action code; the
        // gate would force converting a pure-sync utility to async-everywhere
        // for no security benefit.
        if (p.EndsWith("/PLang/app/modules/MarkdownTeaching.cs")) return false;
        // Exempt generators — they're meta, not app code.
        if (p.Contains("/PLang.Generators/")) return false;
        // Exempt generated source.
        if (p.Contains("/obj/") || p.EndsWith(".g.cs")) return false;
        return true;
    }

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

        // Allowlist: System.IO.Path separator constants AND pure string-arithmetic
        // methods (Path.Combine / GetDirectoryName / ...). The actual gate
        // concern is System.IO.File/Directory/FileInfo and the rooted-path
        // resolvers, not name math on strings.
        if (containingType.Name == "Path" && AllowedSystemIoPathMembers.Contains(symbol.Name))
            return null;

        var span = loc.GetLineSpan();
        var message = $"'{containingType.Name}.{symbol.Name}' under System.IO bypasses FilePath.AuthGate";
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
            && a.AttributeClass.ContainingNamespace?.ToDisplayString() == "app.modules");
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
