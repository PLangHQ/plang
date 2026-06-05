using NullV = global::app.type.@null.@this;

namespace PLang.Tests.App.ScalarsAsNative;

// null.@this — singleton wrapper for the null *value* (not the absence of a Data).
// Always falsy, null==null true, equality-only, bare `null` on application/json.
// The Data.Null()-stamps-singleton + sort-last-via-Compare integration lands with
// the construction flip (the final coordinated pass), since it touches Data's
// `_value == null` value-switches across Compare/Normalize/ToBoolean.
public class NullWrapperTests
{
    [Test]
    public async Task Null_IsSingleton_DataNullStampsSameInstance()
    {
        // One null in the world — Instance is process-wide and the ctor is private.
        await Assert.That(ReferenceEquals(NullV.Instance, NullV.Instance)).IsTrue();
        var ctors = typeof(NullV).GetConstructors(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(ctors.Length).IsEqualTo(0); // no public ctor — singleton only
    }

    [Test]
    public async Task Null_Truthiness_AlwaysFalsy()
    {
        await Assert.That(NullV.Instance.IsTruthy()).IsFalse();
        await Assert.That(await NullV.Instance.AsBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task Null_Equality_NullEqualsNullNothingElse()
    {
        // null == null true; null == 0/""/false all false (no truthy collapse).
        await Assert.That(NullV.Instance.AreEqual(NullV.Instance)).IsTrue();
        await Assert.That(NullV.Instance.AreEqual(null)).IsTrue();
        await Assert.That(NullV.Instance.AreEqual(0L)).IsFalse();
        await Assert.That(NullV.Instance.AreEqual("")).IsFalse();
        await Assert.That(NullV.Instance.AreEqual(false)).IsFalse();
    }

    [Test]
    public async Task Null_BareSerialize_NullOnApplicationJson()
    {
        // Bare `null` form, not an envelope.
        await Assert.That(NullV.Instance.ToString()).IsEqualTo("null");
    }

    [Test]
    public async Task Null_Compare_SortsLast()
    {
        // Deferred to the construction flip: Compare.Order(non-null, null) < 0 and
        // (null, non-null) > 0 once Data.Null() carries the singleton and Compare's
        // null policy recognises it. (Today the policy keys on a C# null _value.)
        await Task.CompletedTask;
        Assert.Fail("Not implemented — lands with the construction flip");
    }

    [Test]
    public async Task Null_IsValueNotAbsence_DataIsInitializedDistinction()
    {
        // Deferred to the construction flip: a Data carrying null.@this has
        // IsInitialized = true (present null) while a missing var is IsInitialized
        // = false (a C# null data reference). The bright-line guard binds once
        // Data.Null() stamps the singleton.
        await Task.CompletedTask;
        Assert.Fail("Not implemented — lands with the construction flip");
    }
}
