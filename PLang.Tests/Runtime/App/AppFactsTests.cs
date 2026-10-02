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
        await using var app = new global::app.@this("/app").Testing();

        var fact = await Read(app, path);

        await fact.IsSuccess();
        await Assert.That((await fact.Value())!.Type.Name).IsEqualTo(type);
    }

    // The store is born ready, a store of kind sqlite: a program reads what it is, never its tables.
    [Test] public async Task TheStore_IsAStore_OfKindSqlite()
    {
        await using var app = new global::app.@this("/app").Testing();

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
            await using (var app = new global::app.@this(root).Testing()) { _ = app.store; }
            await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(root, ".data", "data.sqlite"))).IsFalse();
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    // A store that can't open answers why at its verb — none throws.
    [Test] public async Task AStoreThatCantOpen_AnswersWhy()
    {
        await using var app = new global::app.@this("/app").Testing();
        using var nowhere = new global::app.store.sqlite.@this(null, () => null, app.actor.list.System.Context);

        var set = await nowhere.Set("t", "k", app.actor.list.System.Context.Ok("v"));

        await Assert.That(set.Success).IsFalse();
        await Assert.That(set.Error!.Message).Contains("needs a file");
    }

    // The app's name is its setting, as its asker's settings have it; the system's is its own.
    [Test] public async Task TheName_IsTheAskersSetting()
    {
        await using var app = new global::app.@this("/shopfolder").Testing();
        var user = app.actor.list.User.Context;

        var set = await new global::app.type.item.variable.parser.@this("%!app.setting.name%").Variable.Single()
            .Set(user.Ok(new global::app.type.item.text.@this("Shop")), user);
        var name = await Read(app, "%!app.name%");

        await set.IsSuccess();
        await Assert.That((await name.Value())?.ToString()).IsEqualTo("Shop");
        await Assert.That(app.Name(app.actor.list.System.Context).ToString()).IsEqualTo("shopfolder");
    }

    [Test] public async Task WithNothingSet_TheNameIsTheFolders()
    {
        await using var app = new global::app.@this("/shopfolder").Testing();

        var name = await Read(app, "%!app.name%");

        await Assert.That((await name.Value())?.ToString()).IsEqualTo("shopfolder");
    }

    // The id is the identity's, read through the app's settings, never set there.
    [Test] public async Task TheId_IsReadThroughTheAppsSettings()
    {
        await using var app = new global::app.@this("/app").Testing();

        var id = await Read(app, "%!app.setting.id%");

        await Assert.That((await id.Value())?.ToString()).IsEqualTo(app.Id);
    }

    // An app with no identity yet is being created now: created is this run's start.
    [Test] public async Task ANewApp_IsCreated_WhenItStarts()
    {
        await using var app = new global::app.@this("/app").Testing();

        await Assert.That(app.Created.Value).IsEqualTo(app.StartedAt.Value);
    }
}
