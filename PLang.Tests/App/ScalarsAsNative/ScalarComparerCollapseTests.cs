namespace PLang.Tests.App.ScalarsAsNative;

// After all scalars flow native, ScalarComparer's per-type arms are unreachable.
// The class collapses to coercion + a thin IComparable fallback. Compare's
// IOrderableValue/IEquatableValue dispatch already routes every wrapper to self.
public class ScalarComparerCollapseTests
{
    [Test]
    public async Task ScalarComparer_NameSwitch_IsGone()
    {
        // The per-type Name() switch ("number"/"text"/"datetime"/"duration"/...) is
        // deleted — naming is on the wrapper now. Reflection-probe absence.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ScalarComparer_IsDateTimeToOffset_AreGone()
    {
        // The DateOnly/DateTimeOffset/DateTime swallowing arms (`IsDateTime`, `ToOffset`)
        // that drove the date→datetime collapse are deleted.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Compare_OrderText_RoutesViaIOrderableValue_NotScalarComparer()
    {
        // Order(text, text) goes through text.@this's IOrderableValue impl;
        // ScalarComparer is never reached for two wrapped values.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ToBoolean_RawScalarFallbacks_AreUnreachableForWrappedValues()
    {
        // The `is string ""`, `is bool`, null→false fallbacks remain only for the
        // perimeter; a wrapped value (text/bool/null) reports via IBooleanResolvable
        // before the fallback fires. Pin with a Data wrapping each wrapper.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
