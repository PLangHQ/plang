using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PLang.Tests.Generator.Diagnostics;

// Phase 6 contract — PLNG001 allowed-shape list shrinks. Pre-Phase 6, the
// allowed shapes were:
//   - Data<T>
//   - plain Data
//   - [Provider] T
//   - [VariableName] string  (transitional carve-out)
//
// Post-Phase 6, [VariableName] is gone — handlers that need a literal name
// slot use Data<string> instead (architect's "Pattern B" — Data<string> for
// literal name slots like foreach.ItemName / variable.set.Variable).
//
// PLNG001 still rejects raw scalars (the original purpose of the diagnostic).
// Provider properties still pass.
//
// Existing GeneratorValidationTests covers the diagnostic descriptor's general
// shape (PLNG001 exists, error severity, message format). This file pins the
// allowed-shape list specifically.

public class Plng001PostMigrationTests
{
    private static string RepoRoot => FindRepoRoot();

    private static string FindRepoRoot()
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

    // Data<T> property — the canonical typed shape. NO PLNG001.
    [Test]
    public async Task PLNG001_DataT_Property_NoDiagnostic()
    {
        Assert.Fail("Not implemented");
    }

    // Plain Data property — canonical for live-variable refs (Pattern A). NO PLNG001.
    [Test]
    public async Task PLNG001_PlainData_Property_NoDiagnostic()
    {
        Assert.Fail("Not implemented");
    }

    // [Provider] T property — for engine-resolved providers. NO PLNG001.
    [Test]
    public async Task PLNG001_ProviderProperty_NoDiagnostic()
    {
        Assert.Fail("Not implemented");
    }

    // [VariableName] string property — was a transitional carve-out, now REMOVED.
    // Phase 6 deletes the [VariableName] attribute and rejects this shape.
    // The diagnostic must fire because handlers need to migrate to Data<string>
    // (Pattern B) for literal name slots.
    [Test]
    public async Task PLNG001_VariableNameAttribute_NowReportsDiagnostic()
    {
        Assert.Fail("Not implemented");
    }

    // Raw scalar (e.g. `public partial int Count`) still rejects. The diagnostic
    // didn't go away with Phase 6 — only the allowed-shape list shrank.
    [Test]
    public async Task PLNG001_RawScalar_StillReportsDiagnostic()
    {
        Assert.Fail("Not implemented");
    }
}
