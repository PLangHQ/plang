namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — the two access planes. `.` = data plane (content/keys/elements);
// `!` = property plane (the value's own properties + the envelope, resolved
// chain-wide). The sigil picks the plane, so a content key `size` (`.size`) and
// the value's `size` (`!size`) never collide. Reserved core (`@schema`, `type`,
// `error`, `success`) is protected — a type may not shadow it.
public class Stage2_PlaneResolverTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-stage2pl-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task DotPlane_ResolvesDataContent_TypeAnswers()
    {
        // %dict.field% → dict's content via the type's own resolver; no central case-table
        await using var app = NewApp();
        var d = new Data("cfg", new Dictionary<string, object?> { ["field"] = "content" }) { Context = app.User.Context };
        var child = await d.GetChild("field");
        await Assert.That((await child.Value())?.ToString()).IsEqualTo("content");
    }

    [Test]
    public async Task BangPlane_ResolvesPropertyAndEnvelope_TypeAnswers()
    {
        // %text!length% — the value's own property, answered in a PLang value
        await using var app = NewApp();
        var t = new Data("s", new global::app.type.text.@this("hello")) { Context = app.User.Context };
        var length = await t.GetChild("!length");
        await Assert.That(length.Peek()).IsTypeOf<global::app.type.number.@this>();
        await Assert.That(length.Peek()!.ToString()).IsEqualTo("5");
        // envelope properties resolve on the same plane
        t.Properties["cost"] = 42;
        var cost = await t.GetChild("!cost");
        await Assert.That(cost.Peek()?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task BangType_ReturnsHeadlineType()
    {
        // %x!type% → headline type name (post-narrow: `dict`)
        await using var app = NewApp();
        var d = new Data("x", new Dictionary<string, object?> { ["k"] = 1 },
            global::app.type.@this.FromName("dict")) { Context = app.User.Context };
        var t = await d.GetChild("!type");
        await Assert.That(((await t.Value()) as global::app.type.@this)?.Name).IsEqualTo("dict");
    }

    [Test]
    public async Task BangTypeList_ReturnsAccumulatedChain_NewestFirst()
    {
        // %x!type.list% post-narrow → [dict, file] (newest at index 0)
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-st2chain-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        await using var app = new global::app.@this(dir);
        try
        {
            File.WriteAllText(System.IO.Path.Combine(dir, "c.json"), "{\"a\":1}");
            var read = new global::app.module.file.Read
            {
                Context = app.User.Context,
                Path = new global::app.data.@this<global::app.type.path.@this>("",
                    new global::app.type.path.file.@this(System.IO.Path.Combine(dir, "c.json")) { Context = app.User.Context }),
            };
            var data = await read.Run();
            await data.GetChild("a");                       // narrow
            var chain = await data.GetChild("!type.list");
            var names = ((IReadOnlyList<global::app.type.@this>)chain.Peek()!).Select(t => t.Name).ToList();
            await Assert.That(names[0]).IsEqualTo("dict");
            await Assert.That(names).Contains("file");
        }
        finally { Directory.Delete(dir, true); }
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
        await using var app = NewApp();
        var d = new Data("dict", new Dictionary<string, object?> { ["size"] = 10 }) { Context = app.User.Context };
        d.Properties["size"] = 28;
        var content = await d.GetChild("size");     // `.` — the data plane (content key)
        var property = await d.GetChild("!size");   // `!` — the property plane (Properties bag)
        await Assert.That((await content.Value())).IsEqualTo(10);
        await Assert.That((await property.Value())).IsEqualTo(28);
    }
}
