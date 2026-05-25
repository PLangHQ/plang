using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Modules.Test;

/// <summary>
/// Stage 5 — Batch 9. <c>test/discover.cs</c> denial-path tests.
///
/// The brief's concrete offender. Today's discover does hand-rolled
/// <c>StartsWith(rootPrefix)</c> containment + <c>System.IO.Directory.GetFiles</c>;
/// post-migration it routes through <c>rootPath.List(...)</c> + <c>match.ReadText()</c>
/// + <c>goal.PrPath.ReadText()</c>, all AuthGate-fronted.
/// </summary>
public class DiscoverDenialPathTests
{
    [Test] public async Task Discover_WithTestPathOutsideRoot_DenialNotSilentEmpty()
    {
        // --test=/etc → AuthGate denial; must not silently return zero tests.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Discover_WithDotDotTraversal_DeniedByAuthGate()
    {
        // --test=../../.. → AuthGate denial; the hand-rolled StartsWith check is gone.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
