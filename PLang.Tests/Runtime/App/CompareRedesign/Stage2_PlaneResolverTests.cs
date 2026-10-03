namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — the two access planes. `.` = data plane (content/keys/elements);
// `!` = property plane (the value's own properties + the envelope, resolved
// chain-wide). The sigil picks the plane, so a content key `size` (`.size`) and
// the value's `size` (`!size`) never collide. Reserved core (`@schema`, `type`,
// `error`, `success`) is protected — a type may not shadow it.
public class Stage2_PlaneResolverTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-stage2pl-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task DotPlane_ResolvesDataContent_TypeAnswers()
    {
        // %dict.field% → dict's content via the type's own resolver; no central case-table
        await using var app = NewApp();
        var d = new Data("cfg", new Dictionary<string, object?> { ["field"] = "content" }, context: app.actor.list.User.Context);
        var child = await d.Get("field");
        await Assert.That((await child.Value())?.ToString()).IsEqualTo("content");
    }

    [Test]
    public async Task BangPlane_ResolvesPropertyAndEnvelope_TypeAnswers()
    {
        // %text.length% — the value's own member, read with a dot, answered in a PLang value; a plain value has no
        // `!` facts
        await using var app = NewApp();
        var t = new Data("s", new global::app.type.item.text.@this("hello"), context: app.actor.list.User.Context);
        var length = await t.Get("length");
        await Assert.That(length.Peek()).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(length.Peek()!.ToString()).IsEqualTo("5");
        await Assert.That((await t.Get("!length")).IsInitialized).IsFalse();
        // envelope properties resolve on the same plane
        t.Properties["cost"] = 42;
        var cost = await t.Get("!cost");
        await Assert.That(cost.Peek()?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task BangType_ReturnsHeadlineType()
    {
        // %x!type% → headline type name (post-narrow: `dict`)
        await using var app = NewApp();
        var d = new Data("x", new Dictionary<string, object?> { ["k"] = 1 },
            app.actor.list.User.Context.App.type.list["dict"], context: app.actor.list.User.Context);
        var t = await d.Get("!type");
        await Assert.That(((await t.Value()) as global::app.type.@this)?.Name).IsEqualTo("dict");
    }

    [Test]
    public async Task BangReservedCore_Protected_TypeMayNotShadow()
    {
        // the runtime registration check rejects a shadower; every built-in
        // value family is clean (statics like the lattice `Type` are exempt)
        var reserved = app.actor.list.User.Context.App.type.list.Reserved;
        await Assert.That(Shadows(typeof(ReservedShadower), reserved)).IsEqualTo("Error");
        await Assert.That(Shadows(typeof(global::app.type.item.text.@this), reserved)).IsNull();
        await Assert.That(Shadows(typeof(global::app.type.item.dict.@this), reserved)).IsNull();
        await Assert.That(Shadows(typeof(global::app.type.item.image.@this), reserved)).IsNull();
        await Assert.That(Shadows(typeof(global::app.type.item.path.file.@this), reserved)).IsNull();
    }

    private static string? Shadows(System.Type clr, IReadOnlySet<string> reserved) =>
        clr.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(p => reserved.Contains(p.Name))?.Name;

    private sealed class ReservedShadower : global::app.type.item.@this
    {
        public string Error => "shadow";
    }

    [Test]
    public async Task AtSchemaBlocked_AsDictKey_WireMarkerOnly()
    {
        // @schema is the wire marker — the dict write seam rejects it as a key, under ReservedKey
        var d = new global::app.type.item.dict.@this();
        await Assert.That(() => d.Set("@schema", "data")).Throws<global::app.error.AppException>();
        await Assert.That(() => d.Set(new Data("@schema", "data", context: app.actor.list.User.Context))).Throws<global::app.error.AppException>();
        // ordinary keys unaffected; envelope recognition reads the marker off
        // the JsonElement (IsDataMarked), never through a dict key
        d.Set("schema", "fine");
        await Assert.That(d.Has("schema")).IsTrue();
    }

    [Test]
    public async Task NameField_RemovedFromEnvelope_FreeAsDataKey()
    {
        // the OUTBOUND envelope no longer carries `name` (a server's binding
        // label is not API surface); the Store view keeps it (.pr parameters
        // bind by name). `%x.name%` reads the content's own field.
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var d = new Data("myBinding", new Dictionary<string, object?> { ["name"] = "ingi" }, context: ctx);

        // A Data writes itself via Data.Output through the serializer's async path — the Wire
        // converter is read-only and throws on STJ Write. Out drops the envelope name; Store keeps it.
        var plang = app.actor.list.User.Context.Format("application/plang");
        var outbound = plang.Serialize(d, app.actor.list.User.Context).Peek()!.ToString()!;
        var store = plang.Store(d, app.actor.list.User.Context).Peek()!.ToString()!;

        await Assert.That(outbound).DoesNotContain("\"myBinding\"");
        await Assert.That(store).Contains("\"myBinding\"");
        // the content key `name` is free — nothing on the envelope to shadow it
        var child = await d.Get("name");
        await Assert.That((await child.Value())?.ToString()).IsEqualTo("ingi");
    }

    [Test]
    public async Task BangSize_AndDotSize_AreDistinct_NoShadowing()
    {
        // %dict.size% (content key=10) and %dict!size% (property bag=28) — sigil picks the plane
        await using var app = NewApp();
        var d = new Data("dict", new Dictionary<string, object?> { ["size"] = 10 }, context: app.actor.list.User.Context);
        d.Properties["size"] = 28;
        var content = await d.Get("size");     // `.` — the data plane (content key)
        var property = await d.Get("!size");   // `!` — the property plane (Properties bag)
        await Assert.That((await content.Value())?.ToString()).IsEqualTo("10");
        await Assert.That((await property.Value())?.ToString()).IsEqualTo("28");
    }
}
