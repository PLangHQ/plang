namespace PLang.Tests.App.Testing;

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
    private global::App.@this _app = null!;
    private global::App.Channels.Channel.Stream.@this _capture = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
        // Debug.Write routes via System.Channels.Resolve("debug") ?? Resolve("error").
        // Register a memory channel as "error" on System so debug output lands in a
        // capture buffer instead of the real stderr stream the channel was wired to.
        _app.System.Channels.Register(global::App.Channels.Channel.Stream.@this.Memory(
            global::App.Channels.@this.Error));
        _capture = (global::App.Channels.Channel.Stream.@this)
            _app.System.Channels.Get(global::App.Channels.@this.Error)!;
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

    // Debug.Apply with level="action" attaches BeforeAction + AfterAction widened handlers.
    // Running a goal with one action must fire them without throwing.
    [Test]
    public async Task Debug_LevelAction_AttachesWidenedHandlers_NoThrowOnFire()
    {
        _app.Debug.Apply(new Dictionary<string, object?> { ["level"] = "action" });

        var goal = new Goal
        {
            Name = "Dbg",
            Path = "/Dbg.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "set x",
                    Actions = new StepActions
                    {
                        new PrAction
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data> { new("Name", "x"), new("Value", 1) }
                        }
                    }
                }
            }
        };
        _app.Goals.Add(goal);

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
