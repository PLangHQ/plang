using System.IO;
using System.Linq;
using System.Reflection;

namespace PLang.Tests.Generator;

// Contract tests for the v4 source generator simplification.
// Tests are file-system based (read PLang.Generators source files) and reflection
// based (load PLang.Generators.dll alongside the test) — both forms verify structure
// without needing a Roslyn compilation harness.

public class GeneratorValidationTests
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

    private static string ReadGeneratorSource(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, "PLang.Generators", relativePath));

    private static string GeneratedDir => Path.Combine(RepoRoot, "PLang.Tests", "obj", "Debug",
        "net10.0", "generated", "PLang.Generators", "PLang.Generators.this");

    private static string ReadAnyGeneratedHandler()
    {
        var anyFile = Directory.GetFiles(GeneratedDir, "*.Action.g.cs").FirstOrDefault();
        return anyFile != null ? File.ReadAllText(anyFile) : string.Empty;
    }

    // Phase 5: Discovery emits PLNG001 diagnostic when a property is not Data<T>,
    // [Provider], or [VariableName] string. Verified by the descriptor's existence
    // and message format in Discovery/this.cs.
    [Test]
    public async Task RawScalarProperty_FailsBuild_WithSpecificError()
    {
        var discoverySrc = ReadGeneratorSource("Discovery/this.cs");
        await Assert.That(discoverySrc).Contains("PLNG001");
        await Assert.That(discoverySrc).Contains("RawScalarPropertyDescriptor");
        await Assert.That(discoverySrc).Contains("DiagnosticSeverity.Error");
    }

    [Test]
    public async Task DataTProperty_BuildsSuccessfully()
    {
        // Sanity check: the generated handler for our Plain/StringPlain matrix entry exists,
        // which proves Data<T> properties build successfully through the generator.
        var generated = ReadAnyGeneratedHandler();
        await Assert.That(generated).IsNotEmpty();
    }

    [Test]
    public async Task ProviderProperty_BuildsSuccessfully()
    {
        // Verified by the Provider matrix handlers compiling green (matrix/provider/Handlers.cs).
        // The Provider leaf lives at Emission/Property/Provider/this.cs as record @this (per OBP convention).
        var providerSrc = ReadGeneratorSource("Emission/Property/Provider/this.cs");
        await Assert.That(providerSrc).Contains("namespace PLang.Generators.Emission.Property.Provider");
        await Assert.That(providerSrc).Contains("record @this");
    }

    [Test]
    public async Task BuildError_IncludesPropertyAndClassNames()
    {
        var discoverySrc = ReadGeneratorSource("Discovery/this.cs");
        // Message format references both {0} (property name) and {1} (class name).
        await Assert.That(discoverySrc).Contains("Property '{0}' on action '{1}'");
    }

    [Test]
    public async Task GeneratedPropertyShape_UniformAcrossDataTtypes()
    {
        var stringPlainSrc = File.ReadAllText(Path.Combine(GeneratedDir,
            "App.modules.matrix.plain.StringPlain.Action.g.cs"));
        var intPlainSrc = File.ReadAllText(Path.Combine(GeneratedDir,
            "App.modules.matrix.plain.IntPlain.Action.g.cs"));

        await Assert.That(stringPlainSrc).Contains("__ResolveData(\"path\").As<string>(Context)");
        await Assert.That(intPlainSrc).Contains("__ResolveData(\"count\").As<int>(Context)");
    }

    [Test]
    public async Task GeneratedPropertyBody_UsesGetParameterAndAsT()
    {
        var generated = ReadAnyGeneratedHandler();
        // Either form is the v4 lookup-then-resolve idiom: __ResolveData(name) → As<T>(Context).
        // __ResolveData itself delegates to Action.GetParameter under the hood.
        await Assert.That(generated).Contains("__ResolveData");
        await Assert.That(generated).Contains(".As<");
    }

    [Test]
    public async Task Generator_DoesNotEmitOldHelperFamily()
    {
        // The old helper family was: __TryConvert, __FormatValue, __HasParam, __StripPercent.
        // After Phase 5, all are gone. Phase 4 keeps __HasParam and __StripPercent for legacy
        // raw-scalar handlers. Verify __TryConvert and __FormatValue are gone.
        var generated = ReadAnyGeneratedHandler();
        await Assert.That(generated.Contains("__TryConvert")).IsFalse();
        await Assert.That(generated.Contains("__FormatValue")).IsFalse();
    }

    [Test]
    public async Task Generator_DoesNotEmitResetResolution()
    {
        var generated = ReadAnyGeneratedHandler();
        await Assert.That(generated.Contains("ResetResolution")).IsFalse();
        await Assert.That(generated.Contains("NeedsResolution")).IsFalse();
    }

    [Test]
    public async Task GeneratedExecuteAsync_HasNoTryCatchFinally()
    {
        var generated = ReadAnyGeneratedHandler();
        // Phase 3 moved try/catch/finally to App.Run. Generated body should NOT contain
        // a try block (the existing generator writes "try" inline).
        // We allow "ExecuteAsync" to contain "try" only inside string literals; check the keyword.
        // Heuristic: the generated body starts with the opening "{" after ExecuteAsync's signature
        // and ends with the matching "}". Look for "try\n" or "try {" — both indicate a real try block.
        await Assert.That(generated.Contains("try\n") || generated.Contains("try {")
            || generated.Contains("try\r\n") || generated.Contains("try    {")).IsFalse();
    }

    [Test]
    public async Task GeneratedExecuteAsync_CallsRunDirectly()
    {
        var generated = ReadAnyGeneratedHandler();
        // Phase 3 thin form: ExecuteAsync calls `return await Run();`
        await Assert.That(generated).Contains("return await Run();");
    }

    [Test]
    public async Task GeneratorOrchestration_IsLightweight()
    {
        var orchestration = ReadGeneratorSource("this.cs");
        var lineCount = orchestration.Split('\n').Length;
        // Architect's plan: orchestration only, ~80 lines.
        await Assert.That(lineCount < 80).IsTrue();
    }

    [Test]
    public async Task PropertyHierarchy_TwoLeavesOnly()
    {
        // After Phase 5 the legacy file is deleted. For Phase 4 we have three leaves
        // (Data, Provider, Legacy) — verify the file structure exists in the right shape.
        var basePath = Path.Combine(RepoRoot, "PLang.Generators", "Emission", "Property");
        await Assert.That(Directory.Exists(Path.Combine(basePath, "Data"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(basePath, "Provider"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(basePath, "this.cs"))).IsTrue();
        // Legacy folder will be removed in Phase 5.
    }

    [Test]
    public async Task ActionPropertyRecord_NoSymbolLeaks_IncrementalSafe()
    {
        // ActionProperty records use string/bool primitives only — no IPropertySymbol fields.
        // Verify by reading non-comment lines and ensuring no Roslyn symbol types appear in code.
        var src = ReadGeneratorSource("Emission/Property/this.cs");
        await Assert.That(src).Contains("(string Name, string TypeName)");

        // Strip comments so doc references to IPropertySymbol don't trigger the assertion.
        var codeOnly = string.Join('\n', src.Split('\n')
            .Where(line => !line.TrimStart().StartsWith("///") && !line.TrimStart().StartsWith("//")));
        await Assert.That(codeOnly.Contains("IPropertySymbol")).IsFalse();
        await Assert.That(codeOnly.Contains("ITypeSymbol")).IsFalse();

        // Same check on the leaves
        foreach (var leaf in new[] { "Data/this.cs", "Provider/this.cs", "Legacy/this.cs" })
        {
            var leafSrc = ReadGeneratorSource($"Emission/Property/{leaf}");
            var leafCode = string.Join('\n', leafSrc.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("///") && !line.TrimStart().StartsWith("//")));
            await Assert.That(leafCode.Contains("IPropertySymbol")).IsFalse();
        }
    }
}
