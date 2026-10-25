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
        // a public field is scanned for its shape (a mutable collection exposed); other fields are the type's own state
        if (m is IFieldSymbol { DeclaredAccessibility: Accessibility.Public }) return true;
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

    /// <summary>Where the member is declared: <c>file:line</c>, the file relative to <paramref name="root"/>.</summary>
    public string Where(string root)
    {
        if (_symbol.Locations.FirstOrDefault(l => l.IsInSource) is not { } location) return "";
        var span = location.GetLineSpan();
        return $"{Path.GetRelativePath(root, span.Path)}:{span.StartLinePosition.Line + 1}";
    }

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

    /// <summary>The member's shape verdict — what its signature shows about a collection, the smells a name can't:
    /// a verb-named method handing back a raw collection (the collection should be the API, its own type), a
    /// <c>GetX</c> beside a member <c>X</c> (one thing exposed twice), a public mutable collection (its add and evict
    /// discipline lives in other files). Empty when none.</summary>
    public string Shape
    {
        get
        {
            if (IsContract) return "";
            var flags = new List<string>();
            if (_symbol is IMethodSymbol { DeclaredAccessibility: Accessibility.Public } method
                && Name.HasVerb && IsCollection(Unwrap(method.ReturnType)))
                flags.Add("**RAW-COLLECTION**");
            if (_symbol.Name is { Length: > 3 } name && name.StartsWith("Get", StringComparison.Ordinal)
                && char.IsUpper(name[3]) && _symbol.ContainingType.GetMembers(name[3..]).Any())
                flags.Add($"**GET-TWIN** ({name[3..]})");
            if (_symbol.DeclaredAccessibility == Accessibility.Public && IsMutableCollection(_symbol switch
                {
                    IPropertySymbol p => p.Type,
                    IFieldSymbol f => f.Type,
                    _ => null,
                }))
                flags.Add("**MUTABLE-COLLECTION**");
            return string.Join(", ", flags);
        }
    }

    // a Task<T> / ValueTask<T> is what it completes with
    private static ITypeSymbol Unwrap(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: "Task" or "ValueTask", TypeArguments.Length: 1 } task
           && task.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"
            ? Unwrap(task.TypeArguments[0])
            : type;

    // an array, or a generic collection from System.Collections (List, IEnumerable, IReadOnlyList, Dictionary, …)
    private static bool IsCollection(ITypeSymbol type)
        => type is IArrayTypeSymbol
           || type is INamedTypeSymbol { IsGenericType: true } named
              && named.ContainingNamespace?.ToDisplayString() is { } ns && ns.StartsWith("System.Collections", StringComparison.Ordinal);

    // a collection anyone holding it can grow: List, Dictionary, HashSet, SortedDictionary, Queue, Stack
    private static bool IsMutableCollection(ITypeSymbol? type)
        => type is INamedTypeSymbol { IsGenericType: true } named
           && named.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
           && named.Name is "List" or "Dictionary" or "HashSet" or "SortedDictionary" or "Queue" or "Stack";

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
