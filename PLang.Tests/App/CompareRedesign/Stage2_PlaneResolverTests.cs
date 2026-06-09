namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — the two access planes. `.` = data plane (content/keys/elements);
// `!` = property plane (the value's own properties + the envelope, resolved
// chain-wide). The sigil picks the plane, so a content key `size` (`.size`) and
// the value's `size` (`!size`) never collide. Reserved core (`@schema`, `type`,
// `error`, `success`) is protected — a type may not shadow it.
public class Stage2_PlaneResolverTests
{
    [Test]
    public async Task DotPlane_ResolvesDataContent_TypeAnswers()
    {
        // %dict.field% → dict's content via the type's own resolver; no central case-table
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BangPlane_ResolvesPropertyAndEnvelope_TypeAnswers()
    {
        // %list!count%, %text!length% — typed property values; serialised into `properties` bag
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BangType_ReturnsHeadlineType()
    {
        // %x!type% → headline type name (post-narrow: `dict`)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BangTypeList_ReturnsAccumulatedChain_NewestFirst()
    {
        // %x!type.list% post-narrow → [dict, file, item] (newest at index 0)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BangReservedCore_Protected_TypeMayNotShadow()
    {
        // a type declaring a property named `error`/`type`/`success`/`@schema` is rejected
        // (build-time gate or runtime registration check)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task AtSchemaBlocked_AsDictKey_WireMarkerOnly()
    {
        // @schema is the wire marker; cannot be set/read as a dict key (and `@` isn't a legal C# identifier)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task NameField_RemovedFromEnvelope_FreeAsDataKey()
    {
        // envelope no longer carries `name`; %x.name% reads the content's field, nothing to shadow
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BangSize_AndDotSize_AreDistinct_NoShadowing()
    {
        // %dict.size% (content key=10) and %dict!size% (property bag=28) — sigil picks the plane
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
