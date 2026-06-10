using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PLang.Tests.App.CompareRedesign;

// Stage 7 — full public-surface typing + the PLNG003 build gate. A public
// instance member of an `item.@this` subtype returning raw CLR is flagged
// (warning while the surface converts; error once clean). Internal/private
// untouched; statics (catalog conventions, hooks, operators) out of scope;
// the gated per-type interop accessor (`path.Absolute`, internal + Authorize)
// is the standing exemption.
public class Stage7_SurfaceGateTests
{
    // Synthetic-compilation probe — same harness as Plng002SystemIoBanTests.
    private const string ItemStub = """
        namespace app.type.item { public class @this {} }
        namespace app.type.@bool { public class @this : app.type.item.@this {} }
        """;

    private static System.Collections.Immutable.ImmutableArray<Diagnostic> Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Plng003Test",
            new[] { CSharpSyntaxTree.ParseText(ItemStub + source,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: "/workspace/plang/PLang/app/type/probe/this.cs") },
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

    [Test]
    public async Task Gate_PublicItemSubtypeMember_ReturningString_FailsBuild()
    {
        var diags = Run("""
            namespace app.type.probe { public class @this : app.type.item.@this {
                public string Foo => "raw";
            } }
            """);
        await Assert.That(diags.Any(d => d.Id == "PLNG003")).IsTrue();
    }

    [Test]
    public async Task Gate_PublicItemSubtypeMember_ReturningInt_FailsBuild()
    {
        var diags = Run("""
            namespace app.type.probe { public class @this : app.type.item.@this {
                public int Count => 1;
                public long Size() => 2;
                public bool Flag => true;
                public byte[] Bytes() => new byte[0];
                public System.Collections.Generic.Dictionary<string, int> Map => new();
                public System.Collections.Generic.List<string> Names() => new();
            } }
            """);
        await Assert.That(diags.Count(d => d.Id == "PLNG003")).IsEqualTo(6);
    }

    [Test]
    public async Task Gate_IsTruthyReturnsAtBool_PassesGate()
    {
        // a predicate returning the PLang @bool passes; raw bool fires
        var diags = Run("""
            namespace app.type.probe { public class @this : app.type.item.@this {
                public app.type.@bool.@this IsTruthyTyped() => new();
            } }
            """);
        await Assert.That(diags.Any(d => d.Id == "PLNG003")).IsFalse();
    }

    [Test]
    public async Task Gate_InternalPlumbing_IsLeaf_Untouched()
    {
        // public-only scope: internal/private members never fire
        var diags = Run("""
            namespace app.type.probe { public class @this : app.type.item.@this {
                internal bool IsLeafPlumbing => true;
                private string Backing => "x";
                protected int Hops() => 0;
            } }
            """);
        await Assert.That(diags.Any(d => d.Id == "PLNG003")).IsFalse();
    }

    [Test]
    public async Task Gate_GatedInteropAccessor_PathAbsolute_Exempt()
    {
        // the standing exemption is structural: path.Absolute is INTERNAL (the
        // type's gated interop edge), so the public-only gate never sees it
        var prop = typeof(global::app.type.path.@this).GetProperty("Absolute",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetMethod!.IsAssembly).IsTrue();
    }

    [Test]
    public async Task PathAbsolute_PublicSurface_IsPath_NotString()
    {
        // the public projection is `!absolute` on the property plane; the raw
        // string lives at internal .Absolute (no public string member)
        var pub = typeof(global::app.type.path.@this).GetProperty("Absolute",
            BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(pub).IsNull();
    }

    [Test]
    public async Task TextLength_ReturnsNumber_NotInt()
    {
        var t = new global::app.type.text.@this("héllo");
        object length = t.Length;
        await Assert.That(length).IsTypeOf<global::app.type.number.@this>();
        await Assert.That(length.ToString()).IsEqualTo("5");
    }

    [Test]
    public async Task DictKeys_ReturnsListOfText_NotIEnumerableString()
    {
        var d = new global::app.type.dict.@this();
        d.Set(new Data("name", "a"));
        d.Set(new Data("age", 30L));
        object keys = d.Keys;
        await Assert.That(keys).IsTypeOf<global::app.type.list.@this<global::app.type.text.@this>>();
        var names = ((global::app.type.list.@this<global::app.type.text.@this>)keys)
            .Items.Select(k => k.Peek()).ToList();
        await Assert.That(names.All(v => v is global::app.type.text.@this)).IsTrue();
    }

    [Test]
    public async Task ListCount_ReturnsNumber_NotInt()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task FileSize_ReturnsNumber_NotLong()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "plang_st7size_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tmp, "12345");
        try
        {
            var fp = new global::app.type.path.file.@this(tmp);
            object size = fp.Size;
            await Assert.That(size).IsTypeOf<global::app.type.number.@this>();
            await Assert.That(size.ToString()).IsEqualTo("5");
            object fileSize = new global::app.type.file.@this(fp).Size;
            await Assert.That(fileSize).IsTypeOf<global::app.type.number.@this>();
        }
        finally { File.Delete(tmp); }
    }
}
