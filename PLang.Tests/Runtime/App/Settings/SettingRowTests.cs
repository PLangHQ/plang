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
        var saved = await actor.Setting.Save("app.goal.list.setting", new global::app.data.@this("s", setting, context: actor.Context));
        await saved.IsSuccess();
    }

    // A saved row outlives the App: the next one on the same folder reads it.
    [Test] public async Task Row_IsReadByTheNextApp()
    {
        await using (var app = new global::app.@this(_dir))
            await Save(app.User, os: false);

        await using var again = new global::app.@this(_dir);
        await Assert.That(await Os(again.User.Context)).IsEqualTo("false");
        await Assert.That(await Os(again.System.Context)).IsEqualTo("true");
    }

    // The user falls back to the system's row; the user's own row wins over it.
    [Test] public async Task UserFallsBackToSystem_OwnRowWins()
    {
        await using var app = TestApp.Create(_dir);
        await Save(app.System, os: false);
        await Assert.That(await Os(app.User.Context)).IsEqualTo("false");

        await Save(app.User, os: true);
        await Assert.That(await Os(app.User.Context)).IsEqualTo("true");
        await Assert.That(await Os(app.System.Context)).IsEqualTo("false");
    }

    // This run's value wins over the saved row.
    [Test] public async Task ThisRun_WinsOverTheRow()
    {
        await using var app = TestApp.Create(_dir);
        await Save(app.User, os: false);
        await app.User.Context.Setting.Set("app.goal.list.setting.os", app.User.Context.Ok(true));
        await Assert.That(await Os(app.User.Context)).IsEqualTo("true");
    }

    // Removing the row goes back to the defaults.
    [Test] public async Task Remove_GoesBackToTheDefaults()
    {
        await using var app = TestApp.Create(_dir);
        await Save(app.User, os: false);
        await (await app.User.Setting.Remove("app.goal.list.setting")).IsSuccess();
        await Assert.That(await Os(app.User.Context)).IsEqualTo("true");
    }

    // A module's own setting saved as a row reaches the action-param seam (its module key).
    [Test] public async Task ModuleRow_ReachesTheSeam()
    {
        await using var app = TestApp.Create(_dir);
        var llm = (global::app.module.action.llm.setting.@this)(await Read("%!llm%", app.User.Context)).Peek()!;
        llm.Cache = false;
        await (await app.User.Setting.Save("llm", new global::app.data.@this("s", llm, context: app.User.Context))).IsSuccess();

        var seam = await app.User.Context.Setting.Get(app.Module("llm")["query"]!, "cache");
        await Assert.That((await seam.Value())?.ToString()).IsEqualTo("false");
        await Assert.That((await (await Read("%!llm.query.cache%", app.User.Context)).Value())?.ToString()).IsEqualTo("false");
    }
}
