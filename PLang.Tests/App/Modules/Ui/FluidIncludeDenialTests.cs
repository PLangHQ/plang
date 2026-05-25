using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Modules.Ui;

/// <summary>
/// Stage 5 — Batch 9. <c>ui/code/Fluid.cs</c> include-denial tests.
///
/// <c>PlangFileInfo</c> gains a <c>Path</c> field instead of a string; the
/// Fluid IFileProvider's <c>Read()</c> calls <c>path.ReadText()</c>. An
/// <c>{% include %}</c> pointing outside the template root must be denied.
/// </summary>
public class FluidIncludeDenialTests
{
    [Test] public async Task FluidInclude_TemplateOutsideRoot_DeniedByAuthGate()
    {
        // {% include '../../../etc/passwd' %} → AuthGate denies; render fails cleanly.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task FluidInclude_InRootTemplate_RendersSilently()
    {
        // In-root include → no Ask, template content inlined.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
