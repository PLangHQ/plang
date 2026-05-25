using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 3/4 — Batch 11. Path equality / dict-keying under <c>RootComparison</c>.
///
/// <c>Path.Equals</c> / <c>GetHashCode</c> use <c>_absolutePath</c> with
/// <c>RootComparison</c> (Windows: OrdinalIgnoreCase, Linux: Ordinal).
/// Today's cycle-detection string compare in <c>callstack.call</c> uses
/// <c>OrdinalIgnoreCase</c> unconditionally — on Linux this is a behaviour
/// change (Linux IS case-sensitive at the FS layer, so the new behaviour
/// is actually a bug fix, but it must be pinned with a test).
/// </summary>
public class RootComparisonKeyingTests
{
    [Test] public async Task PathEquals_SameAbsolutePath_DifferentCase_OnLinux_AreNotEqual()
    {
        // Linux: /tmp/foo and /tmp/FOO are distinct paths. RootComparison = Ordinal.
        // Skipped on Windows.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task PathEquals_SameAbsolutePath_DifferentCase_OnWindows_AreEqual()
    {
        // Windows: C:\Foo and C:\foo are equal. RootComparison = OrdinalIgnoreCase.
        // Skipped on Linux.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task DictionaryKeyedByPath_RoundTripsBuiltAndResolvedPath()
    {
        // _goals dict keyed by Path: a Path built from JSON and a Path resolved at
        // runtime hash and equate identically, so the dict hits on both.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task CycleDetection_GoalPrPath_UsesPathEquality_NotStringInterpolation()
    {
        // callstack.call cycle check compares Path (not Path.Relative). Two paths
        // that differ only by trailing separator or normalization should not
        // create false cycles.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task StepDisabledKey_InterpolatesPrPathRelative_NotRawObject()
    {
        // step.DisabledKey cache key interpolates Goal?.PrPath?.Relative; verify
        // the key shape stays stable (string), since downstream caches key on it.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
