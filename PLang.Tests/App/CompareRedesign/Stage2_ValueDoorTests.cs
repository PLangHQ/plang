namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — the typed value door (async, lazy, ValueTask). Sync-completes when
// _value is present; async only when it must read. No public sync .Value; no
// generic ToRaw; value slot is always a typed PLang item. Materialize disappears
// — parse folds into the door. Read fires only behind an await on navigation.
public class Stage2_ValueDoorTests
{
    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-stage2-" + System.Guid.NewGuid().ToString("N")[..8]);
        return new(root);
    }

    // A raw-backed Data (source form pending) straight off the file channel.
    private static async Task<Data> RawBackedJson(global::app.@this app, string root)
    {
        var p = new global::app.type.path.file.@this(System.IO.Path.Combine(root, "cfg.json"), app.User.Context);
        await (await p.WriteText("{\"port\":8080}")).IsSuccess();
        return await new global::app.channel.type.file.@this(p).Read();
    }

    [Test, Skip("Born-typed store-seam stage (slices 1-2) — see .bot/compare-redesign/coder/stage-proposal-born-typed.md; this stub is the pinned contract")]
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
        var d = new Data("x", 42);
        var vt = d.Value();
        await Assert.That(vt.IsCompletedSuccessfully).IsTrue();
        await Assert.That((await vt)).IsEqualTo(42);
    }

    [Test]
    public async Task Value_PendingSource_LoadsAsync_ReadFiresExactlyOnce()
    {
        // pending file source: first Value() awaits read; second Value() returns cached
        // — MaterializeCount transitions 0 → 1 → 1
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
        var first = await d.Value();
        await Assert.That(d.MaterializeCount).IsEqualTo(1);
        var second = await d.Value();
        await Assert.That(d.MaterializeCount).IsEqualTo(1);   // parse fired exactly once, cached
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task Peek_OnUnmaterialisedReference_ReturnsCurrentRung_DoesNotForceParse()
    {
        // Peek() returns the binary/text rung without triggering the source read
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        var rung = d.Peek();
        await Assert.That(rung).IsEqualTo("{\"port\":8080}");   // the text rung, unparsed
        await Assert.That(d.MaterializeCount).IsEqualTo(0);       // Peek forced nothing
    }

    [Test, Skip("Born-typed store-seam stage (slices 1-2) — see .bot/compare-redesign/coder/stage-proposal-born-typed.md; this stub is the pinned contract")]
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
        var prop = typeof(Data).GetProperty("Value",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(prop).IsNull();
        var method = typeof(Data).GetMethod("Value", System.Type.EmptyTypes);
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.ValueTask<object?>));
    }

    [Test, Skip("Born-typed store-seam stage (slices 1-2) — see .bot/compare-redesign/coder/stage-proposal-born-typed.md; this stub is the pinned contract")]
    public async Task GenericToRaw_DoesNotExist_OnItemBase()
    {
        // reflection: item.@this has no public ToRaw(); raw leaves only via Write/As<T>/gated interop
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test, Skip("Born-typed store-seam stage (slices 1-2) — see .bot/compare-redesign/coder/stage-proposal-born-typed.md; this stub is the pinned contract")]
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
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        _ = d.ToString();
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    [Test]
    public async Task EqualsAndGetHashCode_OnUnmaterialisedReference_DoNotRead()
    {
        // MaterializeCount=0 before/after data.Equals(other) and data.GetHashCode()
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        _ = d.Equals(new Data("y", 1));
        _ = d.GetHashCode();
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    [Test, Skip("Born-typed store-seam stage (slices 1-2) — see .bot/compare-redesign/coder/stage-proposal-born-typed.md; this stub is the pinned contract")]
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
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        var v = await d.Value();
        await Assert.That(v is global::app.type.dict.@this).IsTrue();
    }

    [Test, Skip("Born-typed store-seam stage (slices 1-2) — see .bot/compare-redesign/coder/stage-proposal-born-typed.md; this stub is the pinned contract")]
    public async Task DataType_Getter_ReturnsBackingField_NoCLRSniffing()
    {
        // data.Type is `return _type;` — the lazy leaf.ToRaw().GetType() + name-mapping
        // + kind-stamping block is deleted. Stamped at construction from the typed value.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
