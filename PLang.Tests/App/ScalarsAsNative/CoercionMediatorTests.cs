namespace PLang.Tests.App.ScalarsAsNative;

// The one binary-coercion mediator (Operator.NormalizeTypes + Compare dispatcher).
// Post-branch: inspects wrapper types (the second of the two legal type-switch
// sites), not raw CLR. Reconciles "5"==5, numeric widening, date-vs-datetime,
// enum<->string.
public class CoercionMediatorTests
{
    [Test]
    public async Task Mediator_FiveStringEqualsFiveNumber_StillCoerces()
    {
        // "5" == 5 still true after every scalar is wrapped — mediator inspects
        // text vs number and widens through the parser.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Mediator_NumberWidening_IntDecimalDoubleEquivalent()
    {
        // 5L == 5m == 5.0d — wrapper inspection routes through number's existing
        // widening tower (no raw `int` switch survives).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Mediator_EnumString_CoerceOnEqualityAndCompare()
    {
        // An enum-shaped wrapper compares/equals against a text value through
        // the mediator (the one allowed cross-type reconciliation point).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Mediator_DateVsDatetime_ClearCoercionOutcomeNotSilentEqual()
    {
        // A date and a datetime for "the same day" are NOT silently equal —
        // they're a clean coercion outcome the mediator owns. (Pre-branch:
        // ScalarComparer made them equal by folding date into datetime.)
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Mediator_InspectsWrapperTypes_NotRawClr()
    {
        // After the sweep, mediator-internal branches read `a is text`, `b is number`,
        // never `a is string`, `b is int`. A reflection probe records the surface
        // (or an explicit test ensures a raw-CLR scalar can't reach the mediator).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Mediator_BoolAndNullRouteThroughMediatorEquality()
    {
        // bool == bool and null == null routed via the mediator/Compare path, not
        // a `Val(left) is bool b` raw-switch — the dispatch is wrapper-shaped.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
