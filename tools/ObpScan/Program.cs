using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// OBP-shape scanner (syntax-only). Walks .cs under a root and reports the
// mechanical OBP smells that filename scans miss:
//   H1 collection-proxy verb  — a verb-named method that hands back a RAW
//                               collection (List/Dictionary/array/IEnumerable),
//                               not a Data<T>. (e.g. BuildTypeEntries, KnownTypes)
//   H2 Get-twin               — a method GetX where the same type also exposes a
//                               member named X (same thing exposed twice).
//   H3 exposed mutable coll.  — a public property/field typed List/Dictionary/HashSet
//                               (OBP smell #1: the collection should be its own type).
// Heuristics, not proofs — output is a review worklist. Detection here is meant
// to graduate into a DiagnosticAnalyzer for build-time.

string root = args.Length > 0 ? args[0] : "PLang/app";

string[] VERB_PREFIXES = {
    "Get","Build","Create","Make","Compute","Collect","Gather","Fetch","Load",
    "Find","Select","List","Discover","Walk","Scan","Extract","Resolve","Produce",
    "Generate","Assemble"
};
string[] RAW_COLLECTION_HEADS = {
    "List<","IList<","IEnumerable<","IReadOnlyList<","IReadOnlyCollection<","ICollection<",
    "Dictionary<","IDictionary<","IReadOnlyDictionary<","HashSet<","ISet<","ImmutableArray<",
    "ImmutableList<","Queue<","Stack<"
};
string[] MUTABLE_COLLECTION_HEADS = { "List<","Dictionary<","HashSet<","SortedDictionary<","Queue<","Stack<" };

bool IsCollectionType(string t)
{
    t = t.Trim();
    if (t.EndsWith("[]")) return true;
    return RAW_COLLECTION_HEADS.Any(h => t.StartsWith(h, StringComparison.Ordinal));
}
bool IsMutableCollectionType(string t)
{
    t = t.Trim();
    if (t.EndsWith("[]")) return false; // arrays are fixed-size; the smell is growable owned state
    return MUTABLE_COLLECTION_HEADS.Any(h => t.StartsWith(h, StringComparison.Ordinal));
}
// unwrap Task<>/ValueTask<>; returns inner type text. Data<...> stays as-is (NOT a collection).
string Unwrap(string t)
{
    t = t.Trim();
    foreach (var w in new[] { "Task<", "ValueTask<" })
        if (t.StartsWith(w, StringComparison.Ordinal) && t.EndsWith(">"))
            return Unwrap(t[w.Length..^1]);
    return t;
}
bool HasVerbPrefix(string name) =>
    VERB_PREFIXES.Any(v => name.Length > v.Length && name.StartsWith(v, StringComparison.Ordinal)
                           && char.IsUpper(name[v.Length]));

var h1 = new List<string>(); var h2 = new List<string>(); var h3 = new List<string>();
int files = 0;

foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
{
    if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
        path.Contains(".build")) continue;
    files++;
    var text = File.ReadAllText(path);
    var tree = CSharpSyntaxTree.ParseText(text);
    var rootNode = tree.GetRoot();

    foreach (var type in rootNode.DescendantNodes().OfType<TypeDeclarationSyntax>())
    {
        var typeName = type.Identifier.Text;
        var methods = type.Members.OfType<MethodDeclarationSyntax>().ToList();
        // member-name set for Get-twin detection (properties + non-Get methods)
        var memberNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in type.Members.OfType<PropertyDeclarationSyntax>()) memberNames.Add(p.Identifier.Text);
        foreach (var m in methods) memberNames.Add(m.Identifier.Text);

        foreach (var m in methods)
        {
            if (!m.Modifiers.Any(x => x.IsKind(SyntaxKind.PublicKeyword))) continue;
            var name = m.Identifier.Text;
            var ret = m.ReturnType.ToString();
            var inner = Unwrap(ret);
            int line = tree.GetLineSpan(m.Span).StartLinePosition.Line + 1;

            // H1: public method returning a collection (not Data<...>) — proxy candidate
            // regardless of verb/noun name. Annotate verb-prefixed (higher confidence).
            if (IsCollectionType(inner) && !inner.StartsWith("Data<", StringComparison.Ordinal)
                && !inner.StartsWith("data.", StringComparison.Ordinal))
                h1.Add($"{path}:{line}  {typeName}.{name}() -> {ret}{(HasVerbPrefix(name) ? "  [verb]" : "")}");

            // H2: Get-twin — GetX where member X exists
            if (name.StartsWith("Get", StringComparison.Ordinal) && name.Length > 3 && char.IsUpper(name[3]))
            {
                var twin = name[3..];
                if (memberNames.Contains(twin))
                    h2.Add($"{path}:{line}  {typeName}.{name}() twins member '{twin}'");
            }
        }

        // H3: public collection exposed as property or field. Includes read-only
        // exposures (IReadOnlyList/…) — annotate mutable (higher concern) vs readonly.
        foreach (var prop in type.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (!prop.Modifiers.Any(x => x.IsKind(SyntaxKind.PublicKeyword))) continue;
            var pt = prop.Type.ToString();
            if (IsCollectionType(pt))
            {
                int line = tree.GetLineSpan(prop.Span).StartLinePosition.Line + 1;
                h3.Add($"{path}:{line}  {typeName}.{prop.Identifier.Text} : {prop.Type}  [{(IsMutableCollectionType(pt) ? "mutable" : "readonly")}]");
            }
        }
        foreach (var fld in type.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!fld.Modifiers.Any(x => x.IsKind(SyntaxKind.PublicKeyword))) continue;
            var ft = fld.Declaration.Type.ToString();
            if (IsCollectionType(ft))
            {
                int line = tree.GetLineSpan(fld.Span).StartLinePosition.Line + 1;
                foreach (var v in fld.Declaration.Variables)
                    h3.Add($"{path}:{line}  {typeName}.{v.Identifier.Text} : {fld.Declaration.Type}  [{(IsMutableCollectionType(ft) ? "mutable" : "readonly")}]");
            }
        }
    }
}

void Dump(string title, List<string> items)
{
    Console.WriteLine($"\n=== {title}: {items.Count} ===");
    foreach (var i in items.OrderBy(x => x)) Console.WriteLine("  " + i);
}
Console.WriteLine($"scanned {files} files under {root}");
Dump("H1 collection-proxy verb (verb-named method returns a raw collection, not Data<T>)", h1);
Dump("H2 Get-twin (GetX alongside member X)", h2);
Dump("H3 public mutable collection exposed (smell #1)", h3);
