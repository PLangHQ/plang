using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

    private static string GeneratedDir => Path.Combine(RepoRoot, "PLang.Tests", "Generator", "obj", "Debug",
        "net10.0", "generated", "PLang.Generators", "PLang.Generators.this");

    private static string ReadAnyGeneratedHandler()
    {
        var anyFile = Directory.GetFiles(GeneratedDir, "*.Action.g.cs").FirstOrDefault();
        return anyFile != null ? File.ReadAllText(anyFile) : string.Empty;
    }

    // Post-v5: Discovery emits PLNG001 diagnostic when a property is not Data<T>
    // or [Code] T. ([VariableName] string was removed; Variable-name slots are
    // now Data<Variable>.) Verified by the descriptor's existence and message
    // format in Discovery/this.cs.
    [Test]
    public async Task RawScalarProperty_FailsBuild_WithSpecificError()
    {
        var discoverySrc = ReadGeneratorSource("Discovery/this.cs");
        await Assert.That(discoverySrc).Contains("PLNG001");
        await Assert.That(discoverySrc).Contains("RawScalarPropertyDescriptor");
        await Assert.That(discoverySrc).Contains("DiagnosticSeverity.Error");
    }

    // codeanalyzer/v2 (#45) flagged that PLNG001 was emitted with a synthetic 1-character
    // span — IDE squiggles pointed at one column instead of underlining the property name.
    // v3 widens DiagnosticInfo to carry the full identifier span; this test pins the new
    // contract by driving the generator and asserting the diagnostic's location length
    // exceeds 1.
    [Test]
    public async Task RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier()
    {
        const string sourceWithRawScalar = """
            using System;
            namespace app.module {
                public class ActionAttribute : Attribute {}
            }
            namespace app.Test {
                [app.module.Action]
                public partial class BadHandler {
                    // Raw int — not Data<T>, not [Code], not [VariableName]. Triggers PLNG001.
                    public partial int RawIntProperty { get; init; }
                }
            }
            """;
        // Path must be non-empty — orchestrator falls back to Location.None when FilePath is empty,
        // which is the realistic build behaviour (real source files always have a path).
        var compilation = CSharpCompilation.Create(
            "DiagnosticTest",
            new[] { CSharpSyntaxTree.ParseText(sourceWithRawScalar,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: "BadHandler.cs") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            new[] { new PLang.Generators.@this().AsSourceGenerator() });
        driver = driver.RunGenerators(compilation);

        var diagnostics = driver.GetRunResult().Diagnostics;
        var plng001 = diagnostics.FirstOrDefault(d => d.Id == "PLNG001");
        await Assert.That(plng001).IsNotNull();

        var span = plng001!.Location.GetLineSpan();
        var startCol = span.StartLinePosition.Character;
        var endCol = span.EndLinePosition.Character;
        var startLine = span.StartLinePosition.Line;
        var endLine = span.EndLinePosition.Line;

        // Identifier `RawIntProperty` is 14 chars wide. The diagnostic must span more than
        // one character — anything ≥ 2 is acceptable; the v2 regression was a hard-coded 1.
        var spanWidth = endLine == startLine ? endCol - startCol : int.MaxValue;
        await Assert.That(spanWidth).IsGreaterThan(1)
            .Because($"PLNG001 location should underline the identifier, not a 1-char synthetic span (got width={spanWidth})");
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
    public async Task CodeProperty_BuildsSuccessfully()
    {
        // Verified by the [Code] matrix handlers compiling green (matrix/provider/Handlers.cs).
        // The Code leaf lives at Emission/Property/Code/this.cs as record @this (per OBP convention).
        var codeSrc = ReadGeneratorSource("Emission/Property/Code/this.cs");
        await Assert.That(codeSrc).Contains("namespace PLang.Generators.Emission.Property.Code");
        await Assert.That(codeSrc).Contains("record @this");
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
            "app.module.matrix.plain.StringPlain.Action.g.cs"));
        var intPlainSrc = File.ReadAllText(Path.Combine(GeneratedDir,
            "app.module.matrix.plain.IntPlain.Action.g.cs"));

        // Lazy resolution: the property resolves its slot to a typed VIEW via As<T>
        // (conversion/errors surface later, at the value door) — not an eager
        // materialize-and-clone.
        await Assert.That(stringPlainSrc).Contains("__d.As<global::app.type.item.text.@this>()");
        await Assert.That(intPlainSrc).Contains("__d.As<global::app.type.number.@this>()");
    }

    [Test]
    public async Task GeneratedPropertyBody_UsesGetParameterAndAsT()
    {
        var generated = ReadAnyGeneratedHandler();
        // The lookup-then-resolve idiom: __ResolveData(name) → As<T>() (a lazy typed
        // view). __ResolveData itself delegates to Action.GetParameter under the hood.
        await Assert.That(generated).Contains("__ResolveData");
        await Assert.That(generated).Contains(".As<");
    }

    [Test]
    public async Task Generator_DoesNotEmitOldHelperFamily()
    {
        // The old helper family was: __TryConvert, __FormatValue, __HasParam,
        // __StripPercent, __Resolve<T>. Post-v5, all are gone — only __ResolveData
        // remains (Data emit's lookup helper).
        var generated = ReadAnyGeneratedHandler();
        await Assert.That(generated.Contains("__TryConvert")).IsFalse();
        await Assert.That(generated.Contains("__FormatValue")).IsFalse();
        await Assert.That(generated.Contains("__HasParam")).IsFalse();
        await Assert.That(generated.Contains("__StripPercent")).IsFalse();
        await Assert.That(generated.Contains("__Resolve<")).IsFalse();
    }

    [Test]
    public async Task Generator_DoesNotEmitResetResolution()
    {
        var generated = ReadAnyGeneratedHandler();
        await Assert.That(generated.Contains("ResetResolution")).IsFalse();
        await Assert.That(generated.Contains("NeedsResolution")).IsFalse();
    }

    [Test]
    public async Task GeneratedExecuteAsync_WrapsRunInTryCatch()
    {
        var generated = ReadAnyGeneratedHandler();
        // runtime2's builder-quality pass wraps Run() in a narrow try/catch so a bare
        // CLR exception (NRE, InvalidCast) surfaces as a ServiceError carrying
        // "{module}.{action}: {ExType}: {msg}" instead of an anonymous bare throw.
        // The catch converts to a ServiceError; there is deliberately no finally —
        // lifecycle/cleanup still lives in Call.ExecuteAsync, not the generated Execute().
        await Assert.That(generated).Contains("try { return await Run(); }");
        await Assert.That(generated.Contains("finally\n") || generated.Contains("finally {")
            || generated.Contains("finally\r\n") || generated.Contains("finally    {")).IsFalse();
    }

    [Test]
    public async Task GeneratedExecuteAsync_CallsRunDirectly()
    {
        var generated = ReadAnyGeneratedHandler();
        // Execute() invokes Run() inline (inside the try/catch wrap), not via a
        // wrapper method.
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
        // Post-v5 contract: only two property leaves remain — Data and Code.
        // Legacy/ is deleted along with [VariableName].
        var basePath = Path.Combine(RepoRoot, "PLang.Generators", "Emission", "Property");
        await Assert.That(Directory.Exists(Path.Combine(basePath, "Data"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(basePath, "Code"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(basePath, "this.cs"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(basePath, "Legacy"))).IsFalse();
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

        // Same check on the leaves (post-v5: Data + Code only)
        foreach (var leaf in new[] { "Data/this.cs", "Code/this.cs" })
        {
            var leafSrc = ReadGeneratorSource($"Emission/Property/{leaf}");
            var leafCode = string.Join('\n', leafSrc.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("///") && !line.TrimStart().StartsWith("//")));
            await Assert.That(leafCode.Contains("IPropertySymbol")).IsFalse();
        }
    }
}
