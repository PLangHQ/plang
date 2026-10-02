namespace PLang.Tests.App.Debug;

/// <summary>
/// Callstack knobs are configured through the setting walk — <c>app.actor.list.System.Setting.Set(new global::app.call.setting.@this().Path, dict)</c>
/// — the same path <c>--callstack={...}</c> takes at startup. (They used to ride on
/// <c>--debug={callstack:...}</c> via a Flags.Parse cross-node write; that shorthand is gone.)
/// </summary>
public class CallStackWalkTests
{
    [Test]
    public async Task EmptyDict_LeavesDefaults()
    {
        await using var app = new global::app.@this("/app").Testing();
        app.actor.list.System.Setting.Set(new global::app.call.setting.@this().Path, new Dictionary<string, object?>());
        var f = app.actor.list.User.Context.call;
        await Assert.That(f.Timing.Value).IsFalse();
        await Assert.That(f.Diff.Value).IsFalse();
        await Assert.That(f.Tags.Value).IsFalse();
        await Assert.That(f.History.Value).IsFalse();
        await Assert.That(f.Setting.Frame.Max.ToInt32()).IsEqualTo(1000);
    }

    [Test]
    public async Task FullObject_AllKnobsHonored()
    {
        await using var app = new global::app.@this("/app").Testing();
        app.actor.list.System.Setting.Set(new global::app.call.setting.@this().Path, new Dictionary<string, object?>
        {
            ["timing"] = true,
            ["diff"] = new Dictionary<string, object?> { ["enabled"] = true, ["deep"] = false },
            ["tags"] = true,
            ["history"] = true,
            ["frame"] = new Dictionary<string, object?> { ["max"] = 500 }
        });
        var f = app.actor.list.User.Context.call;
        await Assert.That(f.Timing.Value).IsTrue();
        await Assert.That(f.Diff.Value).IsTrue();
        await Assert.That(f.Setting.Diff.Deep.Value).IsFalse();
        await Assert.That(f.Tags.Value).IsTrue();
        await Assert.That(f.History.Value).IsTrue();
        await Assert.That(f.Setting.Frame.Max.ToInt32()).IsEqualTo(500);
    }

    [Test]
    public async Task PartialObject_UnspecifiedKnobsStayDefault()
    {
        await using var app = new global::app.@this("/app").Testing();
        app.actor.list.System.Setting.Set(new global::app.call.setting.@this().Path, new Dictionary<string, object?> { ["diff"] = true });
        var f = app.actor.list.User.Context.call;
        await Assert.That(f.Diff.Value).IsTrue();
        await Assert.That(f.Timing.Value).IsFalse();
        await Assert.That(f.Tags.Value).IsFalse();
    }

    [Test]
    public async Task MaxFramesDefaults1000_WhenOmitted()
    {
        await using var app = new global::app.@this("/app").Testing();
        app.actor.list.System.Setting.Set(new global::app.call.setting.@this().Path, new Dictionary<string, object?> { ["history"] = true });
        await Assert.That(app.actor.list.User.Context.call.Setting.Frame.Max.ToInt32()).IsEqualTo(1000);
    }
}
