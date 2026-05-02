namespace PLang.Tests.App.CallStack;

// Call.@this exposes a typed extension bag (Get<T>/Set<T>) for handlers to attach metadata
// (cache info, http status, llm tokens, schedule identity, etc.).
public class ItemsExtensionTests
{
    [Test]
    public async Task Get_BeforeSet_ReturnsNull()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Set_ThenGet_ReturnsSameInstance()
    {
        // ReferenceEquals — bag stores the instance, not a copy.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Set_DifferentTypes_CoexistInBag()
    {
        // Set<A>(...) and Set<B>(...) both retrievable independently.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Set_SameType_OverwritesPrevious()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Items_LazyAllocated_NotCreatedUntilFirstSet()
    {
        // Internal dict not allocated until first Set call (memory discipline for hot path).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
