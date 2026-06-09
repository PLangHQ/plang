namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — the typed value door (async, lazy, ValueTask). Sync-completes when
// _value is present; async only when it must read. No public sync .Value; no
// generic ToRaw; value slot is always a typed PLang item. Materialize disappears
// — parse folds into the door. Read fires only behind an await on navigation.
public class Stage2_ValueDoorTests
{
    [Test]
    public async Task Value_AuthoredScalar_ReturnsTypedNumberNotRawInt()
    {
        // set %x% = 5 → await data.Value() returns a `number` (item subtype), not boxed int 5
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Value_PresentBacking_CompletesSynchronously_NoAsyncHop()
    {
        // ValueTask.IsCompletedSuccessfully true when _value already materialised; zero alloc
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Value_PendingSource_LoadsAsync_ReadFiresExactlyOnce()
    {
        // pending file source: first Value() awaits read; second Value() returns cached
        // — MaterializeCount transitions 0 → 1 → 1
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Peek_OnUnmaterialisedReference_ReturnsCurrentRung_DoesNotForceParse()
    {
        // Peek() returns the binary/text rung without triggering the source read
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task RawSlot_Dissolved_BareBytesOffChannelRefineInPlace()
    {
        // no _raw field on Data; a bare-bytes value narrows binary → item through the same instance
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PublicSyncValueProperty_DoesNotExist_OnDataType()
    {
        // reflection: typeof(Data).GetProperty("Value") must not exist as a public sync accessor;
        // only `ValueTask<object?> Value()` method remains
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GenericToRaw_DoesNotExist_OnItemBase()
    {
        // reflection: item.@this has no public ToRaw(); raw leaves only via Write/As<T>/gated interop
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TextRawValue_IsPrivate_NotPublicProperty()
    {
        // reflection: text.@this has no public `string Value` property — backing is private,
        // emitted only through text.Write(IWriter)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ToString_OnUnmaterialisedReference_DoesNotNavigate_DoesNotRead()
    {
        // MaterializeCount=0 before and after data.ToString(); ToString reads the
        // already-materialised backing only — sync framework method must not enter the async chain
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task EqualsAndGetHashCode_OnUnmaterialisedReference_DoNotRead()
    {
        // MaterializeCount=0 before/after data.Equals(other) and data.GetHashCode()
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task VarReference_RidesAsTypedText_NeverBareCSharpString()
    {
        // SetValue("%x%") + downstream read yields a `text` instance — value slot is never a raw System.String
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task JsonContainer_RidesAsTypedDictOrList_NeverBareCSharpDictionary()
    {
        // JSON ingestion yields a native PLang dict/list — value slot is never a raw Dictionary<string,object>
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task DataType_Getter_ReturnsBackingField_NoCLRSniffing()
    {
        // data.Type is `return _type;` — the lazy leaf.ToRaw().GetType() + name-mapping
        // + kind-stamping block is deleted. Stamped at construction from the typed value.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
