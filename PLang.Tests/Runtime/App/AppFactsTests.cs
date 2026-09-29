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

    // The store is born ready, a store of kind sqlite: a program reads what it is, never its tables.
    [Test] public async Task TheStore_IsAStore_OfKindSqlite()
    {
        await using var app = TestApp.Create("/app");

        var store = await Read(app, "%!app.store%");

        await store.IsSuccess();
        var value = await store.Value();
        await Assert.That(value!.Type.Name).IsEqualTo("store");
        await Assert.That(value.Type.kind.Name).IsEqualTo("sqlite");
        await Assert.That(value.ToString()).IsEqualTo("sqlite");
    }

    // Born, not opened: an app that never uses its store makes no database.
    [Test] public async Task AnUntouchedStore_MakesNoDatabase()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-store-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using (var app = TestApp.Create(root)) { _ = app.store; }
            await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(root, ".db", "system.sqlite"))).IsFalse();
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    // A store that can't open answers why at its verb — none throws.
    [Test] public async Task AStoreThatCantOpen_AnswersWhy()
    {
        await using var app = TestApp.Create("/app");
        using var nowhere = new global::app.store.sqlite.@this(null, () => null, app.actor.list.System.Context);

        var set = await nowhere.Set("t", "k", app.actor.list.System.Context.Ok("v"));

        await Assert.That(set.Success).IsFalse();
        await Assert.That(set.Error!.Message).Contains("needs a file");
    }

    // An app with no identity yet is being created now: created is this run's start.
    [Test] public async Task ANewApp_IsCreated_WhenItStarts()
    {
        await using var app = TestApp.Create("/app");

        await Assert.That(app.Created.Value).IsEqualTo(app.StartedAt.Value);
    }
}
