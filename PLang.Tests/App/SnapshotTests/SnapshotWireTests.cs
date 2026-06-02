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
    public async Task SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess()
    {
        // Proves the consumer-typed deserialize: the wire string converts to a
        // snapshot.@this through the type system (snapshot.FromWire) — exactly what
        // the `resume` verb's Data<snapshot> param triggers at the action boundary —
        // then Resume re-enters the suspended step and succeeds.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-conv-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = "/G.goal", PrPath = "/G.pr" };
        var step0 = SetStep(0, "s0", "first"); step0.Goal = goal;
        var step1 = SetStep(1, "s1", "second"); step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        string json;
        await using (var call = context.App.CallStack.Push(step1.Actions[0], context.Variable))
        {
            json = app.SnapshotToWire(app.Snapshot());
            await call.DisposeAsync();
        }

        // The string → snapshot conversion the runtime performs for a Data<snapshot> slot.
        var converted = context.App.Type.Convert(json, typeof(global::app.snapshot.@this), context).Value
            as global::app.snapshot.@this;
        await Assert.That(converted).IsNotNull();

        var result = await converted!.Resume(context);
        await result.IsSuccess();
        await Assert.That(context.Variable.GetValue("s1")).IsEqualTo("second");
    }

    private static Step SetStepRef(int index, string varName, string expr)
    {
        var action = TestAction.Create("variable", "set", ("name", "%" + varName + "%"), ("value", expr));
        var step = new Step { Index = index, Text = $"set %{varName}% = {expr}" };
        action.Step = step;
        step.Actions.Add(action);
        return step;
    }

    [Test]
    public async Task MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal()
    {
        // The realistic shape: Start sets vars and calls Sub; Sub sets a var and
        // suspends at a NON-zero step (where "throw if i==1" would fire). We
        // serialize that 2-frame chain to a disk string, convert it back, patch
        // the captured %i% (1 → 2 — the fix), and Resume. Proof points:
        //  - Sub re-enters mid-goal (step 1, not 0) and its continuation reads the
        //    PATCHED %i% — so the edit flowed into resumed execution.
        //  - The stack unwinds: the entry goal Start runs its POST-call step.
        //  - Survivor vars are intact.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-mid-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;
        context.Variable.Set("keep", "alive");
        context.Variable.Set("i", 1L);

        // Start: [0] set a, [1] the call to Sub, [2] post-call marker (the unwind proof).
        var start = new Goal { Name = "Start", Path = "/Start.goal", PrPath = "/Start.pr" };
        var st0 = SetStep(0, "a", "A");                 st0.Goal = start;
        var st1 = SetStep(1, "calledSub", "yes");       st1.Goal = start;   // stands in for `call Sub`
        var st2 = SetStepRef(2, "entryReached", "END"); st2.Goal = start;   // post-call: only runs on unwind
        start.Steps.Add(st0); start.Steps.Add(st1); start.Steps.Add(st2);

        // Sub: [0] set b, [1] the throw point (suspended here), [2] continuation reading %i%.
        var sub = new Goal { Name = "Sub", Path = "/Sub.goal", PrPath = "/Sub.pr" };
        var sb0 = SetStep(0, "b", "B");                  sb0.Goal = sub;
        var sb1 = SetStep(1, "passedThrow", "ok");       sb1.Goal = sub;    // the `throw if i==1` step
        var sb2 = SetStepRef(2, "seenI", "%i%");         sb2.Goal = sub;    // reads the patched value
        sub.Steps.Add(sb0); sub.Steps.Add(sb1); sub.Steps.Add(sb2);

        app.Goal.Add(start); app.Goal.Add(sub);

        // Suspend mid-stack: Start at its call step (1,0), Sub at its throw step (1,0).
        string json;
        await using (var startFrame = context.App.CallStack.Push(start.Steps[1].Actions[0], context.Variable))
        await using (var subFrame = context.App.CallStack.Push(sub.Steps[1].Actions[0], context.Variable))
        {
            json = app.SnapshotToWire(app.Snapshot());
        }

        // Round-trip through the disk string, then patch %i% 1 → 2 (the fix the
        // operator/builder makes — the C# stand-in for `set %snap.variable.i% = 2`).
        var snap = global::app.snapshot.@this.Deserialize(json, context);
        var vars = snap.Section("Variables").Read<List<global::app.data.@this>>("variables")!;
        var iVar = vars.First(v => v.Name == "i");
        iVar.Value = 2L;

        var result = await snap.Resume(context);

        await result.IsSuccess();
        // Sub re-entered at step 1 and continued — and saw the PATCHED %i%.
        await Assert.That(context.Variable.GetValue("passedThrow")).IsEqualTo("ok");
        await Assert.That(System.Convert.ToInt64(context.Variable.GetValue("seenI"))).IsEqualTo(2L);
        // The stack unwound to the entry goal's post-call step.
        await Assert.That(context.Variable.GetValue("entryReached")).IsEqualTo("END");
        // Survivor var intact across the whole round-trip.
        await Assert.That(context.Variable.GetValue("keep")).IsEqualTo("alive");
    }

    [Test]
    public async Task PlangPath_AsSnapshotConvert_EditSurvivesResume()
    {
        // Mirrors the .test.goal EXACTLY: the read result (an envelope STRING) goes
        // through `as snapshot` → variable.set's typeEntity.Convert (set.cs:218),
        // NOT Deserialize directly. Isolates whether that conversion yields a
        // navigable snapshot.@this and whether the edit survives resume.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-aspath-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = "/G.goal", PrPath = "/G.pr" };
        var step0 = SetStep(0, "x", "1");          step0.Goal = goal;
        var step1 = SetStepRef(1, "seen", "%x%");  step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.App.CallStack.Push(goal.Steps[1].Actions[0], context.Variable))
        {
            json = app.SnapshotToWire(app.Snapshot());
        }

        // `as snapshot` path: typeEntity.Convert(envelopeString, context).
        var te = new global::app.type.@this("snapshot") { Context = context };
        var conv = te.Convert(json, context);
        await Assert.That(conv.Value is global::app.snapshot.@this)
            .IsTrue(); // ← if false, the as-snapshot conversion is the bug

        context.Variable.Set("snap", conv.Value);
        context.Variable.Set("snap.variables.x", 2L);
        await Assert.That(System.Convert.ToInt64(context.Variable.Get("snap.variables.x").Value)).IsEqualTo(2L);

        var snap = context.Variable.Get("snap").Value as global::app.snapshot.@this;
        await Assert.That(snap).IsNotNull();
        var result = await snap!.Resume(context);
        await result.IsSuccess();
        await Assert.That(System.Convert.ToInt64(context.Variable.GetValue("seen"))).IsEqualTo(2L);
    }

    [Test]
    public async Task FileSave_OfSnapshot_ThroughChannel_ProducesWireEnvelope()
    {
        // `save %x% to file 'foo.snapshot'`: an unknown extension must serialize a
        // structured Data through the Wire serializer (content-aware fallback),
        // not the plain application/json STJ path which can't render snapshot.@this.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-fs-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;
        context.Variable.Set("x", 1L);

        var snap = app.Snapshot();
        var d = new global::app.data.@this<global::app.snapshot.@this>("", snap,
            new global::app.type.@this("snapshot")) { Context = context };

        using var ms = new System.IO.MemoryStream();
        var result = await context.Actor.Channel.Serializers.SerializeAsync(
            new global::app.channel.serializer.list.SerializeOptions
            { Stream = ms, Data = d, Extension = ".snapshot" });

        // file.save of a snapshot must produce the wire envelope (content-aware
        // fallback routes structured Data to the Wire serializer, not plain STJ).
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ms.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task NavigateAndEditCapturedVariable_ThenResumeToSuccess()
    {
        // The PLang fix-and-replay loop, in C#: read a snapshot back, navigate
        // %snap.variables.x% (read), edit it (set), then resume — the edit flows
        // into resumed execution. Mirrors the .test.goal.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-nav-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = "/G.goal", PrPath = "/G.pr" };
        var step0 = SetStep(0, "x", "1");                step0.Goal = goal;
        var step1 = SetStepRef(1, "seen", "%x%");        step1.Goal = goal;   // reads the edited value
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        // Suspend at step1 with %x% = 1 captured.
        context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.App.CallStack.Push(goal.Steps[1].Actions[0], context.Variable))
        {
            json = app.SnapshotToWire(app.Snapshot());
        }

        // Read the snapshot back as a value, bind it under %snap%.
        var snap = global::app.snapshot.@this.Deserialize(json, context);
        context.Variable.Set("snap", snap);

        // Navigate + read: %snap.variables.x% is 1.
        await Assert.That(System.Convert.ToInt64(context.Variable.Get("snap.variables.x").Value)).IsEqualTo(1L);

        // Edit: set %snap.variables.x% = 2 — routes to the snapshot's SetVariable.
        context.Variable.Set("snap.variables.x", 2L);
        await Assert.That(System.Convert.ToInt64(context.Variable.Get("snap.variables.x").Value)).IsEqualTo(2L);

        // Resume the edited snapshot — step1 reads the patched %x%.
        var result = await snap.Resume(context);
        await result.IsSuccess();
        await Assert.That(System.Convert.ToInt64(context.Variable.GetValue("seen"))).IsEqualTo(2L);
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
