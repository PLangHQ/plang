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

    [Test]
    public async Task Value_AuthoredScalar_ReturnsTypedNumberNotRawInt()
    {
        // set %x% = 5 → await data.Value() returns a `number` (item subtype), not boxed int 5
        var d = new Data("x", 5);
        var v = await d.Value();
        await Assert.That(v is global::app.type.number.@this).IsTrue();
        await Assert.That(((global::app.type.number.@this)v!).Clr<object>()).IsEqualTo(5);
    }

    [Test]
    public async Task Value_PresentBacking_CompletesSynchronously_NoAsyncHop()
    {
        // ValueTask.IsCompletedSuccessfully true when _value already materialised; zero alloc
        var d = new Data("x", 42);
        var vt = d.Value();
        await Assert.That(vt.IsCompletedSuccessfully).IsTrue();
        await Assert.That((await vt)?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Value_PendingSource_LoadsAsync_ReadFiresExactlyOnce()
    {
        // pending file source: first Value() awaits read; second Value() returns cached
        // — MaterializeCount transitions 0 → 1 → 1
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
        var first = await d.Value();
        await Assert.That(d.MaterializeCount()).IsEqualTo(1);
        var second = await d.Value();
        await Assert.That(d.MaterializeCount()).IsEqualTo(1);   // parse fired exactly once, cached
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task RawSlot_Dissolved_BareBytesOffChannelRefineInPlace()
    {
        // no _raw field on Data — the undecoded source form lives on the type
        // that owns it; a channel payload narrows through the SAME Data.
        var rawField = typeof(Data).GetField("_raw",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await Assert.That(rawField).IsNull();

        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        await Assert.That(d.HasRaw).IsTrue();      // source-backed, untouched
        _ = await d.Value();                       // parse rebinds the same Data
        await Assert.That(d.HasRaw).IsFalse();     // single storage — raw moved
        await Assert.That(d.Type.Name).IsEqualTo("dict");
    }


    [Test]
    public async Task GenericToRaw_DoesNotExist_OnItemBase()
    {
        // item.@this has no PUBLIC ToRaw() — the raw escape is internal-only
        // (gated interop: conversion leaves, comparison normalization, the
        // wire walk). Raw leaves the value only via Write / the typed ask.
        var method = typeof(global::app.type.item.@this).GetMethod("ToRaw",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(method).IsNull();
    }

    [Test]
    public async Task TextRawValue_IsPrivate_NotPublicProperty()
    {
        // text.@this has no PUBLIC `string Value` property — the string face
        // is internal (gated interop for in-assembly leaf handlers); outside
        // the engine it is emitted only through text.Write(IWriter).
        var prop = typeof(global::app.type.text.@this).GetProperty("Value",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }

    [Test]
    public async Task ToString_OnUnmaterialisedReference_DoesNotNavigate_DoesNotRead()
    {
        // MaterializeCount=0 before and after data.ToString(); ToString reads the
        // already-materialised backing only — sync framework method must not enter the async chain
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        _ = d.ToString();
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    [Test]
    public async Task EqualsAndGetHashCode_OnUnmaterialisedReference_DoNotRead()
    {
        // MaterializeCount=0 before/after data.Equals(other) and data.GetHashCode()
        await using var app = NewApp(out var root);
        var d = await RawBackedJson(app, root);
        _ = d.Equals(new Data("y", 1));
        _ = d.GetHashCode();
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    [Test]
    public async Task VarReference_RidesAsTypedText_NeverBareCSharpString()
    {
        // SetValue("%x%") + downstream read yields a `text` instance — value slot is never a raw System.String
        var d = new Data("slot");
        d.SetValue("%x%");
        await Assert.That(d.Peek() is global::app.type.text.@this).IsTrue();
        await Assert.That(d.IsVariable).IsTrue();
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

    [Test]
    public async Task DataType_MintsEntityFromInstance()
    {
        // data.Type is a pure forward — the instance mints its own entity.
        var n = new Data("n", 5);
        await Assert.That(n.Type.Name).IsEqualTo("number");
        await Assert.That(n.Type.Kind).IsEqualTo("int");
        var t = new Data("t", "hello");
        await Assert.That(t.Type.Name).IsEqualTo("text");
    }
}
