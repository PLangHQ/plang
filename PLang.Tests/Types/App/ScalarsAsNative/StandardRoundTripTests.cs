namespace PLang.Tests.App.ScalarsAsNative;

// A value in a standard (a duration's short or iso, a size's iec or si) is a class of its own that writes itself in
// it: stored on the wire and read back, it is the same text in the same standard.
public class StandardRoundTripTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    // the value stored on the plang wire, a typed row, and read back
    private async Task<global::app.type.item.@this?> Stored(global::app.type.item.@this value)
    {
        var format = Ctx.App.type.list["wire"].kind["plang"]!;
        using var stream = new System.IO.MemoryStream();
        await (await format.Encode(stream, new global::app.data.@this("v", value, context: Ctx), Ctx, global::app.View.Store)).IsSuccess();
        stream.Position = 0;
        var read = await format.Decode(stream, Ctx, global::app.View.Store);
        return await read.Value() as global::app.type.item.@this;
    }

    [Test]
    [Arguments("5m", "short")]
    [Arguments("PT5M", "iso")]
    public async Task ADuration_RoundTripsInItsStandard(string text, string standard)
    {
        var value = await Stored(global::app.type.item.duration.@this.Resolve(text, Ctx)!);

        await Assert.That(value is global::app.type.item.duration.@this).IsTrue();
        await Assert.That(await Ctx.Written(value!)).IsEqualTo(text);
        await Assert.That(value!.Type.kind.Name).IsEqualTo(standard);
    }

    // `set %d% = "PT30S" as iso duration`: a text declared a standard is made by that standard
    [Test]
    [Arguments("duration", "5m", "short")]
    [Arguments("duration", "PT5M", "iso")]
    [Arguments("duration", "00:05:00", "dotnet")]
    [Arguments("size", "100 MB", "si")]
    [Arguments("size", "95.4 MiB", "iec")]
    public async Task AText_DeclaredAStandard_IsMadeByIt(string family, string text, string standard)
    {
        var value = Ctx.App.type.list[new global::app.type.@this(family, standard), Ctx].Make(Ctx.Text(text), Ctx);

        await Assert.That(value.Type.Name).IsEqualTo(family);
        await Assert.That(value.Type.kind.Name).IsEqualTo(standard);
        await Assert.That(await Ctx.Written(value)).IsEqualTo(text);
    }

    // a standard reads only its own form: a text in another is refused, naming the standard and how it is written
    [Test]
    [Arguments("duration", "iso", "30s", "'30s' is not a duration in iso (iso is written PT30S)")]
    [Arguments("size", "si", "100 MiB", "'100 MiB' is not a size in si (si is written 100 MB)")]
    public async Task AText_InAnotherStandard_IsRefused_NamingTheDeclaredOne(string family, string standard, string text, string message)
    {
        System.Exception? refused = null;
        try { Ctx.App.type.list[new global::app.type.@this(family, standard), Ctx].Make(Ctx.Text(text), Ctx); }
        catch (System.Exception thrown) { refused = thrown; }

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!.Message).Contains(message);
    }

    [Test]
    [Arguments("duration", "iso", "PT30S")]
    [Arguments("size", "si", "104.9 MB")]
    public async Task AText_InTheDeclaredStandard_StillMakes(string family, string standard, string text)
    {
        var value = Ctx.App.type.list[new global::app.type.@this(family, standard), Ctx].Make(Ctx.Text(text), Ctx);

        await Assert.That(value.Type.kind.Name).IsEqualTo(standard);
        await Assert.That(await Ctx.Written(value)).IsEqualTo(text);
    }

    // a value made in a standard holds the app's own entry for it, never a type object of its own
    [Test]
    public async Task AStandardsValue_HoldsTheAppsEntryForIt()
    {
        var count = global::app.type.item.size.@this.Create(2048, Ctx);
        var carrier = new global::app.data.@this("d", context: Ctx);
        var asked = global::app.type.item.duration.@this.Create("PT30S", new global::app.type.@this("duration", "iso"), carrier)!;

        await Assert.That(ReferenceEquals(count.Type, Ctx.App.type.list[new global::app.type.@this("size", "iec"), Ctx])).IsTrue();
        await Assert.That(ReferenceEquals(asked.Type, Ctx.App.type.list[new global::app.type.@this("duration", "iso"), Ctx])).IsTrue();
    }

    [Test]
    [Arguments("100 MB", "si")]
    [Arguments("95.4 MiB", "iec")]
    public async Task ASize_RoundTripsInItsStandard(string text, string standard)
    {
        var value = await Stored(global::app.type.item.size.@this.Resolve(text, Ctx)!);

        await Assert.That(value is global::app.type.item.size.@this).IsTrue();
        await Assert.That(await Ctx.Written(value!)).IsEqualTo(text);
        await Assert.That(value!.Type.kind.Name).IsEqualTo(standard);
    }
}
