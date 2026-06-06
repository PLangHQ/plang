using TextV = global::app.type.text.@this;
using NumberV = global::app.type.number.@this;
using BoolV = global::app.type.@bool.@this;
using NullV = global::app.type.@null.@this;
using DateV = global::app.type.date.@this;
using DateTimeV = global::app.type.datetime.@this;
using Compare = global::app.data.Compare;

namespace PLang.Tests.App.ScalarsAsNative;

// The one binary-coercion mediator (Operator.NormalizeTypes + Compare dispatcher).
// Post-branch: inspects wrapper types (the second of the two legal type-switch
// sites), not raw CLR. Reconciles "5"==5, numeric widening, date-vs-datetime,
// enum<->text. Coercion runs BEFORE the value's own self-dispatch so a cross-type
// pair reconciles instead of trivially failing text.AreEqual(number).
public class CoercionMediatorTests
{
    private static Data D(object v) => new("", v);

    [Test]
    public async Task Mediator_FiveStringEqualsFiveNumber_StillCoerces()
    {
        // text "5" == number 5 — the mediator inspects (text, number) and parses
        // the text through the number tower.
        await Assert.That(Compare.AreEqual(D(new TextV("5")), D(NumberV.From(5L)))).IsTrue();
        await Assert.That(Compare.AreEqual(D(NumberV.From(5L)), D(new TextV("5")))).IsTrue();
        await Assert.That(Compare.AreEqual(D(new TextV("6")), D(NumberV.From(5L)))).IsFalse();
    }

    [Test]
    public async Task Mediator_NumberWidening_IntDecimalDoubleEquivalent()
    {
        // 5L == 5m == 5.0d — number.@this widens in its own tower; no raw int switch.
        await Assert.That(Compare.AreEqual(D(NumberV.From(5L)), D(NumberV.From(5m)))).IsTrue();
        await Assert.That(Compare.AreEqual(D(NumberV.From(5L)), D(NumberV.From(5.0d)))).IsTrue();
        await Assert.That(Compare.AreEqual(D(NumberV.From(5m)), D(NumberV.From(5.0d)))).IsTrue();
        await Assert.That(Compare.Order(D(NumberV.From(5L)), D(NumberV.From(6.0d)))).IsLessThan(0);
    }

    [Test]
    public async Task Mediator_EnumString_CoerceOnEqualityAndCompare()
    {
        // An enum value (not a value wrapper — arrives raw) reconciles against a
        // text literal by the enum's name. This is the one allowed cross-type point.
        await Assert.That(Compare.AreEqualValues(global::app.tester.Status.Pass, new TextV("Pass"))).IsTrue();
        await Assert.That(Compare.AreEqualValues(new TextV("Pass"), global::app.tester.Status.Pass)).IsTrue();
        await Assert.That(Compare.AreEqualValues(global::app.tester.Status.Pass, new TextV("Fail"))).IsFalse();
    }

    [Test]
    public async Task Mediator_DateVsDatetime_ClearCoercionOutcomeNotSilentEqual()
    {
        // A date and a datetime for the same day are NOT silently equal — distinct
        // types, no fold. (Pre-branch ScalarComparer folded date into datetime.)
        var date = D(new DateV(new System.DateOnly(2026, 1, 1)));
        var dt = D(new DateTimeV(new System.DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero)));
        await Assert.That(Compare.AreEqual(date, dt)).IsFalse();
    }

    [Test]
    public async Task Mediator_InspectsWrapperTypes_NotRawClr()
    {
        // The coercion is driven by wrapper types: text<->number reconciles in both
        // directions, and number<->number needs no coercion (widens in its tower).
        // A raw-CLR scalar is not what the mediator keys on — the wrappers are.
        var (l, r) = global::app.module.condition.Operator.NormalizeTypes(new TextV("5"), NumberV.From(5L));
        await Assert.That(l).IsTypeOf<NumberV>();   // text coerced to number
        await Assert.That(r).IsTypeOf<NumberV>();
    }

    [Test]
    public async Task Mediator_BoolAndNullRouteThroughMediatorEquality()
    {
        // bool == bool and null == null route via Compare's wrapper-shaped dispatch
        // (IEquatableValue), not a raw `is bool` switch.
        await Assert.That(Compare.AreEqual(D(new BoolV(true)), D(new BoolV(true)))).IsTrue();
        await Assert.That(Compare.AreEqual(D(new BoolV(true)), D(new BoolV(false)))).IsFalse();
        await Assert.That(Compare.AreEqual(Data.Null(), Data.Null())).IsTrue();
        await Assert.That(Compare.AreEqual(D(new BoolV(false)), Data.Null())).IsFalse();
    }
}
