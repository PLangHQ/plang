using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.Generator.Diagnostics;

/// <summary>
/// PLNG006 — a value is written through its output kind: <c>writer.Kind.pair.Output(value, …)</c>, never
/// <c>value.Output(writer, …)</c>, which skips the pair the kind has for it.
/// </summary>
public class Plng006WrittenThroughKindTests
{
    private const string Stubs = """
        using System.Threading.Tasks;
        namespace app { public enum View { Out, Store, Debug } }
        namespace app.actor.context { public sealed class @this {} }
        namespace app.type.format {
            public interface IWriter {}
            public interface IOutput { ValueTask Output(app.type.item.@this value, IWriter writer, app.View mode, app.actor.context.@this? context); }
        }
        namespace app.type.item {
            public class @this { public virtual ValueTask Output(app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context) => default; }
        }
        namespace app.type.item.text { public sealed class @this : app.type.item.@this {
            public override ValueTask Output(app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context) => default; } }
        namespace app.data { public sealed class @this { public ValueTask Output(app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context) => default; } }
        namespace app.type.kind.pair.list { public sealed class @this {
            public ValueTask Output(app.type.item.@this value, app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context)
                => value.Output(writer, mode, context); } }
        """;

    private static System.Collections.Immutable.ImmutableArray<Diagnostic> Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Plng006Test",
            new[]
            {
                CSharpSyntaxTree.ParseText(Stubs, new CSharpParseOptions(LanguageVersion.Preview), path: "/src/Stubs.cs"),
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path: "/src/Test.cs"),
            },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.ValueTask).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            new[] { new PLang.Generators.@this().AsSourceGenerator() });
        return driver.RunGenerators(compilation).GetRunResult().Diagnostics;
    }

    private static int Found(System.Collections.Immutable.ImmutableArray<Diagnostic> diags)
        => diags.Count(d => d.Id == "PLNG006");

    [Test] public async Task Fires_OnAMemberWrittenAroundItsKind()
    {
        var diags = Run("""
            namespace app.module.foo {
                public sealed class box : app.type.item.@this {
                    public app.type.item.text.@this Name { get; } = new();
                    public override async System.Threading.Tasks.ValueTask Output(app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context)
                        => await Name.Output(writer, mode, context);
                }
            }
            """);

        var found = diags.Where(d => d.Id == "PLNG006").ToList();
        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(found[0].GetMessage()).Contains("Name");
    }

    [Test] public async Task Fires_OnARootWrittenAroundItsKind_OutsideAnOutput()
    {
        var diags = Run("""
            namespace app.module.foo {
                public static class show {
                    public static System.Threading.Tasks.ValueTask Write(app.type.item.@this value, app.type.format.IWriter writer)
                        => value.Output(writer, app.View.Out, null);
                }
            }
            """);

        await Assert.That(Found(diags)).IsEqualTo(1);
    }

    [Test] public async Task DoesNotFire_OnThisOrBase_OrOnAData()
    {
        var diags = Run("""
            namespace app.module.foo {
                public sealed class box : app.type.item.@this {
                    public app.data.@this Held { get; } = new();
                    public override async System.Threading.Tasks.ValueTask Output(app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context)
                    {
                        await base.Output(writer, mode, context);
                        await Held.Output(writer, mode, context);
                    }
                    public System.Threading.Tasks.ValueTask Again(app.type.format.IWriter writer) => this.Output(writer, app.View.Out, null);
                }
            }
            """);

        await Assert.That(Found(diags)).IsEqualTo(0);
    }

    [Test] public async Task DoesNotFire_InTheDoor_OrInAPairWritingItsOwnValue()
    {
        var diags = Run("""
            namespace app.module.foo.format {
                public sealed class plang : app.type.format.IOutput {
                    public System.Threading.Tasks.ValueTask Output(app.type.item.@this value, app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context)
                        => value.Output(writer, mode, context);
                }
            }
            """);

        await Assert.That(Found(diags)).IsEqualTo(0);
    }

    [Test] public async Task Fires_InAPairWritingAnotherValue()
    {
        var diags = Run("""
            namespace app.module.foo.format {
                public sealed class plang : app.type.format.IOutput {
                    public app.type.item.text.@this Other { get; } = new();
                    public System.Threading.Tasks.ValueTask Output(app.type.item.@this value, app.type.format.IWriter writer, app.View mode, app.actor.context.@this? context)
                        => Other.Output(writer, mode, context);
                }
            }
            """);

        await Assert.That(Found(diags)).IsEqualTo(1);
    }
}
