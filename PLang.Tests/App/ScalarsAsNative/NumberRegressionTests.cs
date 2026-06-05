namespace PLang.Tests.App.ScalarsAsNative;

// number.@this is the reference-shape wrapper (already complete pre-branch).
// After it becomes `: item.@this`, its behavior must be unchanged — these
// tests pin "no regression on the worked example."
public class NumberRegressionTests
{
    [Test]
    public async Task Number_Arithmetic_UnchangedUnderItemInheritance()
    {
        // 1 + 1, 2 * 3, 10 / 2 — through the same Number.@this paths as before.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Number_Compare_UnchangedUnderItemInheritance()
    {
        // Order(1, 2) < 0, AreEqual(5, 5) true — IOrderableValue/IEquatableValue
        // dispatch still routes; `item` adds nothing to ordering.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Number_Truthiness_ZeroFalsyNonZeroTruthy()
    {
        // Routed through `item`'s sync truthiness path (no async hop for a hot if %n%).
        // 0 falsy; 1 truthy; -1 truthy.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
