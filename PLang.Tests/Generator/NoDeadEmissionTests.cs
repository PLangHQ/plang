using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PLang.Tests.Generator;

// Contract tests for "no dead emission in generated handlers".
//
// The v1 regression set:
//  - __variables — declared, assigned, never read.
//  - __paramData + ParamData() — __paramData was read in-file (by ParamData()), but
//    ParamData() itself had zero callers anywhere in the source tree. Both dead.
//    Critically, ParamData() was `protected`, not `public`.
//
// codeanalyzer/v2 (#40) flagged that the v2 incarnation of this fixture was
// structurally incapable of catching either regression: its `reads<=0` heuristic
// gave reads=1 for the __variables shape (decl + 1 LHS = 2 occurrences − 1 LHS = 1)
// and reads=2 for the __paramData shape (decl + 2 LHS + 1 internal read = 4 − 2 = 2).
//
// tester/v3 #1 flagged that v3's Pattern B was anchored to `^\s*public\s+...` —
// so `protected ParamData()`, the actual regression shape, would still slip past.
// Plus comments and string literals in the concatenated caller-source false-greened
// dead methods whose name happened to appear in any docstring or emission template.
//
// v4 splits the contract into:
//   Pattern A — every declared private field has at least one read in the same file
//     (heuristic: reads = total_occurrences − assignments − decl_line_occurrences).
//     Catches the __variables shape.
//   Pattern B — every public OR protected method declared in a generated handler
//     has at least one caller across the source tree, where caller-source is
//     stripped of comments and string literals before scanning. Catches the
//     __paramData/ParamData() shape (regardless of access modifier).
//   Convention — every declared private field uses the `__` prefix (#44).
//
// Pattern A is pinned by 5 Heuristic_* tests that drive `HasReadOf` on synthetic
// source. Pattern B is pinned by 3 Heuristic_* tests that drive `IsOrphanMethod`
// directly, plus 2 stripping tests that pin `StripCommentsAndStrings`. So both
// patterns are regression-tested independently of whether the live generated tree
// happens to be clean.

public class NoDeadEmissionTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "PLang.sln"))
                                && !Directory.Exists(Path.Combine(dir, "PLang.Generators")))
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return dir!;
        }
    }

    private static string GeneratedDir => Path.Combine(RepoRoot,
        "PLang.Tests", "obj", "Debug", "net10.0",
        "generated", "PLang.Generators", "PLang.Generators.this");

    // Names whose call site is the framework (interface dispatch, reflection-style wiring).
    // ExecuteAsync / SnapshotParams are required by ICodeGenerated and called by App.Run.
    private static readonly HashSet<string> _publicMethodCallerExemptions = new()
    {
        "ExecuteAsync",
        "SnapshotParams",
    };

    [Test]
    public async Task NoGeneratedHandlerDeclaresAnUnreadPrivateField()
    {
        var files = Directory.Exists(GeneratedDir)
            ? Directory.GetFiles(GeneratedDir, "*.Action.g.cs")
            : Array.Empty<string>();
        await Assert.That(files).IsNotEmpty();

        var dead = new List<string>();
        foreach (var path in files)
        {
            var src = File.ReadAllText(path);
            foreach (Match m in PrivateFieldDecl.Matches(src))
            {
                var fieldName = m.Groups[1].Value;
                if (!HasReadOf(src, fieldName))
                    dead.Add($"{Path.GetFileName(path)}:{fieldName}");
            }
        }
        await Assert.That(dead).IsEmpty();
    }

    [Test]
    public async Task EveryGeneratedPrivateFieldUsesDoubleUnderscorePrefix()
    {
        // Pin the convention. Every private field the generator emits must start with `__`.
        // Without this assertion, a future generator change could drop the prefix and the
        // dead-field test (which scans `__\w+`-prefixed names) would silently miss those.
        var files = Directory.Exists(GeneratedDir)
            ? Directory.GetFiles(GeneratedDir, "*.Action.g.cs")
            : Array.Empty<string>();
        await Assert.That(files).IsNotEmpty();

        var violations = new List<string>();
        foreach (var path in files)
        {
            var src = File.ReadAllText(path);
            foreach (Match m in PrivateFieldDecl.Matches(src))
            {
                var fieldName = m.Groups[1].Value;
                if (!fieldName.StartsWith("__"))
                    violations.Add($"{Path.GetFileName(path)}:{fieldName}");
            }
        }
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task NoGeneratedHandlerExposesUnusedPublicOrProtectedMethod()
    {
        // Pattern B: cross-file scan for orphan public OR protected methods.
        // A generated handler that emits `protected Foo() { return __field; }` with
        // no callers anywhere is the __paramData/ParamData() regression shape exactly:
        // __field looks alive in-file, Foo() is callable in subclasses, but nothing
        // anywhere in the tree actually invokes Foo() — so the whole chain is dead.
        // Widened from public-only in v4 (tester #1) — the original ParamData() was
        // protected, not public.
        var files = Directory.Exists(GeneratedDir)
            ? Directory.GetFiles(GeneratedDir, "*.Action.g.cs")
            : Array.Empty<string>();
        await Assert.That(files).IsNotEmpty();

        var allCallableSources = LoadAllCallableSources();

        var orphans = new List<string>();
        foreach (var path in files)
        {
            var src = File.ReadAllText(path);
            foreach (Match m in PublicOrProtectedMethodDecl.Matches(src))
            {
                var name = m.Groups[1].Value;
                if (IsOrphanMethod(name, allCallableSources, _publicMethodCallerExemptions))
                    orphans.Add($"{Path.GetFileName(path)}:{name}");
            }
        }
        await Assert.That(orphans).IsEmpty();
    }

    private static readonly Regex PrivateFieldDecl = new(
        @"^\s*private\s+(?:static\s+|readonly\s+|const\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*[;=]",
        RegexOptions.Multiline);

    private static readonly Regex PublicOrProtectedMethodDecl = new(
        @"^\s*(?:public|protected)\s+(?:async\s+|partial\s+|static\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*\(",
        RegexOptions.Multiline);

    internal static bool IsOrphanMethod(
        string methodName,
        string allCallableSources,
        ISet<string> exemptions)
    {
        // Pure-string helper so synthetic Heuristic_* tests can drive it directly,
        // mirroring the shape Pattern A's HasReadOf already provides.
        if (exemptions.Contains(methodName)) return false;
        var callerPattern = new Regex(@"\b" + Regex.Escape(methodName) + @"\s*\(");
        return !callerPattern.IsMatch(allCallableSources);
    }

    internal static bool HasReadOf(string src, string fieldName)
    {
        // total_occurrences − assignments − decl_line_occurrences
        // Subtracts the declaration line so a decl-only field (count == 1) registers as 0 reads.
        // Uses `=(?!=)` to exclude `==` / `!=` comparisons from the assignment count.
        var totalOccurrences = Regex.Matches(src, @"\b" + Regex.Escape(fieldName) + @"\b").Count;
        var assignments = Regex.Matches(src,
            @"\b" + Regex.Escape(fieldName) + @"\b\s*(\??\[[^\]]*\])?\s*=(?!=)").Count;
        var declOccurrences = Regex.Matches(src,
            @"^\s*private\s+(?:static\s+|readonly\s+|const\s+)*[\w\.<>\?,\s:@\[\]]+?\s+\b"
            + Regex.Escape(fieldName) + @"\b\s*[;=]",
            RegexOptions.Multiline).Count;
        return totalOccurrences - assignments - declOccurrences > 0;
    }

    // Heuristic regression tests — synthetic source mirrors the v1 regression shapes so the
    // assertion is pinned independently of whether the live generated tree happens to be clean.

    [Test]
    public async Task Heuristic_VariablesShape_DeclAndOneLhs_NoRead_IsDead()
    {
        var src = """
            partial class H {
                private readonly Dictionary<string, object?>? __variables;
                public void M() { __variables = new Dictionary<string, object?>(); }
            }
            """;
        await Assert.That(HasReadOf(src, "__variables")).IsFalse();
    }

    [Test]
    public async Task Heuristic_DeclOnly_IsDead()
    {
        var src = """
            partial class H {
                private int __orphan;
            }
            """;
        await Assert.That(HasReadOf(src, "__orphan")).IsFalse();
    }

    [Test]
    public async Task Heuristic_DeclAndAssignAndRead_IsAlive()
    {
        var src = """
            partial class H {
                private int __counter;
                public void M() { __counter = 1; var x = __counter; }
            }
            """;
        await Assert.That(HasReadOf(src, "__counter")).IsTrue();
    }

    [Test]
    public async Task Heuristic_DoubleEqualsIsNotAnAssignment()
    {
        // Regex `=(?!=)` must reject `==`. Otherwise `if (__x == null)` is mis-counted as a write
        // and the only read is hidden.
        var src = """
            partial class H {
                private object? __x;
                public void M() { __x = null; if (__x == null) { } }
            }
            """;
        await Assert.That(HasReadOf(src, "__x")).IsTrue();
    }

    [Test]
    public async Task Heuristic_DeclWithInlineInit_NoRead_IsDead()
    {
        // `private int __x = 7;` — decl + assignment on the same line. No subsequent read.
        var src = """
            partial class H {
                private int __x = 7;
            }
            """;
        await Assert.That(HasReadOf(src, "__x")).IsFalse();
    }

    // Pattern B regression tests — drive IsOrphanMethod against synthetic source
    // so the orphan detection is pinned independently of whether the live generated
    // tree happens to have a clean caller graph.

    [Test]
    public async Task Heuristic_OrphanProtectedMethod_IsFlagged()
    {
        // The v1 regression shape: protected method declared in a generated handler,
        // zero callers anywhere. IsOrphanMethod must return true for the orphan shape.
        var allCallers = "// nothing references ParamData() here";
        var stripped = StripCommentsAndStrings(allCallers);
        await Assert.That(IsOrphanMethod("ParamData", stripped, new HashSet<string>())).IsTrue();
    }

    [Test]
    public async Task PublicOrProtectedMethodDecl_MatchesProtectedDeclaration()
    {
        // Pin the regex itself — without this, a future narrowing back to `public`-only
        // (the v3 toothlessness shape) would not be caught by IsOrphanMethod tests
        // alone, because they bypass the regex entirely.
        var src = "    protected static Data ParamData(string name) => Data.Ok();\n";
        var matches = PublicOrProtectedMethodDecl.Matches(src);
        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(matches[0].Groups[1].Value).IsEqualTo("ParamData");
    }

    [Test]
    public async Task PublicOrProtectedMethodDecl_MatchesPublicDeclaration()
    {
        var src = "    public async Task<int> Foo(int x) => x + 1;\n";
        var matches = PublicOrProtectedMethodDecl.Matches(src);
        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(matches[0].Groups[1].Value).IsEqualTo("Foo");
    }

    [Test]
    public async Task PublicOrProtectedMethodDecl_DoesNotMatchPrivate()
    {
        var src = "    private void Bar() {}\n";
        var matches = PublicOrProtectedMethodDecl.Matches(src);
        await Assert.That(matches.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Heuristic_CalledMethod_IsNotFlagged()
    {
        var allCallers = "void Caller() { var x = MyHelper(arg); }";
        var stripped = StripCommentsAndStrings(allCallers);
        await Assert.That(IsOrphanMethod("MyHelper", stripped, new HashSet<string>())).IsFalse();
    }

    [Test]
    public async Task Heuristic_ExemptedMethod_IsNotFlagged()
    {
        // Framework-dispatched methods (ExecuteAsync, SnapshotParams) have no
        // textual caller but are wired by interface dispatch in App.Run.
        var allCallers = "// no textual caller";
        var exempt = new HashSet<string> { "ExecuteAsync" };
        var stripped = StripCommentsAndStrings(allCallers);
        await Assert.That(IsOrphanMethod("ExecuteAsync", stripped, exempt)).IsFalse();
    }

    [Test]
    public async Task Strip_MethodNameInsideLineComment_DoesNotCountAsCaller()
    {
        // Tester #3 — once Pattern B widens to protected, `Data` and `Error` become
        // the names checked. They are common enough that comments containing them
        // would false-green the orphan check. Stripping comments fixes that.
        var src = "// Data() is a helper provided by the generator\n";
        var stripped = StripCommentsAndStrings(src);
        await Assert.That(IsOrphanMethod("Data", stripped, new HashSet<string>())).IsTrue();
    }

    [Test]
    public async Task Strip_MethodNameInsideStringLiteral_DoesNotCountAsCaller()
    {
        var src = "var template = \"Data()\";";
        var stripped = StripCommentsAndStrings(src);
        await Assert.That(IsOrphanMethod("Data", stripped, new HashSet<string>())).IsTrue();
    }

    [Test]
    public async Task Strip_MethodNameInsideRawStringLiteral_DoesNotCountAsCaller()
    {
        // Critical: PLang.Generators/Emission/Action/this.cs emits literal text
        // like `protected static Data() => ...` inside raw strings. Without stripping,
        // those emission templates falsely register as callers.
        var src = "var emitter = \"\"\"protected static Data() => Data.Ok();\"\"\";";
        var stripped = StripCommentsAndStrings(src);
        await Assert.That(IsOrphanMethod("Data", stripped, new HashSet<string>())).IsTrue();
    }

    private static string LoadAllCallableSources()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var dir in new[] { "PLang", "PLang.Tests", "PLang.Generators", "PlangConsole" })
        {
            var fullDir = Path.Combine(RepoRoot, dir);
            if (!Directory.Exists(fullDir)) continue;
            foreach (var f in Directory.GetFiles(fullDir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                if (f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                sb.AppendLine(StripCommentsAndStrings(File.ReadAllText(f)));
            }
        }
        return sb.ToString();
    }

    internal static string StripCommentsAndStrings(string src)
    {
        // Pragmatic regex pass — not a full lexer, but sufficient for detecting
        // whether `\bName\s*\(` appears in real code vs. comment/string text.
        // Order strips locals (single-line) before non-locals (multi-line) so a
        // single unbalanced `/*` inside a string can't make the block-comment
        // regex run away across the file.
        src = Regex.Replace(src, "\"\"\"[\\s\\S]*?\"\"\"", " ");          // raw strings (multi-line, well-bounded)
        src = Regex.Replace(src, @"@""(?:[^""]|"""")*""", " ");           // verbatim strings (multi-line, well-bounded)
        src = Regex.Replace(src, @"""(?:\\.|[^""\\\n])*""", " ");         // regular strings (single-line)
        src = Regex.Replace(src, @"'(?:\\.|[^'\\\n])'", " ");             // char literals (single-line)
        src = Regex.Replace(src, @"/\*[\s\S]*?\*/", " ");                 // block comments
        src = Regex.Replace(src, @"//[^\n]*", " ");                       // line comments
        return src;
    }
}
