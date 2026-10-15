namespace PLang.Tests.App.Settings;

/// <summary>
/// Stage 2 guard (kind-is-type v2, decision 774): a settings row saved before the change
/// still reads after it. A saved row is identified by its key (<c>&lt;actor&gt;!&lt;path&gt;</c>),
/// which already says which class — the row's own <c>kind</c> field is no longer the class
/// picker. One App writes an llm settings row in today's shape (<c>{setting, kind: &lt;path&gt;}</c>,
/// as the store holds it); a second App on the same dir reads it back through the actor's
/// settings. Green today; must stay green after stage 2.
/// </summary>
public class OldSettingRowStillReadsTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_oldrow_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_dir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch (System.IO.IOException) { }
    }

    [Test] public async Task LlmSettingRow_SavedInTodaysShape_ReadsBack()
    {
        await using (var app = new global::app.@this(_dir))
        {
            var llm = app.actor.list.User.Setting.Of<global::app.module.llm.setting.@this>();
            llm.Cache = new(global::app.module.cache.type.cache.skip);
            await (await app.actor.list.User.Setting.Save(llm)).IsSuccess();
        }

        await using var again = new global::app.@this(_dir);
        await again.actor.list.User.Setting.Load();
        var reloaded = again.actor.list.User.Setting.Of<global::app.module.llm.setting.@this>();
        await Assert.That(reloaded.Cache?.ToString()).IsEqualTo("skip");
    }
}
