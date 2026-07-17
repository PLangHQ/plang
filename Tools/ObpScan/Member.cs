using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace ObpScan;

/// <summary>One scanned member — owns every question the scan asks about it: how long it is,
/// whether its name smells, who calls it, and whether it sits on the wrong owner. The scan does
/// not compute these ABOUT the member; the member answers them. (This class is itself the OBP
/// principle the tool enforces — behaviour lives on the element.)</summary>
public sealed class Member
{
    private readonly ISymbol _symbol;
    private readonly string _declaringNamespace;

    public Member(ISymbol symbol, string declaringNamespace)
    {
        _symbol = symbol;
        _declaringNamespace = declaringNamespace;
    }

    public static bool IsScannable(ISymbol m)
    {
        if (m.IsImplicitlyDeclared) return false;
        if (m is not (IMethodSymbol or IPropertySymbol)) return false;
        if (m is IMethodSymbol ms && ms.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet
            or MethodKind.Constructor or MethodKind.StaticConstructor) return false;
        return true;
    }

    public MemberName Name => new(_symbol.Name, Returns(SpecialType.System_Boolean), IsContract);

    /// <summary>A member whose name AND presence are dictated by a base contract — an override of a
    /// base member, or an interface implementation. The scan judges neither its name nor its
    /// ownership: the framework named it and the polymorphic machinery calls it.</summary>
    private bool IsContract
        => _symbol.IsOverride || HasExplicitImpl || ImplementsInterface;

    private bool HasExplicitImpl => _symbol switch
    {
        IMethodSymbol m => m.ExplicitInterfaceImplementations.Any(),
        IPropertySymbol p => p.ExplicitInterfaceImplementations.Any(),
        _ => false,
    };

    /// <summary>Source lines the declaration spans — the length fingerprint.</summary>
    public int Lines
    {
        get
        {
            var r = _symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (r is null) return 0;
            var s = r.GetSyntax().GetLocation().GetLineSpan();
            return s.EndLinePosition.Line - s.StartLinePosition.Line + 1;
        }
    }

    /// <summary>The namespaces that reference this member — the ownership evidence, resolved from the
    /// call graph (not grep), so `step.Actions.Foo()` and `actions.Foo()` are told apart.</summary>
    public async Task<IReadOnlyList<string>> Callers(Solution solution)
    {
        var result = new List<string>();
        foreach (var found in await SymbolFinder.FindReferencesAsync(_symbol, solution))
            foreach (var loc in found.Locations)
            {
                if (loc.Location.SourceTree is not { } tree) continue;
                var node = (await tree.GetRootAsync()).FindNode(loc.Location.SourceSpan);
                var owner = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                if (owner is null) continue;
                var model = await loc.Document.GetSemanticModelAsync();
                if (model?.GetDeclaredSymbol(owner)?.ContainingNamespace?.ToDisplayString() is { } ns)
                    result.Add(ns);
            }
        return result;
    }

    /// <summary>The member's own ownership verdict: MISPLACED when every caller lives outside this
    /// member's declaring concept and the member is not an interface obligation.</summary>
    public Ownership Judge(IReadOnlyList<string> callers)
    {
        if (callers.Count == 0 || IsContract) return new Ownership("own", null);
        bool allForeign = callers.All(n => !Concept.Same(n, _declaringNamespace));
        if (allForeign)
            return new Ownership("callers ∉ declaring ns → **MISPLACED**",
                                 callers.Distinct().Select(Concept.Short).First());
        return new Ownership("own", null);
    }

    private bool ImplementsInterface
        => _symbol.ContainingType.AllInterfaces.Any(i => i.GetMembers(_symbol.Name).Any());

    private bool Returns(SpecialType t) => _symbol switch
    {
        IMethodSymbol m => m.ReturnType.SpecialType == t,
        IPropertySymbol p => p.Type.SpecialType == t,
        _ => false,
    };
}

/// <summary>A member's ownership verdict + the foreign concept its callers point at (null when owned).</summary>
public sealed record Ownership(string Verdict, string? ForeignConcept);
