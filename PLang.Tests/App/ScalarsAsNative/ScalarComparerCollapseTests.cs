using System.Reflection;
using TextV = global::app.type.text.@this;
using NumberV = global::app.type.number.@this;
using BoolV = global::app.type.@bool.@this;
using NullV = global::app.type.@null.@this;
using Compare = global::app.data.Compare;

namespace PLang.Tests.App.ScalarsAsNative;

// After all scalars flow native, ScalarComparer's per-type arms are unreachable.
// The class collapses to coercion + a thin IComparable fallback. Compare's
// IOrderableValue/IEquatableValue dispatch already routes every wrapper to self.
public class ScalarComparerCollapseTests
{
    private static System.Type SC =>
        typeof(global::app.data.@this).Assembly.GetType("app.data.ScalarComparer")!;

    private static Data D(object v) => new("", v);

    [Test]
    public async Task ScalarComparer_NameSwitch_IsGone()
    {
        // The per-type Name() switch ("number"/"text"/"datetime"/...) is deleted —
        // naming is on the wrapper now.
        await Assert.That(SC).IsNotNull();
        var name = SC.GetMethod("Name", BindingFlags.NonPublic | BindingFlags.Static);
        await Assert.That(name).IsNull();
    }

    [Test]
    public async Task ScalarComparer_IsDateTimeToOffset_AreGone()
    {
        // The DateOnly/DateTimeOffset/DateTime swallowing arms (IsDateTime, ToOffset)
        // that drove the date→datetime collapse are deleted.
        await Assert.That(SC.GetMethod("IsDateTime", BindingFlags.NonPublic | BindingFlags.Static)).IsNull();
        await Assert.That(SC.GetMethod("ToOffset", BindingFlags.NonPublic | BindingFlags.Static)).IsNull();
    }

    [Test]
    public async Task Compare_OrderText_RoutesViaIOrderableValue_NotScalarComparer()
    {
        // text owns its order (IOrderableValue), so Order(text, text) self-dispatches;
        // ScalarComparer is never the decider for two wrapped values.
        await Assert.That(new TextV("a") is global::app.data.IOrderableValue).IsTrue();
        await Assert.That(Compare.Order(D(new TextV("a")), D(new TextV("b")))).IsLessThan(0);
        await Assert.That(Compare.Order(D(new TextV("b")), D(new TextV("a")))).IsGreaterThan(0);
        // ordinal-case-insensitive, matching the historical ScalarComparer string policy
        await Assert.That(Compare.Order(D(new TextV("abc")), D(new TextV("ABC")))).IsEqualTo(0);
    }

    [Test]
    public async Task ToBoolean_RawScalarFallbacks_AreUnreachableForWrappedValues()
    {
        // A wrapped value reports truthiness via IBooleanResolvable (item) BEFORE the
        // raw `is string ""` / `is bool` fallbacks — those remain only for a perimeter.
        await Assert.That(D(new TextV("")).ToBoolean()).IsFalse();   // empty text falsy
        await Assert.That(D(new TextV("x")).ToBoolean()).IsTrue();
        await Assert.That(D(new BoolV(false)).ToBoolean()).IsFalse();
        await Assert.That(D(new BoolV(true)).ToBoolean()).IsTrue();
        await Assert.That(D(NumberV.From(0L)).ToBoolean()).IsFalse();
        await Assert.That(D(NumberV.From(3L)).ToBoolean()).IsTrue();
        await Assert.That(D(NullV.Instance).ToBoolean()).IsFalse();
    }
}
