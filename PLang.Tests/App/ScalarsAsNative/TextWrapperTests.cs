namespace PLang.Tests.App.ScalarsAsNative;

// text.@this is the canonical wrapper for textual content; backs a CLR string.
// After build-out it carries ops, ordinal compare, value-equality + GetHashCode,
// IBooleanResolvable, a bare serializer, and atomicity (NOT IEnumerable as chars).
public class TextWrapperTests
{
    [Test]
    public async Task Text_Length_ReturnsCodepointCount()
    {
        // %s.length% via a method on the wrapper, not via raw string fallback.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_CaseAndTrim_ReturnTextNotRawString()
    {
        // upper/lower/trim return text.@this, not raw string — flow stays native.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_ContainsStartsEndsWith_BehavioralOps()
    {
        // contains/startsWith/endsWith on the wrapper; case policy documented.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_SubstringAndSplit_BehavioralOps()
    {
        // substring(start, len) and split(sep) return wrappers (text / list of text).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_Order_OrdinalCompare()
    {
        // text.@this implements IOrderableValue; "a" < "b" ordinally, settled case policy.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_Equality_TwoSameValueAreEqualAndHashEqual()
    {
        // text("a") and text("a") are Equal AND share GetHashCode — usable as a
        // dict key and inside a HashSet without surprise.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_Equality_RawStringAndTextResolveConsistentlyInHashSet()
    {
        // Mid-migration aliasing guard: the implicit text↔string operator compiles
        // but does NOT make a raw string "a" hash-equal to a text.@this("a").
        // A HashSet (or list-element dedup) populated across a not-yet-swept window
        // must not silently miss matches. Stage-2 bound: construction flip + sweep
        // land together for text.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_Truthiness_EmptyFalsyNonEmptyTruthy()
    {
        // text("") falsy via IBooleanResolvable; text("x") truthy. Sync path reachable
        // through Data.ToBoolean() — no async hop for the hot if %s%.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Text_IsNotIEnumerable_ForeachDoesNotCharIterate()
    {
        // text.@this must not implement IEnumerable (chars). The Data
        // IsPlangAssignable/IsPlangIterable carve-out that exempts raw `string`
        // extends to text.@this — confirmed by reflection + a Data.IsIterable probe.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
