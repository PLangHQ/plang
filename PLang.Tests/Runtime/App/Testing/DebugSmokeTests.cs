namespace PLang.Tests.App.Tester;

/// <summary>
/// Smoke guard for the Debug module's widened AfterAction/BeforeAction bindings.
/// The handlers were widened from (context) → (context, action, result) as part of the
/// Batch 6 payload-widening. Debug ignores the extra params (`(context, _, _) =>`) but
/// the dispatch path still pushes them — a mis-typed lambda signature would crash at
/// first fire. This test verifies Debug.Apply + Level="action" + one real action fires
/// all four handlers without throwing.
/// </summary>
public class DebugSmokeTests
{
    private global::app.@this _app = null!;
    private global::app.channel.type.stream.@this _capture = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
        // Debug.Write routes via System.Channels.Resolve("debug") ?? Resolve("error").
        // Register a memory channel as "error" on System so debug output lands in a
        // capture buffer instead of the real stderr stream the channel was wired to.
        _app.System.Channel.Register(global::app.channel.type.stream.@this.Memory(
            global::app.channel.list.@this.Error));
        _capture = (global::app.channel.type.stream.@this)
            (_app.System.Channel.Get(global::app.channel.list.@this.Error))!;
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
    }

    private string ReadCapture()
    {
        // The MemoryStream has been written + flushed; rewind and read all.
        _capture.Stream.Position = 0;
        using var reader = new StreamReader(_capture.Stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    // Debug config (level="action") set via the walk, then Activate() attaches BeforeAction +
    // AfterAction widened handlers. Running a goal with one action must fire them without throwing.
    [Test]
    public async Task Debug_LevelAction_AttachesWidenedHandlers_NoThrowOnFire()
    {
        _app.Debug = new Debugging(_app.System.Context);
        _app.Setting.Set(_app.Debug, new Dictionary<string, object?> { ["level"] = "action" });
        _app.Debug.Activate();

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("Dbg",
            Make.Step("set x",
                Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", 1)))));
        _app.Goal.Add(goal);

        // If the widened lambda mis-handles the (action, result) params (e.g. dereferences a
        // null), this call throws. If signatures are correct, it completes and emits to stderr.
        await _app.RunGoalAsync(goal, _app.User.Context);

        var debugOut = ReadCapture();
        // Step-level markers come from the always-on handlers.
        await Assert.That(debugOut).Contains("DEBUG [BEFORE]");
        await Assert.That(debugOut).Contains("DEBUG [AFTER]");
        // Action-level markers come from the widened BeforeAction/AfterAction handlers —
        // their presence proves the widened lambdas ran without throwing.
        await Assert.That(debugOut).Contains("ACTION [BEFORE]");
        await Assert.That(debugOut).Contains("ACTION [AFTER]");
    }
}
