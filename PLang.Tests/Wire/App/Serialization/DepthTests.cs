namespace PLang.Tests.App.Serialization;

// plang's readers read 64 levels deep and recurse per level, so a value deeper than that is refused where it is
// written, by its depth, never written for a reader to fail on (or a peer to overflow a stack with).
public class DepthTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // A list holding a list … levels deep, a number at the bottom.
    private static object Nested(int levels)
    {
        object value = 1L;
        for (var i = 0; i < levels; i++) value = new List<object?> { value };
        return value;
    }

    [Test]
    public async Task AValueAsDeepAsTheReadersRead_RoundTrips()
    {
        var context = app.actor.list.User.Context;
        var json = app.type.list.Mime("application/json");
        using var bytes = new MemoryStream();

        await (await json.Encode(bytes, new global::app.data.@this("x", Nested(64), context: context), context, global::app.View.Out)).IsSuccess();
        var back = await json.Decode(bytes.ToArray(), context, "x");

        await back.IsSuccess();
        await Assert.That((await back.Open())).IsNotNull();
        await back.IsSuccess();
    }

    [Test]
    public async Task AValueDeeperThanTheReadersRead_IsRefusedAtTheWrite()
    {
        var context = app.actor.list.User.Context;
        using var bytes = new MemoryStream();

        var written = await app.type.list.Mime("application/json")
            .Encode(bytes, new global::app.data.@this("x", Nested(65), context: context), context, global::app.View.Out);

        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("ValueTooDeep");
        await Assert.That(written.Error.Message).Contains("64");
    }

    // A slice already written (a wire relayed byte-identical) is pasted whole: its own nesting counts where it lands.
    private object Pasted(int levels)
    {
        var slice = new string('[', levels) + "1" + new string(']', levels);
        var plang = (global::app.type.item.wire.kind.plang.@this)app.type.list[typeof(global::app.type.item.wire.@this)].kind["plang"]!;
        var wire = new global::app.type.item.wire.@this(slice, app.type.list[typeof(global::app.type.item.list.@this)], plang);
        return new List<object?> { new List<object?> { wire } };
    }

    [Test]
    public async Task AWirePastedUnderTwoLevels_CountsItsOwnDepth()
    {
        var context = app.actor.list.User.Context;
        var json = app.type.list.Mime("application/json");

        using (var fits = new MemoryStream())
            await (await json.Encode(fits, new global::app.data.@this("x", Pasted(62), context: context), context, global::app.View.Out)).IsSuccess();

        using var deep = new MemoryStream();
        var written = await json.Encode(deep, new global::app.data.@this("x", Pasted(63), context: context), context, global::app.View.Out);
        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("ValueTooDeep");
    }

    [Test]
    public async Task APlangWriteOfAValueTooDeep_IsRefused()
    {
        var context = app.actor.list.User.Context;
        using var bytes = new MemoryStream();

        var written = await app.type.list.Format("plang")!.Encode(bytes, new global::app.data.@this("x", Nested(70), context: context), context);

        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("ValueTooDeep");
    }
}
