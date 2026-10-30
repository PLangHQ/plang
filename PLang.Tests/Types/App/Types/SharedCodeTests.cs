namespace PLang.Tests.App.Types;

// Root apps made from one code read one scan of its facts; a kind on one of the code's types would be every root's, so
// the shared code refuses it, loudly, and no other root sees it.
public class SharedCodeTests
{
    private sealed class ProbeKind(global::app.type.@this owner) : global::app.type.kind.@this("webpsh", owner);

    // a second root from the same code reads its scan: it allocates the scan's bytes less than a root with a code of
    // its own
    [Test, NotInParallel]
    public async Task ASecondRootFromOneCode_ReadsItsScan()
    {
        var code = new global::app.origin.shared(global::app.Wiring.none);
        await using (new global::app.@this("/shared", code)) { }
        await using (new global::app.@this("/own", wiring: global::app.Wiring.none)) { }

        var from = System.GC.GetAllocatedBytesForCurrentThread();
        await using (new global::app.@this("/shared", code)) { }
        var shared = System.GC.GetAllocatedBytesForCurrentThread() - from;
        from = System.GC.GetAllocatedBytesForCurrentThread();
        await using (new global::app.@this("/own", wiring: global::app.Wiring.none)) { }
        var own = System.GC.GetAllocatedBytesForCurrentThread() - from;

        await Assert.That(own - shared).IsGreaterThan(1_000_000L).Because("a root from a held code doesn't scan it again");
    }

    // a kind on a code type is refused on the shared code, and no other root from it sees it
    [Test]
    public async Task AKindOnASharedCodesType_IsRefused_AndNoOtherRootSeesIt()
    {
        var code = new global::app.origin.shared(global::app.Wiring.none);
        await using var one = new global::app.@this("/one", code);
        await using var two = new global::app.@this("/two", code);
        var image = one.type.list["image"];

        var refused = await Assert.That(() => one.type.list.Add([new ProbeKind(image)])).Throws<global::app.error.AppException>();

        await Assert.That(refused!.Error.Key).IsEqualTo("TypesShared");
        await Assert.That(two.type.list["image"]).IsSameReferenceAs(image).Because("both read the one scan");
        await Assert.That(two.type.list["image"].kind["webpsh"]).IsNull();
    }
}
