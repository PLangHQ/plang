namespace PLang.Tests.App.Types;

// A format of a class that writes itself (decision 157): the format<T> binds T's own encode/decode on its first
// use — nothing is handed in by the registry. Each format here is made bare, as no registry made it.
public class FormatWritesItselfTests
{
    private static global::app.@this NewApp() => global::PLang.Tests.Session.App("/test").Testing();

    [Test] public async Task AGoalsPrFormat_MadeBare_ReadsAndWritesThroughTheGoalsOwnDecodeAndEncode()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var pr = new global::app.type.kind.format.@this<global::app.goal.@this>(
            new global::app.Attributes.FormatAttribute("", "application/plang-goal", ".pr"), app.type.list["goal"]);

        var read = await pr.Decode(System.Text.Encoding.UTF8.GetBytes("{\"name\":\"Start\"}"), ctx, "start");
        await read.IsSuccess();
        await Assert.That(read.Peek() is global::app.goal.@this { Name: "Start" }).IsTrue();

        using var stream = new System.IO.MemoryStream();
        var written = await pr.Encode(stream, read, ctx);
        await written.IsSuccess();
        var text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).Contains("\"name\": \"Start\"");
        await Assert.That(text).EndsWith("\n");
    }

    [Test] public async Task AnImagesPngFormat_MadeBare_WritesTheImagesOwnBytes()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var png = new global::app.type.kind.format.@this<global::app.type.item.image.@this>(
            new global::app.Attributes.FormatAttribute("png", "image/png") { Compressible = false }, app.type.list["image"]);
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        using var stream = new System.IO.MemoryStream();
        var written = await png.Encode(stream, app.Ok(new global::app.type.item.image.@this(bytes, app.type.list["image"])), ctx);
        await written.IsSuccess();
        await Assert.That(stream.ToArray()).IsEquivalentTo(bytes);
    }
}
