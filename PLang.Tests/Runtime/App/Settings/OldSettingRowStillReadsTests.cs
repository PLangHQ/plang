namespace PLang.Tests.App.Settings;

/// <summary>
/// Stage 2 guard (kind-is-type v2, decision 774): a settings row saved before the change
/// still reads after it. The row is identified by its key (<c>&lt;actor&gt;!&lt;path&gt;</c>),
/// which already says which class — the row's own <c>kind</c> field is no longer the class
/// picker. The fixture (<c>fixtures/oldsettingrow/data.sqlite</c>, flattened out of a
/// <c>.data/</c> folder that <c>.gitignore</c> would hide) was written by today's binary;
/// its one row is <c>user!llm.setting</c>, whose signed envelope holds the type in today's
/// shape — <c>{"name":"setting","kind":"llm.setting"}</c> — and the value
/// <c>{"cache":"skip","key":"","endpoint":"","model":"","enabled":true}</c>. The test
/// copies that sqlite into a fresh <c>.data/setting/</c> under its own temp dir and only
/// reads: a new App, <c>Setting.Load()</c>, the reloaded instance's <c>Cache</c> is
/// <c>skip</c>. Green today; must stay green after stage 2 so an existing user's rows
/// survive the rebuild.
/// </summary>
public class OldSettingRowStillReadsTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_oldrow_" + System.Guid.NewGuid().ToString("N")[..8]);
        var settingDir = System.IO.Path.Combine(_dir, ".data", "setting");
        System.IO.Directory.CreateDirectory(settingDir);
        System.IO.File.Copy(FixtureSqlite(), System.IO.Path.Combine(settingDir, "data.sqlite"));
    }

    [After(Test)]
    public void Cleanup()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch (System.IO.IOException) { }
    }

    private static string FixtureSqlite([System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here)!, "fixtures", "oldsettingrow", "data.sqlite");

    [Test] public async Task LlmSettingRow_FromTodaysFixture_ReadsBack()
    {
        await using var app = new global::app.@this(_dir);
        await app.actor.list.User.Setting.Load();
        var reloaded = app.actor.list.User.Setting.Of<global::app.module.llm.setting.@this>();
        await Assert.That(reloaded.Cache?.ToString()).IsEqualTo("skip");
    }
}
