namespace PLang.Tests.App.ChannelsTests;

// A json file is empty as what it stands for is empty: {} and [] are empty, {"a":1} is not.
public class JsonFileTruthTests
{
    [Test]
    [Arguments("{}", false)]
    [Arguments("[]", false)]
    [Arguments("\"\"", false)]
    [Arguments("0", false)]
    [Arguments("false", false)]
    [Arguments("null", false)]
    [Arguments("{\"a\":1}", true)]
    [Arguments("[1]", true)]
    public async Task AJsonFile_IsTrueAsWhatItHolds(string json, bool truthy)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jsontruth_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "in.json"), json);

        var read = await context.File(System.IO.Path.Combine(dir, "in.json")).Read(context);

        await Assert.That(await read.ToBooleanAsync()).IsEqualTo(truthy);
    }

    // opened for its truth, the content is parsed once: navigating it after reads what the truth opened
    [Test]
    public async Task AJsonFile_OpenedForItsTruth_NavigatesAfter()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jsontruth_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "in.json"), "{\"a\":1}");
        var read = await context.File(System.IO.Path.Combine(dir, "in.json")).Read(context);

        await Assert.That(await read.ToBooleanAsync()).IsTrue();
        var a = await read.Get("a");

        await a.IsSuccess();
        await Assert.That((await a.Value())!.Clr<long>()).IsEqualTo(1L);
    }
}
