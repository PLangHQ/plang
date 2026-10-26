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
