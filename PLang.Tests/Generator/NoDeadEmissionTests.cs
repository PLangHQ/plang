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
//
// codeanalyzer/v2 (#40) flagged that the v2 incarnation of this fixture was
// structurally incapable of catching either regression: its `reads<=0` heuristic
// gave reads=1 for the __variables shape (decl + 1 LHS = 2 occurrences − 1 LHS = 1)
// and reads=2 for the __paramData shape (decl + 2 LHS + 1 internal read = 4 − 2 = 2).
//
// v3 fixes this by splitting into two contracts:
//   Pattern A — every declared private field has at least one read in the same file
//     (heuristic: reads = total_occurrences − assignments − decl_line_occurrences).
//     Catches the __variables shape.
//   Pattern B — every public method declared in a generated handler has at least
//     one caller across the source tree (excluding the generated tree itself).
//     Catches the __paramData/ParamData() shape.
// And a third assertion (#44) pinning the `__`-prefix convention.

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
    public async Task NoGeneratedHandlerExposesUnusedPublicMethod()
    {
        // Pattern B: cross-file scan for orphan public methods.
        // A generated handler that emits `public Foo() { return __field; }` with no
        // callers anywhere is a __paramData-class regression: __field looks alive in-file
        // but the chain Foo → __field is dead end-to-end.
        var files = Directory.Exists(GeneratedDir)
            ? Directory.GetFiles(GeneratedDir, "*.Action.g.cs")
            : Array.Empty<string>();
        await Assert.That(files).IsNotEmpty();

        var allCallableSources = LoadAllCallableSources();

        var orphans = new List<string>();
        foreach (var path in files)
        {
            var src = File.ReadAllText(path);
            foreach (Match m in PublicMethodDecl.Matches(src))
            {
                var name = m.Groups[1].Value;
                if (_publicMethodCallerExemptions.Contains(name)) continue;

                var callerPattern = new Regex(@"\b" + Regex.Escape(name) + @"\s*\(");
                if (!callerPattern.IsMatch(allCallableSources))
                    orphans.Add($"{Path.GetFileName(path)}:{name}");
            }
        }
        await Assert.That(orphans).IsEmpty();
    }

    private static readonly Regex PrivateFieldDecl = new(
        @"^\s*private\s+(?:static\s+|readonly\s+|const\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*[;=]",
        RegexOptions.Multiline);

    private static readonly Regex PublicMethodDecl = new(
        @"^\s*public\s+(?:async\s+|partial\s+|static\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*\(",
        RegexOptions.Multiline);

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
                sb.AppendLine(File.ReadAllText(f));
            }
        }
        return sb.ToString();
    }
}
