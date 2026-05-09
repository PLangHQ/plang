using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PLang.Tests.Generator.Diagnostics;

// Post-v5 PLNG001 contract — allowed shapes shrink to two:
//   - Data<T>
//   - plain Data
//   - [Code] T
//
// [VariableName] string was a transitional carve-out; it is deleted in v5.
// Variable-name slots use Data<Variable> (App.Variables.Variable, with the
// IRawNameResolvable marker that bypasses %var% substitution in Data.As<T>).
//
// PLNG001 still rejects raw scalars (the original purpose of the diagnostic).
//
// Tests drive the generator through CSharpGeneratorDriver and inspect emitted
// diagnostics — same harness GeneratorValidationTests uses for its location-span
// pin (RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier).

public class Plng001PostMigrationTests
{
    private static GeneratorDriver Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "PostMigrationTest",
            new[] { CSharpSyntaxTree.ParseText(source,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: "Test.cs") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            new[] { new PLang.Generators.@this().AsSourceGenerator() });
        return driver.RunGenerators(compilation);
    }

    private const string Stubs = """
        using System;
        namespace App.modules {
            public class ActionAttribute : Attribute {}
            public class CodeAttribute : Attribute {}
            public interface IContext {}
            public interface ICodeGenerated {}
        }
        namespace App.Data {
            public partial class @this { public class Type {} }
            public partial class @this<T> : @this {}
        }
        """;

    // Data<T> property — the canonical typed shape. NO PLNG001.
    [Test]
    public async Task PLNG001_DataT_Property_NoDiagnostic()
    {
        var source = Stubs + """
            namespace App.Test {
                [App.modules.Action]
                public partial class GoodHandler {
                    public partial App.Data.@this<int> Count { get; init; }
                }
            }
            """;

        var diagnostics = Run(source).GetRunResult().Diagnostics;
        await Assert.That(diagnostics.Any(d => d.Id == "PLNG001")).IsFalse();
    }

    // Plain Data property — canonical for live-variable refs (Pattern A). NO PLNG001.
    [Test]
    public async Task PLNG001_PlainData_Property_NoDiagnostic()
    {
        var source = Stubs + """
            namespace App.Test {
                [App.modules.Action]
                public partial class GoodHandler {
                    public partial App.Data.@this Value { get; init; }
                }
            }
            """;

        var diagnostics = Run(source).GetRunResult().Diagnostics;
        await Assert.That(diagnostics.Any(d => d.Id == "PLNG001")).IsFalse();
    }

    // [Code] T property — for engine-resolved code dependencies. NO PLNG001.
    [Test]
    public async Task PLNG001_CodeProperty_NoDiagnostic()
    {
        var source = Stubs + """
            namespace App.Test {
                public interface IFooProvider {}
                [App.modules.Action]
                public partial class GoodHandler {
                    [App.modules.Code]
                    public partial IFooProvider Foo { get; init; }
                }
            }
            """;

        var diagnostics = Run(source).GetRunResult().Diagnostics;
        await Assert.That(diagnostics.Any(d => d.Id == "PLNG001")).IsFalse();
    }

    // [VariableName] string property — the transitional carve-out is REMOVED.
    // The attribute itself no longer exists in App.modules; if a handler still
    // declares a `partial string` (with the now-undefined [VariableName] decoration
    // or without), PLNG001 fires because raw `string` doesn't match Data<T>/Data/[Code].
    [Test]
    public async Task PLNG001_VariableNameAttribute_NowReportsDiagnostic()
    {
        // Author who forgot to migrate — bare `partial string`, no Data wrap.
        var source = Stubs + """
            namespace App.Test {
                [App.modules.Action]
                public partial class StaleHandler {
                    public partial string Name { get; init; }
                }
            }
            """;

        var diagnostics = Run(source).GetRunResult().Diagnostics;
        var plng001 = diagnostics.FirstOrDefault(d => d.Id == "PLNG001");
        await Assert.That(plng001).IsNotNull();
        await Assert.That(plng001!.GetMessage()).Contains("Name");
    }

    // Raw scalar (e.g. `public partial int Count`) still rejects. The diagnostic
    // didn't go away with the migration — only the allowed-shape list shrank.
    [Test]
    public async Task PLNG001_RawScalar_StillReportsDiagnostic()
    {
        var source = Stubs + """
            namespace App.Test {
                [App.modules.Action]
                public partial class RawScalarHandler {
                    public partial int RawCount { get; init; }
                }
            }
            """;

        var diagnostics = Run(source).GetRunResult().Diagnostics;
        var plng001 = diagnostics.FirstOrDefault(d => d.Id == "PLNG001");
        await Assert.That(plng001).IsNotNull();
        await Assert.That(plng001!.GetMessage()).Contains("RawCount");
    }
}
