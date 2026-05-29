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
/// path-named properties under <c>PLang/app/**</c>. Two narrow carve-outs:
/// <list type="bullet">
///   <item><c>System.IO.Path.*</c> (pure name math) is allowed only from
///   <c>PLang/app/Utils/PathHelper.cs</c> — the single forwarder.</item>
///   <item><c>System.IO.File/Directory/FileInfo/FileStream/...</c> (actual
///   IO) is allowed only under <c>PLang/app/types/path/**</c> — the gated
///   verb surface.</item>
/// </list>
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
    private const string PathHelperPath = "/workspace/plang/PLang/app/Utils/PathHelper.cs";

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

    [Test] public async Task Fires_OnSystemIoPathDirectorySeparatorChar_UnderModulesNamespace()
    {
        // Separator constants are not allowlisted at the symbol level — like
        // any other System.IO.Path.* member, they must route through
        // PathHelper. Type-based exemption, not symbol-name allowlist.
        var source = """
            namespace app.modules.foo {
                public class Handler {
                    public char Sep() => System.IO.Path.DirectorySeparatorChar;
                }
            }
            """;
        var diags = Run(source, ModulesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task Fires_OnSystemIoPath_InsidePathTypesNamespace()
    {
        // System.IO.Path.* under path-types still fires — Path.* is allowed
        // ONLY from PathHelper, not under path-types. The path-types carve-out
        // covers File/Directory/FileInfo/Stream, not name math.
        var source = """
            namespace app.type.path.test {
                public class Impl {
                    public string Join() => System.IO.Path.Combine("a", "b");
                }
            }
            """;
        var diags = Run(source, PathTypesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task DoesNotFire_OnSystemIoFile_InsidePathTypesNamespace()
    {
        // File.* under path-types stays exempt — the verb surface owns IO.
        var source = """
            namespace app.type.path.test {
                public class FsImpl {
                    public string Read() => System.IO.File.ReadAllText("x.txt");
                }
            }
            """;
        var diags = Run(source, PathTypesPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsFalse();
    }

    [Test] public async Task DoesNotFire_OnSystemIoPath_InsidePathHelper()
    {
        // PathHelper is the single allowed bridge to System.IO.Path.*; its
        // own body necessarily imports those members.
        var source = """
            namespace app.Utils {
                internal static class PathHelper {
                    public static string Combine(string a, string b) => System.IO.Path.Combine(a, b);
                    public static char Sep => System.IO.Path.DirectorySeparatorChar;
                }
            }
            """;
        var diags = Run(source, PathHelperPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsFalse();
    }

    [Test] public async Task Fires_OnSystemIoFile_InsidePathHelper()
    {
        // PathHelper is name-math only. An addition that reaches actual IO
        // must fire — guards against future "PathHelper.ReadAllText" drift.
        var source = """
            namespace app.Utils {
                internal static class PathHelper {
                    public static string Read(string p) => System.IO.File.ReadAllText(p);
                }
            }
            """;
        var diags = Run(source, PathHelperPath);
        await Assert.That(diags.Any(d => d.Id == "PLNG002")).IsTrue();
    }

    [Test] public async Task DoesNotFire_OnDataOfPath_InActionHandler()
    {
        // Data<some-non-string> is the correct shape — must not trip PLNG002.
        var source = Stubs + """
            namespace app.type.path { public class @this {} }
            namespace app.modules.foo {
                [app.modules.Action]
                public partial class Handler {
                    public partial app.data.@this<app.type.path.@this> Path { get; init; }
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
