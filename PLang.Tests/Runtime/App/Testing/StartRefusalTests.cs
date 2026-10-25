using global::PLang;

namespace PLang.Tests.App.Tester;

/// <summary>
/// A command line plang can't read is refused, the argument named — never answered quietly with what it may have
/// meant: a second goal, a setting written as a goal, a mode word given a value it would drop, a mode turned off
/// while its settings turn it on, one property written twice in different case.
/// </summary>
public class StartRefusalTests
{
    private string _root = null!;

    [Before(Test)]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-startrefusal-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_root);
    }

    [After(Test)]
    public void Teardown()
    {
        if (System.IO.Directory.Exists(_root)) System.IO.Directory.Delete(_root, true);
    }

    private async Task Refused(string[] line, string names)
    {
        var (app, error) = await new Executor(_root).Configure(line);
        if (app != null) await app.DisposeAsync();
        await Assert.That(error).IsNotNull().Because($"plang {string.Join(" ", line)} was read without a word");
        await Assert.That(error!.Error!.Message).Contains(names);
    }

    [Test] public async Task ASecondGoal_IsRefused()
        => await Refused(["Start", "Other"], "Other");

    [Test] public async Task ASettingWrittenAsAGoal_IsRefused()
        => await Refused(["test.setting.timeout"], "--test.setting.timeout");

    [Test] public async Task AModeWordGivenAValue_IsRefused()
        => await Refused(["--test=module/x"], "--test=module/x");

    [Test] public async Task AModeTurnedOff_WhileItsSettingTurnsItOn_IsRefused()
        => await Refused(["--debug=false", "--debug.setting.goal=Start"], "--debug=false");

    [Test] public async Task OnePropertyWrittenTwiceInDifferentCase_IsRefused()
        => await Refused(["name=a", "Name=b"], "Name");

    [Test] public async Task OneFlagWrittenTwiceInDifferentCase_IsRefused()
        => await Refused(["--test.setting.timeout=5s", "--Test.Setting.Timeout=6s"], "Test.Setting.Timeout");

    // what reads still reads: a goal and a folder to test, a path that starts with a dot, the same property twice
    [Test]
    [Arguments("Start name=a")]
    [Arguments("--test ./module")]
    [Arguments("sub/Run.goal")]
    [Arguments("name=a name=b")]
    [Arguments("--debug=false")]
    public async Task ALineItReads_IsNotRefused(string line)
    {
        var (app, error) = await new Executor(_root).Configure(line.Split(' '));
        await Assert.That(error).IsNull().Because(error?.Error?.Message ?? "");
        if (app != null) await app.DisposeAsync();
    }

    // %path% is the root the app landed on, not the folder as typed: the build compares it with that root
    [Test]
    [Arguments("--test")]
    [Arguments("--build")]
    public async Task APath_IsSeededFromTheRoot(string mode)
    {
        var typed = System.IO.Path.Combine(_root, "sub", "..");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, "sub"));
        var (app, error) = await new Executor(typed).Configure([mode]);
        await Assert.That(error).IsNull();
        await using var _ = app!;
        var path = await app.actor.list.User.Context.Variable.Get("path");
        await Assert.That(await path.Value()).IsTypeOf<global::app.type.item.path.file.@this>();
        await Assert.That(((global::app.type.item.path.@this)(await path.Value())!).Absolute).IsEqualTo(app.filesystem.root.Absolute);
    }
}
