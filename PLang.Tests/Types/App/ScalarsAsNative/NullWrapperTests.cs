using NullV = global::app.type.item.@null.@this;

using PLang.Tests.App.Fixtures;

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
        // Data.Null() carries the singleton; Compare's null policy coalesces it to
        // a C# null and sorts it last (the policy lives on Compare, not the wrapper).
        var present = new Data("", global::app.type.number.@this.From(5L));
        var nul = Data.Null();
        await Assert.That(CompareTestOps.OrdD(present, nul)).IsLessThan(0);
        await Assert.That(CompareTestOps.OrdD(nul, present)).IsGreaterThan(0);
        await Assert.That(CompareTestOps.OrdD(nul, Data.Null())).IsEqualTo(0);
    }

    [Test]
    public async Task Null_IsValueNotAbsence_DataIsInitializedDistinction()
    {
        // A Data carrying null.@this is a PRESENT null — IsInitialized = true and
        // its Value is the singleton. A missing var (NotFound/Uninitialized) is the
        // ABSENCE of a value — IsInitialized = false. Two different axes.
        var presentNull = Data.Null();
        await Assert.That(presentNull.IsInitialized).IsTrue();
        await Assert.That(ReferenceEquals((presentNull.Peek()), NullV.Instance)).IsTrue();

        var missing = Data.NotFound("x");
        await Assert.That(missing.IsInitialized).IsFalse();
    }
}
