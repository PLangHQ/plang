namespace PLang.Tests.App.ScalarsAsNative;

// null.@this — singleton wrapper for the null *value* (not the absence of a Data).
// Always falsy, null==null true, sorts last, bare `null` on application/json.
// Data.Null() stays the factory and stamps the singleton instance.
public class NullWrapperTests
{
    [Test]
    public async Task Null_IsSingleton_DataNullStampsSameInstance()
    {
        // Two Data.Null() calls carry the same null.@this instance — no per-value
        // allocation; one null in the world.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Null_Truthiness_AlwaysFalsy()
    {
        // IBooleanResolvable returns false. The `if null` and `if !null` paths.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Null_Equality_NullEqualsNullNothingElse()
    {
        // null == null true; null == 0/""/false all false (no truthy collapse).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Null_Compare_SortsLast()
    {
        // Compare.Order(non-null, null) < 0; Compare.Order(null, non-null) > 0.
        // Note: null.@this does NOT implement IOrderableValue — the sort-last
        // policy lives on Compare itself, not on the wrapper.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Null_BareSerialize_NullOnApplicationJson()
    {
        // Normalize emits bare `null`, not `{"value":null}` or any envelope.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Null_IsValueNotAbsence_DataIsInitializedDistinction()
    {
        // A Data carrying null.@this has IsInitialized = true (a present null value).
        // A missing variable / NotFound is IsInitialized = false — a different axis
        // that stays a C# null reference, not null.@this. The bright line guard.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
