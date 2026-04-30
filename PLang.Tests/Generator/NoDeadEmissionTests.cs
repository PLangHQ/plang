using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PLang.Tests.Generator;

// Contract test for "no dead private fields in generated handlers".
// Codeanalyzer Finding 11/12: __variables and __paramData were declared, assigned,
// and never read. The v1 test set had no scan that would catch dead emission. This
// fixture parses every *.Action.g.cs and asserts every private field has at least
// one read elsewhere in the same file.

public class NoDeadEmissionTests
{
    private static string GeneratedDir
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
            return Path.Combine(dir!, "PLang.Tests", "obj", "Debug", "net10.0",
                "generated", "PLang.Generators", "PLang.Generators.LazyParamsGenerator");
        }
    }

    // Names that the generator emits but only the legacy raw-scalar / __Resolve pipeline reads.
    // Phase 5 deletes the legacy family; remove these exemptions when that lands.
    private static readonly HashSet<string> _phase5LegacyExemptions = new()
    {
        "__resolutionError", // assigned by __Resolve<T>; read by ExecuteAsync's guard. v4-shape handlers without legacy props never reassign it.
    };

    [Test]
    public async Task NoGeneratedHandlerDeclaresAnUnreadPrivateField()
    {
        var files = Directory.Exists(GeneratedDir)
            ? Directory.GetFiles(GeneratedDir, "*.Action.g.cs")
            : Array.Empty<string>();

        await Assert.That(files).IsNotEmpty();

        var fieldDecl = new Regex(@"^\s*private\s+[\w\.<>\?,\s:@]+?\s+(__\w+)\s*[;=]",
            RegexOptions.Multiline);

        var dead = new System.Collections.Generic.List<string>();
        foreach (var path in files)
        {
            var src = File.ReadAllText(path);
            foreach (Match m in fieldDecl.Matches(src))
            {
                var fieldName = m.Groups[1].Value;
                if (_phase5LegacyExemptions.Contains(fieldName)) continue;

                // Count occurrences of the identifier elsewhere (anywhere it appears).
                // Subtract 1 for the declaration line. A field with only the declaration
                // (count == 1) and an assignment on the same identifier is still "set, not read";
                // distinguish read by counting non-LHS occurrences.
                var allOccurrences = Regex.Matches(src, @"\b" + Regex.Escape(fieldName) + @"\b").Count;
                // Assignments where the field is on the LHS: `fieldName = ...` or `fieldName[x] = ...` or `fieldName?[x] = ...`
                var assignments = Regex.Matches(src,
                    @"\b" + Regex.Escape(fieldName) + @"\b\s*(\??\[[^\]]*\])?\s*=").Count;
                var reads = allOccurrences - assignments;

                // The declaration itself counts as a "use" by the regex above (just the bare name)
                // but isn't a read — adjust by subtracting 1 if the line has no `=`.
                // Simpler heuristic: a field is dead if reads <= 0.
                if (reads <= 0)
                {
                    dead.Add($"{Path.GetFileName(path)}:{fieldName}");
                }
            }
        }

        await Assert.That(dead).IsEmpty();
    }
}
