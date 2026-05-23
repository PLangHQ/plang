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
    [Test]
    public async Task CodeAttribute_TypeExistsInAppModulesNamespace()
    {
        // app.modules.CodeAttribute must exist. Test by name to catch namespace
        // moves; the source generator and Describe() both look for this exact
        // attribute (see PLang/app/modules/this.cs Describe()).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ProviderAttribute_DoesNotExistAnymore()
    {
        // Guard against reintroduction. No public type named ProviderAttribute
        // inside the app.modules namespace of the PLang assembly.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task PLNG001DiagnosticText_ReferencesCodeNotProvider()
    {
        // The PLNG001 build-warning text in the source generator must mention
        // [Code] (not [Provider]). Authors triggered by PLNG001 need to be
        // taught the current attribute name, not a removed one.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
