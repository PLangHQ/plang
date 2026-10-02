namespace PLang.Tests.App.Settings;

/// <summary>
/// An actor's saved rows: one per actor per setting, read into the layers — the defaults ← the actor's
/// row (the user's, else the system's) ← this run's values.
/// </summary>
public class SettingRowTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_rows_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_dir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch (System.IO.IOException) { }
    }

    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    private static async Task<string?> Os(global::app.actor.context.@this ctx)
        => (await (await Read("%!app.goal.list.setting.os%", ctx)).Value())?.ToString();

    // The instance as read, with one option changed, saved as the actor's row.
    private static async Task Save(global::app.actor.@this actor, bool os)
    {
        var setting = (global::app.goal.list.setting.@this)(await Read("%!app.goal.list.setting%", actor.Context)).Peek()!;
        setting.Os = os;
        var saved = await actor.Setting.Save(setting);
        await saved.IsSuccess();
    }

    // A saved row outlives the App: the next one on the same folder reads it.
    [Test] public async Task Row_IsReadByTheNextApp()
    {
        await using (var app = new global::app.@this(_dir))
            await Save(app.actor.list.User, os: false);

        await using var again = new global::app.@this(_dir);
        await Assert.That(await Os(again.actor.list.User.Context)).IsEqualTo("false");
        await Assert.That(await Os(again.actor.list.System.Context)).IsEqualTo("true");
    }

    // The values a saved row's typed-list option holds, as the next App reads it back.
    private static async Task<List<string>> Listed<T>(global::app.@this app, System.Func<T, global::app.type.item.list.@this> option)
        where T : global::app.type.item.setting.@this, new()
    {
        await app.actor.list.User.Setting.Load();
        var values = new List<string>();
        foreach (var row in option(app.actor.list.User.Setting.Of<T>()).Items(app.actor.list.User.Context))
            values.Add((await row.Value())?.ToString() ?? "");
        return values;
    }

    // A typed-list option of choices round-trips through the row: goal.list's visibility.
    [Test] public async Task Row_TypedListOfChoices_IsReadBack()
    {
        await using (var app = new global::app.@this(_dir))
        {
            var setting = app.actor.list.User.Setting.Of<global::app.goal.list.setting.@this>();
            setting.Visibility = new global::app.type.item.list.@this<global::app.type.item.choice.@this<global::app.goal.Visibility>>(
                new global::app.type.item.choice.@this<global::app.goal.Visibility>[] { global::app.goal.Visibility.Public, global::app.goal.Visibility.Private });
            await (await app.actor.list.User.Setting.Save(setting)).IsSuccess();
        }

        await using var again = new global::app.@this(_dir);
        await Assert.That(await Listed<global::app.goal.list.setting.@this>(again, s => s.Visibility))
            .IsEquivalentTo(new[] { "Public", "Private" });
    }

    // A typed-list option of text round-trips through the row: test's include.
    [Test] public async Task Row_TypedListOfText_IsReadBack()
    {
        await using (var app = new global::app.@this(_dir))
        {
            var setting = app.actor.list.User.Setting.Of<global::app.test.setting.@this>();
            setting.Include = new global::app.type.item.list.@this<global::app.type.item.text.@this>(
                new global::app.type.item.text.@this[] { "smoke", "fast" });
            await (await app.actor.list.User.Setting.Save(setting)).IsSuccess();
        }

        await using var again = new global::app.@this(_dir);
        await Assert.That(await Listed<global::app.test.setting.@this>(again, s => s.Include))
            .IsEquivalentTo(new[] { "smoke", "fast" });
    }

    // The user falls back to the system's row; the user's own row wins over it.
    [Test] public async Task UserFallsBackToSystem_OwnRowWins()
    {
        await using var app = new global::app.@this(_dir).Testing();
        await Save(app.actor.list.System, os: false);
        await Assert.That(await Os(app.actor.list.User.Context)).IsEqualTo("false");

        await Save(app.actor.list.User, os: true);
        await Assert.That(await Os(app.actor.list.User.Context)).IsEqualTo("true");
        await Assert.That(await Os(app.actor.list.System.Context)).IsEqualTo("false");
    }

    // This run's value wins over the saved row.
    [Test] public async Task ThisRun_WinsOverTheRow()
    {
        await using var app = new global::app.@this(_dir).Testing();
        await Save(app.actor.list.User, os: false);
        await app.actor.list.User.Context.Setting.Set("app.goal.list.setting.os", app.actor.list.User.Context.Ok(true));
        await Assert.That(await Os(app.actor.list.User.Context)).IsEqualTo("true");
    }

    // Removing the row goes back to the defaults.
    [Test] public async Task Remove_GoesBackToTheDefaults()
    {
        await using var app = new global::app.@this(_dir).Testing();
        await Save(app.actor.list.User, os: false);
        await (await app.actor.list.User.Setting.Remove(app.actor.list.User.Setting.Of<global::app.goal.list.setting.@this>())).IsSuccess();
        await Assert.That(await Os(app.actor.list.User.Context)).IsEqualTo("true");
    }

    // A module's own setting saved as a row reaches the action-param seam (its module key).
    [Test] public async Task ModuleRow_ReachesTheSeam()
    {
        await using var app = new global::app.@this(_dir).Testing();
        var llm = (global::app.module.llm.setting.@this)(await Read("%!llm.setting%", app.actor.list.User.Context)).Peek()!;
        llm.Cache = new(global::app.module.cache.type.cache.skip);
        await (await app.actor.list.User.Setting.Save(llm)).IsSuccess();

        var seam = await app.actor.list.User.Context.Setting.Get(app.Module("llm")["query"]!, "cache");
        await Assert.That((await seam.Value())?.ToString()).IsEqualTo("skip");
        await Assert.That((await (await Read("%!llm.query.setting.cache%", app.actor.list.User.Context)).Value())?.ToString()).IsEqualTo("skip");
    }
}
