namespace PLang.Tests.App.Settings;

/// <summary>
/// A settings row an older binary wrote reads loudly, never as a silent default. The fixture
/// (<c>fixtures/oldshapesettingrow/data.sqlite</c>) is the committed row the old binary wrote (one row
/// <c>user!llm.setting</c>, no secrets): its signature names no view, so it can't be checked and is refused. A
/// fresh App over that store asks for the <c>llm.setting</c> class: the answer is <c>SettingsUnreadable</c>, naming
/// the row's key and how to fix it (delete it and set it again), its cause the signature's refusal.
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

        var read = await app.actor.list.User.Setting.Get("llm.setting");

        // The read of the old row surfaces SettingsUnreadable, names the row's key (user!llm.setting) and spells out
        // the by-hand fix; why is the signature's refusal, whole.
        await read.IsFailure();
        var err = read.Error!;
        await Assert.That(err.Key).IsEqualTo("SettingsUnreadable");
        await Assert.That(err.Message).Contains("llm.setting");
        await Assert.That(err.Message).Contains("delete it and set it again");
        await Assert.That(err.list.Single().Key).IsEqualTo("SignatureInvalid");
    }
}
