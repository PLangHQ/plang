using app.error;

namespace PLang.Tests.App.SnapshotTests;

/// <summary>
/// The disk round-trip: Snapshot → SnapshotToWire (JSON string) → SnapshotFromWire
/// → Restore. Proves a captured failure can be persisted and replayed with no live
/// state — the deterministic-replay loop the builder needs.
/// </summary>
public class SnapshotWireTests
{
    private static global::app.snapshot.@this RoundTrip(global::app.@this app, global::app.snapshot.@this snap)
        => app.SnapshotFromWire(app.SnapshotToWire(snap));

    [Test]
    public async Task Variables_SurviveWireRoundTrip_WithValueAndType()
    {
        var src = new global::app.@this("/src");
        src.User.Context.Variable.Set("count", 42L);
        src.User.Context.Variable.Set("name", "plang");

        var wired = RoundTrip(src, src.Snapshot());

        var dst = new global::app.@this("/dst");
        dst.Restore(wired, dst.User.Context);

        await Assert.That(dst.User.Context.Variable.Get("count")?.Value).IsEqualTo(42L);
        await Assert.That(dst.User.Context.Variable.Get("name")?.Value).IsEqualTo("plang");
    }

    [Test]
    public async Task BuildAndTestingBits_SurviveWireRoundTrip()
    {
        var src = new global::app.@this("/src");
        src.Builder.IsEnabled = true;
        src.Tester.IsEnabled = true;

        var wired = RoundTrip(src, src.Snapshot());

        var dst = new global::app.@this("/dst");
        dst.Restore(wired, dst.User.Context);

        await Assert.That(dst.Builder.IsEnabled).IsTrue();
        await Assert.That(dst.Tester.IsEnabled).IsTrue();
    }

    [Test]
    public async Task ErrorsTrail_SurvivesWireRoundTrip_WithContentAndId()
    {
        var src = new global::app.@this("/src");
        var e1 = new ServiceError("first", "TestErr", 400);
        var e2 = new ServiceError("second", "TestErr", 500);
        using (src.Error.Push(e1)) { }
        using (src.Error.Push(e2)) { }

        var wired = RoundTrip(src, src.Snapshot());

        var dst = new global::app.@this("/dst");
        dst.Restore(wired, dst.User.Context);

        await Assert.That(dst.Error.Trail.Count).IsEqualTo(2);
        await Assert.That(dst.Error.Trail[0].Message).IsEqualTo("first");
        await Assert.That(dst.Error.Trail[0].StatusCode).IsEqualTo(400);
        await Assert.That(dst.Error.Trail[1].Message).IsEqualTo("second");
        await Assert.That(dst.Error.Trail[1].StatusCode).IsEqualTo(500);
        // Id is preserved across the wire (the base-Error restore ctor carries it).
        await Assert.That(dst.Error.Trail[0].Id).IsEqualTo(e1.Id);
    }

    [Test]
    public async Task CallStackFrames_Scalars_RoundTripWithIntTyping()
    {
        // Build a frame section by hand the way call.@this.Capture does, then drive
        // it through the wire. The int keys must come back as int (not long) so
        // CallStack.Restore's Read<int> resolves them.
        var src = new global::app.@this("/src");
        var snap = new global::app.snapshot.@this();
        // Emulate one captured frame's scalar shape.
        var cs = snap.Section("CallStack");
        var frames = new List<global::app.snapshot.@this>();
        var f = new global::app.snapshot.@this();
        f.Write("goalPrPath", "/.build/Start/00. Goal.pr");
        f.Write("goalHash", "abc123");
        f.Write("stepIndex", 3);
        f.Write("actionIndex", 1);
        f.Write("actionModule", "llm");
        f.Write("actionName", "query");
        f.Write("id", "deadbeef");
        frames.Add(f);
        cs.Write("frames", frames);

        var wired = RoundTrip(src, snap);

        var rf = wired.Section("CallStack").Read<List<global::app.snapshot.@this>>("frames")!;
        await Assert.That(rf.Count).IsEqualTo(1);
        await Assert.That(rf[0].Read<int>("stepIndex")).IsEqualTo(3);
        await Assert.That(rf[0].Read<int>("actionIndex")).IsEqualTo(1);
        await Assert.That(rf[0].Read<string>("goalPrPath")).IsEqualTo("/.build/Start/00. Goal.pr");
        await Assert.That(rf[0].Read<string>("actionName")).IsEqualTo("query");
    }

    private static Step SetStep(int index, string varName, object value)
    {
        var action = TestAction.Create("variable", "set", ("name", "%" + varName + "%"), ("value", value));
        var step = new Step { Index = index, Text = $"set %{varName}% = {value}" };
        action.Step = step;
        step.Actions.Add(action);
        return step;
    }

    [Test]
    public async Task EndToEnd_SuspendedState_SurvivesDisk_AndResumesToSuccess()
    {
        // The whole point: capture a suspended/failing position, serialize it to a
        // STRING (the disk shape), read it back, and Resume — re-entering the
        // captured step and running to success with nothing held in memory.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-wire-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = "/G.goal", PrPath = "/G.pr" };
        var step0 = SetStep(0, "s0", "first"); step0.Goal = goal;
        var step1 = SetStep(1, "s1", "second"); step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        // Suspend at step1/action0 (what the throw-time snapshot captures).
        string json;
        await using (var call = context.App.CallStack.Push(step1.Actions[0], context.Variable))
        {
            json = app.SnapshotToWire(app.Snapshot());   // <-- to disk (string)
            await call.DisposeAsync();
        }

        // Resume purely from the serialized string — no in-memory snapshot object.
        var result = await app.ResumeFromWire(json, context);

        await result.IsSuccess();
        // Step 1 ran on resume; step 0 did NOT (we resumed mid-goal).
        await Assert.That(context.Variable.GetValue("s1")).IsEqualTo("second");
        await Assert.That(context.Variable.Get("s0").IsInitialized).IsFalse();
    }

    [Test]
    public async Task EmptyApp_WireIsValidJson_AndRestoresClean()
    {
        var src = new global::app.@this("/src");
        var json = src.SnapshotToWire(src.Snapshot());

        await Assert.That(json.StartsWith("{")).IsTrue();

        var dst = new global::app.@this("/dst");
        dst.Restore(src.SnapshotFromWire(json), dst.User.Context);

        await Assert.That(dst.Builder.IsEnabled).IsFalse();
    }
}
