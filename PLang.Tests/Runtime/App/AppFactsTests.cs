namespace PLang.Tests.App;

// The app's facts are plang values: when it was created and started are datetimes, how long it has run a duration.
public class AppFactsTests
{
    private static async Task<global::app.data.@this> Read(global::app.@this app, string path)
        => await new global::app.type.item.variable.parser.@this(path).Variable.Single().Start(app.actor.list.User.Context);

    [Test]
    [Arguments("%!app.created%", "datetime")]
    [Arguments("%!app.updated%", "datetime")]
    [Arguments("%!app.startedat%", "datetime")]
    [Arguments("%!app.uptime%", "duration")]
    public async Task AFact_IsItsPlangType(string path, string type)
    {
        await using var app = TestApp.Create("/app");

        var fact = await Read(app, path);

        await fact.IsSuccess();
        await Assert.That((await fact.Value())!.Type.Name).IsEqualTo(type);
    }

    // An app with no identity yet is being created now: created is this run's start.
    [Test] public async Task ANewApp_IsCreated_WhenItStarts()
    {
        await using var app = TestApp.Create("/app");

        await Assert.That(app.Created.Value).IsEqualTo(app.StartedAt.Value);
    }
}
