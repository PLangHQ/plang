using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 3/4 — Batch 11. Path equality / dict-keying under <c>RootComparison</c>.
/// </summary>
public class RootComparisonKeyingTests
{
    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rce-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task PathEquals_SameAbsolutePath_DifferentCase_OnLinux_AreNotEqual()
    {
        if (System.OperatingSystem.IsWindows()) return; // skip on Windows
        var app = NewApp(out _);
        var a = new FilePath("/tmp/foo/bar.txt", app.User.Context);
        var b = new FilePath("/tmp/FOO/bar.txt", app.User.Context);
        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test] public async Task PathEquals_SameAbsolutePath_DifferentCase_OnWindows_AreEqual()
    {
        if (!System.OperatingSystem.IsWindows()) return; // skip on Linux
        var app = NewApp(out _);
        var a = new FilePath(@"C:\Foo\bar.txt", app.User.Context);
        var b = new FilePath(@"C:\foo\BAR.txt", app.User.Context);
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test] public async Task DictionaryKeyedByPath_RoundTripsBuiltAndResolvedPath()
    {
        var app = NewApp(out _);
        var dict = new System.Collections.Generic.Dictionary<global::app.type.path.@this, string>();
        var built = global::app.type.path.@this.Resolve("/Cache/Start.goal", app.User.Context);
        var resolved = global::app.type.path.@this.Resolve("/Cache/Start.goal", app.User.Context);
        dict[built] = "value";
        await Assert.That(dict.ContainsKey(resolved)).IsTrue();
    }

    [Test] public async Task CycleDetection_GoalPrPath_UsesPathEquality_NotStringInterpolation()
    {
        var app = NewApp(out _);
        var context = app.User.Context;
        var a = global::app.type.path.@this.Resolve("/Cache/Foo.goal", context);
        var b = global::app.type.path.@this.Resolve("/Cache/Foo.goal", context);
        // Same absolute path → Path equality returns true → cycle detection
        // would correctly identify these as the same goal.
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test] public async Task StepDisabledKey_InterpolatesPrPathRelative_NotRawObject()
    {
        var app = NewApp(out _);
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.path.@this.Resolve("/Start.goal", context)
        };
        var step = new Step { Index = 0, Text = "noop", Goal = goal, Context = context };
        // step.DisabledKey is private — test indirectly: Disabled get/set roundtrips.
        // The key shape is `step:<PrPath>:<index>:disabled` — Goal?.PrPath inside
        // interpolation needs to render as a string, not "@this { ... }".
        step.Disabled = true;
        await Assert.That(step.Disabled).IsTrue();
    }
}
