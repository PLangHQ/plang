using app.error;

namespace PLang.Tests.App.SnapshotTests;

/// <summary>
/// The disk round-trip: Snapshot → SnapshotToWire (JSON string) → SnapshotFromWire
/// → Restore. Proves a captured failure can be persisted and replayed with no live
/// state — the deterministic-replay loop the builder needs.
/// </summary>
public class SnapshotWireTests
{
    private static async Task<global::app.snapshot.@this> RoundTrip(global::app.@this app, global::app.snapshot.@this snap)
        => await app.SnapshotFromWire(await app.SnapshotToWire(snap), snap.Context);

    [Test]
    public async Task Variables_SurviveWireRoundTrip_WithValueAndType()
    {
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.User.Context.Variable.Set("count", 42L);
        src.User.Context.Variable.Set("name", "plang");

        var wired = await RoundTrip(src, src.Snapshot(src.User.Context));

        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(wired, dst.User.Context);

        await Assert.That((await (await dst.User.Context.Variable.Get("count")).Value())?.ToString()).IsEqualTo("42");
        await Assert.That((await (await dst.User.Context.Variable.Get("name")).Value())?.ToString()).IsEqualTo("plang");
    }

    [Test]
    public async Task BuildAndTestingBits_SurviveWireRoundTrip()
    {
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.Build = new global::app.module.build.@this(src.System.Context);
        src.Test = new global::app.test.list.@this(src.System.Context);

        var wired = await RoundTrip(src, src.Snapshot(src.User.Context));

        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(wired, dst.User.Context);

        await Assert.That(dst.Build != null).IsTrue();
        await Assert.That(dst.Test != null).IsTrue();
    }

    [Test]
    [Skip("Deferred to the snapshot-wire redesign. The snapshot's Data-normalization serializes each IError as an empty [Out] property bag instead of deferring to ErrorWire, so Message is dropped on round-trip. Re-asserted against the new snapshot model.")]
    public async Task ErrorsTrail_SurvivesWireRoundTrip_WithContentAndId()
    {
        var src = global::PLang.Tests.TestApp.Create("/src");
        var e1 = new ServiceError("first", "TestErr", 400);
        var e2 = new ServiceError("second", "TestErr", 500);
        using (src.Error.Push(e1, src.User.Context)) { }
        using (src.Error.Push(e2, src.User.Context)) { }

        var wired = await RoundTrip(src, src.Snapshot(src.User.Context));

        var dst = global::PLang.Tests.TestApp.Create("/dst");
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
        var src = global::PLang.Tests.TestApp.Create("/src");
        var snap = new global::app.snapshot.@this(src.User.Context);
        // Emulate one captured frame's scalar shape.
        var cs = snap.Section("CallStack");
        var frames = new List<global::app.snapshot.@this>();
        var f = new global::app.snapshot.@this(src.User.Context);
        f.Write("goalPrPath", "/.build/Start/00. Goal.pr");
        f.Write("goalHash", "abc123");
        f.Write("stepIndex", 3);
        f.Write("actionIndex", 1);
        f.Write("actionModule", "llm");
        f.Write("actionName", "query");
        f.Write("id", "deadbeef");
        frames.Add(f);
        cs.Write("frames", frames);

        var wired = await RoundTrip(src, snap);

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
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-wire-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var step0 = SetStep(0, "s0", "first"); step0.Goal = goal;
        var step1 = SetStep(1, "s1", "second"); step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        // Suspend at step1/action0 (what the throw-time snapshot captures).
        string json;
        await using (var call = context.CallStack.Push(step1.Actions[0], context.Variable))
        {
            json = await app.SnapshotToWire(app.Snapshot(app.User.Context));   // <-- to disk (string)
            await call.DisposeAsync();
        }

        // Resume purely from the serialized string — no in-memory snapshot object.
        var result = await app.ResumeFromWire(json, context);

        await result.IsSuccess();
        // Step 1 ran on resume; step 0 did NOT (we resumed mid-goal).
        await Assert.That((await context.Variable.GetValue("s1"))).IsEqualTo("second");
        await Assert.That((await context.Variable.Get("s0")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess()
    {
        // Proves the consumer-typed deserialize: the wire string converts to a
        // snapshot.@this through the type system (snapshot.FromWire) — exactly what
        // the `resume` verb's Data<snapshot> param triggers at the action boundary —
        // then Resume re-enters the suspended step and succeeds.
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-conv-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var step0 = SetStep(0, "s0", "first"); step0.Goal = goal;
        var step1 = SetStep(1, "s1", "second"); step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        string json;
        await using (var call = context.CallStack.Push(step1.Actions[0], context.Variable))
        {
            json = await app.SnapshotToWire(app.Snapshot(app.User.Context));
            await call.DisposeAsync();
        }

        // The string → snapshot conversion the runtime performs for a Data<snapshot> slot.
        var converted = (await context.App.Type.Convert(json, typeof(global::app.snapshot.@this), context).Value())
            as global::app.snapshot.@this;
        await Assert.That(converted).IsNotNull();

        var result = await converted!.Resume(context);
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("s1"))).IsEqualTo("second");
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
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-mid-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;
        context.Variable.Set("keep", "alive");
        context.Variable.Set("i", 1L);

        // Start: [0] set a, [1] the call to Sub, [2] post-call marker (the unwind proof).
        var start = new Goal { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/Start.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/Start.pr", global::PLang.Tests.TestApp.SharedContext) };
        var st0 = SetStep(0, "a", "A");                 st0.Goal = start;
        var st1 = SetStep(1, "calledSub", "yes");       st1.Goal = start;   // stands in for `call Sub`
        var st2 = SetStepRef(2, "entryReached", "END"); st2.Goal = start;   // post-call: only runs on unwind
        start.Steps.Add(st0); start.Steps.Add(st1); start.Steps.Add(st2);

        // Sub: [0] set b, [1] the throw point (suspended here), [2] continuation reading %i%.
        var sub = new Goal { Name = "Sub", Path = global::app.type.item.path.@this.Resolve("/Sub.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/Sub.pr", global::PLang.Tests.TestApp.SharedContext) };
        var sb0 = SetStep(0, "b", "B");                  sb0.Goal = sub;
        var sb1 = SetStep(1, "passedThrow", "ok");       sb1.Goal = sub;    // the `throw if i==1` step
        var sb2 = SetStepRef(2, "seenI", "%i%");         sb2.Goal = sub;    // reads the patched value
        sub.Steps.Add(sb0); sub.Steps.Add(sb1); sub.Steps.Add(sb2);

        app.Goal.Add(start); app.Goal.Add(sub);

        // Suspend mid-stack: Start at its call step (1,0), Sub at its throw step (1,0).
        string json;
        await using (var startFrame = context.CallStack.Push(start.Steps[1].Actions[0], context.Variable))
        await using (var subFrame = context.CallStack.Push(sub.Steps[1].Actions[0], context.Variable))
        {
            json = await app.SnapshotToWire(app.Snapshot(app.User.Context));
        }

        // Round-trip through the disk string, then patch %i% 1 → 2 (the fix the
        // operator/builder makes — the C# stand-in for `set %snap.variable.i% = 2`).
        var snap = (await new global::app.data.@this("", json, context: context).Value<global::app.snapshot.@this>())!;
        var vars = snap.Section("Variables").Read<List<global::app.data.@this>>("variables")!;
        var iVar = vars.First(v => v.Name == "i");
        iVar.SetValue(2L);

        var result = await snap.Resume(context);

        await result.IsSuccess();
        // Sub re-entered at step 1 and continued — and saw the PATCHED %i%.
        await Assert.That((await context.Variable.GetValue("passedThrow"))).IsEqualTo("ok");
        await Assert.That(System.Convert.ToInt64((await context.Variable.GetValue("seenI")))).IsEqualTo(2L);
        // The stack unwound to the entry goal's post-call step.
        await Assert.That((await context.Variable.GetValue("entryReached"))).IsEqualTo("END");
        // Survivor var intact across the whole round-trip.
        await Assert.That((await context.Variable.GetValue("keep"))).IsEqualTo("alive");
    }

    [Test]
    public async Task PlangPath_AsSnapshotConvert_EditSurvivesResume()
    {
        // Mirrors the .test.goal EXACTLY: the read result (an envelope STRING) goes
        // through `as snapshot` → variable.set's typeEntity.Convert (set.cs:218),
        // NOT Deserialize directly. Isolates whether that conversion yields a
        // navigable snapshot.@this and whether the edit survives resume.
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-aspath-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var step0 = SetStep(0, "x", "1");          step0.Goal = goal;
        var step1 = SetStepRef(1, "seen", "%x%");  step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.CallStack.Push(goal.Steps[1].Actions[0], context.Variable))
        {
            json = await app.SnapshotToWire(app.Snapshot(app.User.Context));
        }

        // `as snapshot` path: typeEntity.Create(envelopeString, context). A wire-raw string
        // defers to a lazy source declared {snapshot}; it materializes to the snapshot on .Value().
        var te = new global::app.type.@this("snapshot") { Context = context };
        var conv = await new global::app.data.@this("", te.Create(json, context), context: context).Value();
        await Assert.That(conv is global::app.snapshot.@this)
            .IsTrue(); // ← if false, the as-snapshot conversion is the bug

        context.Variable.Set("snap", conv);
        context.Variable.Set("snap.variables.x", 2L);
        await Assert.That(System.Convert.ToInt64((await (await context.Variable.Get("snap.variables.x")).Value()))).IsEqualTo(2L);

        var snap = (await (await context.Variable.Get("snap")).Value()) as global::app.snapshot.@this;
        await Assert.That(snap).IsNotNull();
        var result = await snap!.Resume(context);
        await result.IsSuccess();
        await Assert.That(System.Convert.ToInt64((await context.Variable.GetValue("seen")))).IsEqualTo(2L);
    }

    [Test]
    public async Task FileSave_OfSnapshot_UnregisteredExtension_WritesJsonContent()
    {
        // `save %snapshot% to file 'foo.snapshot'`: an unregistered extension falls to the Text
        // serializer, and the text writer renders a container AS JSON (the writer owns
        // BeginObject/BeginArray — no per-type override, no shape selector). So a snapshot writes
        // its json content — the snapshot's own sections, nested Data self-describing via @schema —
        // NOT the top-level plang wire envelope. (A resumable save uses a plang-registered extension.)
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-fs-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;
        context.Variable.Set("x", 1L);

        var snap = app.Snapshot(app.User.Context);
        var d = new global::app.data.@this<global::app.snapshot.@this>("", snap,
            new global::app.type.@this("snapshot"), context: context);

        using var ms = new System.IO.MemoryStream();
        // file-save owns its selector (its Extension); an unregistered one falls to Text (content).
        var serializers = context.Actor.Channel.Serializers;
        var serializer = serializers.GetByExtension(".snapshot") ?? serializers.Text;
        var result = await serializer.SerializeAsync(ms, d);

        var content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result.Success).IsTrue();
        // json content: the snapshot's own object, its sections present — the value wrote itself.
        await Assert.That(content.StartsWith("{")).IsTrue();
        await Assert.That(content).Contains("\"Variables\"");
    }

    [Test]
    public async Task ThrowTimeSnapshot_EditSurvivesResume()
    {
        // The ONE difference from the passing edit-resume tests: the snapshot comes
        // from app.Snapshot(error) (throw-time: SnapshotAt + error.CallFrames), the
        // path Error.Callback uses in the .test.goal — not app.Snapshot(app.User.Context) (live).
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-tt-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var step0 = SetStep(0, "x", "1");          step0.Goal = goal;
        var step1 = SetStepRef(1, "seen", "%x%");  step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.CallStack.Push(goal.Steps[1].Actions[0], context.Variable))
        {
            var err = new ServiceError("boom", goal.Steps[1],
                context.CallStack.Current!.SnapshotChain());
            json = await app.SnapshotToWire(app.Snapshot(err, app.User.Context));   // throw-time overload
        }

        var te = new global::app.type.@this("snapshot") { Context = context };
        // Wire-raw string → lazy source declared {snapshot}; materialize it to the snapshot.
        var snap = await new global::app.data.@this("", te.Create(json, context), context: context).Value()
            as global::app.snapshot.@this;
        await Assert.That(snap).IsNotNull();

        context.Variable.Set("snap", snap);
        context.Variable.Set("snap.variables.x", 2L);
        await Assert.That(System.Convert.ToInt64((await (await context.Variable.Get("snap.variables.x")).Value()))).IsEqualTo(2L);

        var result = await snap!.Resume(context);
        await result.IsSuccess();
        await Assert.That(System.Convert.ToInt64((await context.Variable.GetValue("seen")))).IsEqualTo(2L);
    }

    [Test]
    public async Task TypedSnapshotString_NavigateEditResume_PersistsEdit()
    {
        // The `as snapshot` contract: %snap% is a Data with a string Value (wire bytes
        // off disk) but Type=snapshot. Navigating %snap.variables.x% must materialize
        // and cache the snapshot so an edit persists into resume — proving the runtime
        // honours a typed-string snapshot end-to-end (the cast must reach this Data;
        // see RawStringInSnap counterpart: an untyped string loses the edit).
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-typed-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var step0 = SetStep(0, "x", "1");          step0.Goal = goal;
        var step1 = SetStepRef(1, "seen", "%x%");  step1.Goal = goal;
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.CallStack.Push(goal.Steps[1].Actions[0], context.Variable))
            json = await app.SnapshotToWire(app.Snapshot(app.User.Context));

        // %snap% = string value, but TYPED as snapshot (what an honored `as snapshot` yields).
        context.Variable.Set(new global::app.data.@this(
            "snap", json, new global::app.type.@this("snapshot"), context: context));

        context.Variable.Set("snap.variables.x", 2L);

        var snap = await (await context.Variable.Get("snap")).Value<global::app.snapshot.@this>();
        var result = await snap!.Resume(context);
        await result.IsSuccess();
        long seen = System.Convert.ToInt64((await context.Variable.GetValue("seen")));
        await Assert.That(seen).IsEqualTo(2L);  // edit persists IFF navigation materializes+caches
    }

    [Test]
    public async Task NavigateAndEditCapturedVariable_ThenResumeToSuccess()
    {
        // The PLang fix-and-replay loop, in C#: read a snapshot back, navigate
        // %snap.variables.x% (read), edit it (set), then resume — the edit flows
        // into resumed execution. Mirrors the .test.goal.
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-nav-" + System.Guid.NewGuid().ToString("N")[..8]));
        var context = app.User.Context;

        var goal = new Goal { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var step0 = SetStep(0, "x", "1");                step0.Goal = goal;
        var step1 = SetStepRef(1, "seen", "%x%");        step1.Goal = goal;   // reads the edited value
        goal.Steps.Add(step0); goal.Steps.Add(step1);
        app.Goal.Add(goal);

        // Suspend at step1 with %x% = 1 captured.
        context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.CallStack.Push(goal.Steps[1].Actions[0], context.Variable))
        {
            json = await app.SnapshotToWire(app.Snapshot(app.User.Context));
        }

        // Read the snapshot back as a value, bind it under %snap%.
        var snap = (await new global::app.data.@this("", json, context: context).Value<global::app.snapshot.@this>())!;
        context.Variable.Set("snap", snap);

        // Navigate + read: %snap.variables.x% is 1.
        await Assert.That(System.Convert.ToInt64((await (await context.Variable.Get("snap.variables.x")).Value()))).IsEqualTo(1L);

        // Edit: set %snap.variables.x% = 2 — routes to the snapshot's SetVariable.
        context.Variable.Set("snap.variables.x", 2L);
        await Assert.That(System.Convert.ToInt64((await (await context.Variable.Get("snap.variables.x")).Value()))).IsEqualTo(2L);

        // Resume the edited snapshot — step1 reads the patched %x%.
        var result = await snap.Resume(context);
        await result.IsSuccess();
        await Assert.That(System.Convert.ToInt64((await context.Variable.GetValue("seen")))).IsEqualTo(2L);
    }

    [Test]
    public async Task EmptyApp_WireIsValidJson_AndRestoresClean()
    {
        var src = global::PLang.Tests.TestApp.Create("/src");
        var json = await src.SnapshotToWire(src.Snapshot(src.User.Context));

        await Assert.That(json.StartsWith("{")).IsTrue();

        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(await src.SnapshotFromWire(json, dst.User.Context), dst.User.Context);

        await Assert.That(dst.Build != null).IsFalse();
    }
}
