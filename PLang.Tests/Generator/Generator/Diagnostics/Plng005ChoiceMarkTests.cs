using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.Generator.Diagnostics;

/// <summary>
/// PLNG005 — a <c>choice&lt;T&gt;</c> draws on a class marked <c>[Choice]</c>: the types read the marked classes when
/// an app is born, so a choice over an unmarked class is one no app knows the options of.
/// </summary>
public class Plng005ChoiceMarkTests
{
    private const string Stubs = """
        using System;
        namespace app.Attributes { public sealed class ChoiceAttribute : Attribute {} }
        namespace app.type.item.choice { public sealed class @this<T> {} }
        """;

    private static System.Collections.Immutable.ImmutableArray<Diagnostic> Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Plng005Test",
            new[]
            {
                CSharpSyntaxTree.ParseText(Stubs, new CSharpParseOptions(LanguageVersion.Preview), path: "/src/Stubs.cs"),
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path: "/src/Test.cs"),
            },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            new[] { new PLang.Generators.@this().AsSourceGenerator() });
        return driver.RunGenerators(compilation).GetRunResult().Diagnostics;
    }

    [Test] public async Task Fires_OnAChoiceOverAnUnmarkedEnum()
    {
        var diags = Run("""
            namespace app.module.foo {
                public enum Level { low, high }
                public class Handler { public app.type.item.choice.@this<Level> Level { get; init; } }
            }
            """);

        var found = diags.Where(d => d.Id == "PLNG005").ToList();
        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(found[0].GetMessage()).Contains("app.module.foo.Level");
    }

    [Test] public async Task DoesNotFire_OnAChoiceOverAMarkedEnum()
    {
        var diags = Run("""
            namespace app.module.foo {
                [app.Attributes.Choice] public enum Level { low, high }
                public class Handler { public app.type.item.choice.@this<Level> Level { get; init; } }
            }
            """);

        await Assert.That(diags.Any(d => d.Id == "PLNG005")).IsFalse();
    }

    [Test] public async Task DoesNotFire_OnTheChoicesOwnGenericT_OrAnUnboundChoice()
    {
        var diags = Run("""
            namespace app.module.foo {
                public class Holder<T> { public app.type.item.choice.@this<T>? Value { get; init; } }
                public static class Form { public static System.Type Open => typeof(app.type.item.choice.@this<>); }
            }
            """);

        await Assert.That(diags.Any(d => d.Id == "PLNG005")).IsFalse();
    }
}
