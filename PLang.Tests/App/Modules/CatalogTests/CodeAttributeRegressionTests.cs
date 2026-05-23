using System.Linq;
using System.Reflection;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Opportunistic [Provider] → [Code] rename (architect plan, "Opportunistic
/// rename" section). Mechanical; tested as a regression guard so the old name
/// can't slip back in via copy-paste from older branches.
///
/// Two anchors:
///   1. The attribute type lives at the documented [Code] name and not
///      [Provider]. If [Provider] ever reappears as a public type in the
///      PLang assembly, the test fails — it would mean someone re-added the
///      old name.
///   2. The PLNG001 diagnostic text references [Code], not [Provider].
///      The source generator is what authors will see in the build output,
///      and the text must teach the new name.
/// </summary>
public class CodeAttributeRegressionTests
{
    private static Assembly PLangAssembly =>
        typeof(global::app.modules.@this).Assembly;

    [Test]
    public async Task CodeAttribute_TypeExistsInAppModulesNamespace()
    {
        var type = PLangAssembly.GetType("app.modules.CodeAttribute", throwOnError: false);
        await Assert.That(type).IsNotNull();
        await Assert.That(typeof(Attribute).IsAssignableFrom(type!)).IsTrue();
    }

    [Test]
    public async Task ProviderAttribute_DoesNotExistAnymore()
    {
        // Guard against reintroduction. No public type literally named
        // ProviderAttribute anywhere in the app.modules namespace tree.
        var bad = PLangAssembly.GetTypes()
            .Where(t => t.IsPublic
                     && t.Namespace != null
                     && t.Namespace.StartsWith("app.modules", StringComparison.Ordinal)
                     && t.Name == "ProviderAttribute")
            .ToList();
        await Assert.That(bad.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PLNG001DiagnosticText_ReferencesCodeNotProvider()
    {
        // Source-generator file is build-time only and not loaded into the
        // PLang runtime assembly; verify its text by reading the source file.
        // The diagnostic title + messageFormat live in PLang.Generators/Discovery/this.cs.
        var generatorSource = LocateGeneratorSource();
        var text = File.ReadAllText(generatorSource);

        // The current [Code] name must appear in the diagnostic text.
        await Assert.That(text).Contains("[Code]");
        // The old [Provider] name must NOT appear anywhere in the generator
        // source — same guard as the runtime, applied to the build text.
        await Assert.That(text).DoesNotContain("[Provider]");
        await Assert.That(text).DoesNotContain("ProviderAttribute");
    }

    private static string LocateGeneratorSource()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang.Generators")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) throw new InvalidOperationException("repo root not found");
        return Path.Combine(dir, "PLang.Generators", "Discovery", "this.cs");
    }
}
