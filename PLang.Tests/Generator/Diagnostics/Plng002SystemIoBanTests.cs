using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.Generator.Diagnostics;

/// <summary>
/// PLNG002 — bans <c>System.IO.*</c> reaches and <c>data.@this&lt;string&gt;</c>
/// path-named properties under <c>PLang/app/**</c> (excluding
/// <c>PLang/app/types/path/**</c> and Generators).
///
/// Stage 1 lands the diagnostic in <b>warning</b> mode; Stage 6 flips to
/// <b>error</b>. The fires/silent fixtures below pin the rule independently
/// of severity.
///
/// Allowlist: <c>System.IO.Path.DirectorySeparatorChar</c> /
/// <c>AltDirectorySeparatorChar</c> (separator constants, not IO).
/// </summary>
public class Plng002SystemIoBanTests
{
    private const string Stubs = """
        using System;
        namespace app.modules {
            public class ActionAttribute : Attribute {}
            public class CodeAttribute : Attribute {}
            public interface IContext {}
            public interface ICodeGenerated {}
        }
        namespace app.data {
            public partial class @this { public class type {} }
            public partial class @this<T> : @this {}
        }
        """;

    private static System.Collections.Immutable.ImmutableArray<Diagnostic> Run(string source, string filePath)
    {
        var systemIoRef = MetadataReference.CreateFromFile(typeof(File).Assembly.Location);
        var compilation = CSharpCompilation.Create(
            "Plng002Test",
            new[] { CSharpSyntaxTree.ParseText(source,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: filePath) },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                systemIoRef,
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            new[] { new PLang.Generators.@this().AsSourceGenerator() });
        return driver.RunGenerators(compilation).GetRunResult().Diagnostics;
    }

    private const string ModulesPath = "/workspace/plang/PLang/app/modules/foo/Test.cs";
    private const string PathTypesPath = "/workspace/plang/PLang/app/types/path/test/Test.cs";

    [Test] public async Task Fires_OnFileReadAllText_UnderModulesNamespace()
    {
        var source = """
            namespace app.modules.foo {
                public class Handler {
                    public string Read() => System.IO.File.ReadAllText("x.txt");
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task Fires_OnDirectoryGetFiles_UnderModulesNamespace()
    {
        var source = """
            namespace app.modules.foo {
                public class Handler {
                    public string[] Walk() => System.IO.Directory.GetFiles("/tmp");
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task Fires_OnSystemIoPathCombine_UnderModulesNamespace()
    {
        var source = """
            namespace app.modules.foo {
                public class Handler {
                    public string Join() => System.IO.Path.Combine("a", "b");
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task Fires_OnDataOfString_NamedPath_InActionHandler()
    {
        var source = Stubs + """
            namespace app.modules.foo {
                [app.modules.Action]
                public partial class Handler {
                    public partial app.data.@this<string> Path { get; init; }
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task DoesNotFire_OnSystemIoPathDirectorySeparatorChar()
    {
        var source = """
            namespace app.modules.foo {
                public class Handler {
                    public char Sep() => System.IO.Path.DirectorySeparatorChar;
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsFalse();
    }

    [Test] public async Task DoesNotFire_InsidePathTypesNamespace()
    {
        var source = """
            namespace app.types.path.test {
                public class FsImpl {
                    public string Read() => System.IO.File.ReadAllText("x.txt");
                }
            }
            """;
        var diags = Run(source, PathTypesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsFalse();
    }

    [Test] public async Task DoesNotFire_OnDataOfPath_InActionHandler()
    {
        // Data<some-non-string> is the correct shape — must not trip PLNG002.
        var source = Stubs + """
            namespace app.types.path { public class @this {} }
            namespace app.modules.foo {
                [app.modules.Action]
                public partial class Handler {
                    public partial app.data.@this<app.types.path.@this> Path { get; init; }
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsFalse();
    }

    [Test] public async Task DiagnosticLocation_UnderlinesOffendingMemberAccess()
    {
        var source = """
            namespace app.modules.foo {
                public class Handler {
                    public string Read() => System.IO.File.ReadAllText("x.txt");
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        var plng = diags.FirstOrDefault(d => d.Id == "PLNG002");
        await Assert.That(plng).IsNotNull();
        var span = plng!.Location.GetLineSpan();
        // Must pin to a real source line, not Location.None.
        await Assert.That(span.Path).IsEqualTo(ModulesPath);
        await Assert.That(span.StartLinePosition.Line).IsGreaterThan(0);
    }
}
