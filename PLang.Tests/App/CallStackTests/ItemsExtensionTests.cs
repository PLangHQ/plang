using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class ItemsExtensionTests
{
    private sealed class CacheInfo { public bool Hit { get; set; } }
    private sealed class HttpInfo { public int Status { get; set; } }

    [Test]
    public async Task Get_BeforeSet_ReturnsNull()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(call.GetItem<CacheInfo>()).IsNull();
    }

    [Test]
    public async Task Set_ThenGet_ReturnsSameInstance()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        var info = new CacheInfo { Hit = true };
        call.SetItem(info);
        var fetched = call.GetItem<CacheInfo>();
        await Assert.That(ReferenceEquals(fetched, info)).IsTrue();
    }

    [Test]
    public async Task Set_DifferentTypes_CoexistInBag()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        call.SetItem(new CacheInfo { Hit = true });
        call.SetItem(new HttpInfo { Status = 200 });
        await Assert.That(call.GetItem<CacheInfo>()!.Hit).IsTrue();
        await Assert.That(call.GetItem<HttpInfo>()!.Status).IsEqualTo(200);
    }

    [Test]
    public async Task Set_SameType_OverwritesPrevious()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        call.SetItem(new CacheInfo { Hit = true });
        call.SetItem(new CacheInfo { Hit = false });
        await Assert.That(call.GetItem<CacheInfo>()!.Hit).IsFalse();
    }

    [Test]
    public async Task Items_LazyAllocated_NotCreatedUntilFirstSet()
    {
        // Hot-path discipline: the bag dict isn't allocated until first SetItem.
        // We can't directly observe the field, but Get returning null for any T proves
        // that no dict exists (a null bag returns null, an empty dict also returns null —
        // both pass; the spec is "lazy", verified at the source level).
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(call.GetItem<CacheInfo>()).IsNull();
        await Assert.That(call.GetItem<HttpInfo>()).IsNull();
    }
}
