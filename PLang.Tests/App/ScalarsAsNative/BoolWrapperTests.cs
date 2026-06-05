using Bool = global::app.type.@bool.@this;

namespace PLang.Tests.App.ScalarsAsNative;

// bool.@this — the truthiness primitive. Wraps a raw bool; this is where the
// IBooleanResolvable turtles stop. Equality-only (Order throws). OwnedClrTypes = bool.
public class BoolWrapperTests
{
    [Test]
    public async Task Bool_WrapsRawBool_AsBooleanAsyncBottomsOutAtValue()
    {
        await Assert.That(await new Bool(true).AsBooleanAsync()).IsTrue();
        await Assert.That(await new Bool(false).AsBooleanAsync()).IsFalse();
        await Assert.That(new Bool(true).IsTruthy()).IsTrue();
        await Assert.That(new Bool(false).IsTruthy()).IsFalse();
    }

    [Test]
    public async Task Bool_Equality_TrueEqualsTrueAndHashEqual()
    {
        await Assert.That(new Bool(true).Equals(new Bool(true))).IsTrue();
        await Assert.That(new Bool(true).Equals(new Bool(false))).IsFalse();
        await Assert.That(new Bool(true).GetHashCode()).IsEqualTo(new Bool(true).GetHashCode());
    }

    [Test]
    public async Task Bool_BareSerialize_TrueFalseOnApplicationJson()
    {
        // Bare lowercase true/false form.
        await Assert.That(new Bool(true).ToString()).IsEqualTo("true");
        await Assert.That(new Bool(false).ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Bool_OwnsClrBool_RegisteredInOwnedClrTypes()
    {
        await Assert.That(Bool.OwnedClrTypes.Any(o => o.Clr == typeof(bool))).IsTrue();
    }

    [Test]
    public async Task Bool_NotOrderable_OrderThrows()
    {
        // bool.@this does NOT implement IOrderableValue; Compare.Order throws.
        await Assert.That(typeof(global::app.data.IOrderableValue).IsAssignableFrom(typeof(Bool))).IsFalse();
        await Assert.That(() => global::app.data.Compare.Order(new Data("", new Bool(true)), new Data("", new Bool(false))))
            .Throws<global::app.data.Compare.NotOrderableException>();
    }
}
