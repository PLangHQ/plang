namespace PLang.Tests.App.Settings;

/// <summary>
/// Pin for decision 790 (the fix-it-by-hand rule): an old-shape settings row — one written by
/// the pre-789 binary with type <c>{"name":"setting","kind":"llm.setting"}</c> — reads loudly,
/// not silently. The fixture (<c>fixtures/oldshapesettingrow/data.sqlite</c>) is the committed
/// row the old binary wrote (one row <c>user!llm.setting</c>, no secrets — Key/Endpoint/Model
/// cleared when it was generated). A fresh App over that store asks for the <c>llm.setting</c>
/// class; the read throws an <c>AppException</c> whose <c>Error</c> is <c>SettingsUnreadable</c>,
/// names the row's key, and says how to fix it (delete it and set it again).
/// </summary>
public class AnOldShapeSettingRowIsNamedLoudlyTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_oldshape_" + System.Guid.NewGuid().ToString("N")[..8]);
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
        => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here)!, "fixtures", "oldshapesettingrow", "data.sqlite");

    [Test] public async Task AnOldShapeSettingRow_IsNamedLoudly()
    {
        await using var app = new global::app.@this(_dir);
        await app.actor.list.User.Setting.Load();

        var thrown = Assert.Throws<global::app.error.AppException>(
            () => _ = app.actor.list.User.Setting["llm.setting"]);

        // The read of an old-shape row surfaces SettingsUnreadable, names the row's key
        // (user!llm.setting), and spells out the by-hand fix.
        var err = thrown!.Error;
        await Assert.That(err.Key).IsEqualTo("SettingsUnreadable");
        await Assert.That(err.Message).Contains("llm.setting");
        await Assert.That(err.Message).Contains("older shape");
        await Assert.That(err.Message).Contains("delete it and set it again");
    }
}
