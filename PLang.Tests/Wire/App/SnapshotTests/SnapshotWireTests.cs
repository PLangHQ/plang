using app.error;

namespace PLang.Tests.App.SnapshotTests;

/// <summary>
/// The disk round-trip: Snapshot → its text (what a file save writes) → the snapshot that text is asked as
/// → Restore. Proves a captured failure can be persisted and replayed with no live
/// state — the deterministic-replay loop the builder needs.
/// </summary>
public class SnapshotWireTests
{
    private static async Task<global::app.snapshot.@this> RoundTrip(global::app.@this app, global::app.snapshot.@this snap)
        => await snap.Context.Snapshot(await snap.Text(snap.Context));

    [Test]
    public async Task Variables_SurviveWireRoundTrip_WithValueAndType()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        await src.actor.list.User.Context.Variable.Set("count", 42L);
        await src.actor.list.User.Context.Variable.Set("name", "plang");

        var wired = await RoundTrip(src, src.Snapshot(src.actor.list.User.Context).Saved());

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        await dst.Restore(wired, dst.actor.list.User.Context);

        await Assert.That((await (await dst.actor.list.User.Context.Variable.Get("count")).Value())?.ToString()).IsEqualTo("42");
        await Assert.That((await (await dst.actor.list.User.Context.Variable.Get("name")).Value())?.ToString()).IsEqualTo("plang");
    }

    [Test]
    public async Task BuildAndTestingBits_DontRideTheWire()
    {
        // The App's mode is how it was started, the host's choice: a snapshot of a building app, through the wire,
        // leaves a testing destination testing and not building.
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        src.Build = new global::app.module.build.@this(src.actor.list.System.Context);

        var wired = await RoundTrip(src, src.Snapshot(src.actor.list.User.Context).Saved());

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        await dst.Restore(wired, dst.actor.list.User.Context);

        await Assert.That(dst.Build == null).IsTrue();
        await Assert.That(dst.test.list.Session != null).IsTrue();
    }

    // The errors-trail wire test is gone with the trail itself: the run-wide error log is
    // CallStack.Audit, which rides the CallStack section, and the error in play lives on the
    // frame that failed. It was skipped from the day it was written (the snapshot serialized
    // each global::app.error.@this as an empty [Out] bag), so nothing is losing coverage here.

    [Test]
    public async Task CallStackFrames_Scalars_RoundTripWithIntTyping()
    {
        // Build a frame section by hand the way call.@this.Capture does, then drive
        // it through the wire. The int keys must come back as int (not long) so
        // CallStack.Restore's Read<int> resolves them.
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var snap = new global::app.snapshot.@this(src.actor.list.User.Context);
        // Emulate one captured frame's scalar shape.
        var cs = snap.Node(SnapshotPathExtensions.UserCall);
        var frames = new List<global::app.snapshot.@this>();
        var f = new global::app.snapshot.@this(src.actor.list.User.Context);
        f.Set("goalPrPath", "/.build/Start/00. Goal.pr");
        f.Set("goalHash", "abc123");
        f.Set("stepIndex", 3);
        f.Set("actionIndex", 1);
        f.Set("actionModule", "llm");
        f.Set("actionName", "query");
        f.Set("id", "deadbeef");
        frames.Add(f);
        cs.Set("frames", frames);

        var wired = await RoundTrip(src, snap);

        var rf = await wired.Node(SnapshotPathExtensions.UserCall).Frames("frames")!;
        await Assert.That(rf.Count).IsEqualTo(1);
        await Assert.That(await rf[0].Int("stepIndex")).IsEqualTo(3);
        await Assert.That(await rf[0].Int("actionIndex")).IsEqualTo(1);
        await Assert.That(await rf[0].Text("goalPrPath")).IsEqualTo("/.build/Start/00. Goal.pr");
        await Assert.That(await rf[0].Text("actionName")).IsEqualTo("query");
    }

    // A step is born knowing its goal, and the goal is born knowing the step.
    // A value as a formal line writes it: a %variable% bare, anything else a JSON literal.
    private static string Formal(object value)
        => value is string s && s.Length > 1 && s.StartsWith('%') && s.EndsWith('%') ? s : System.Text.Json.JsonSerializer.Serialize(value);

    private static async Task<Step> SetStep(global::app.actor.context.@this context, Goal goal, int index, string varName, object value)
    {
        var action = await context.Action($"variable.set(Name=%{varName}%, Value={Formal(value)})");
        var step = new Step(context.App.type.list) { Goal = goal, Index = index, Text = $"set %{varName}% = {value}" };
        action = action.In(step);
        step.Code.Add(action);
        goal.Step.Add(step);
        return step;
    }

    [Test]
    public async Task EndToEnd_SuspendedState_SurvivesDisk_AndResumesToSuccess()
    {
        // The whole point: capture a suspended/failing position, serialize it to a
        // STRING (the disk shape), read it back, and Resume — re-entering the
        // captured step and running to success with nothing held in memory.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-wire-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;

        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        await SetStep(context, goal, 0, "s0", "first");
        var step1 = await SetStep(context, goal, 1, "s1", "second");
        app.goal.list.Add(goal);

        // Suspend at step1/action0 (what the throw-time snapshot captures).
        string json;
        await using (var call = context.call.Push(step1.Code[0], await context.call.Setting(), context.Variable))
        {
            json = await app.Snapshot(app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);   // <-- to disk (string)
            await call.DisposeAsync();
        }

        // Resume purely from the serialized string — no in-memory snapshot object.
        var result = await (await context.Snapshot(json)).Resume(context);

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
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-conv-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;

        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        await SetStep(context, goal, 0, "s0", "first");
        var step1 = await SetStep(context, goal, 1, "s1", "second");
        app.goal.list.Add(goal);

        string json;
        await using (var call = context.call.Push(step1.Code[0], await context.call.Setting(), context.Variable))
        {
            json = await app.Snapshot(app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);
            await call.DisposeAsync();
        }

        // The string → snapshot conversion the runtime performs for a Data<snapshot> slot:
        // the snapshot builds ITSELF from its wire string through its own Create door.
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(new global::app.type.@this("snapshot")), context: context);
        var converted = global::app.snapshot.@this.Create(context.Text(json), null, carrier);
        await Assert.That(converted).IsNotNull();

        var result = await converted!.Resume(context);
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("s1"))).IsEqualTo("second");
    }

    private static async Task<Step> SetStepRef(global::app.actor.context.@this context, Goal goal, int index, string varName, string expr)
    {
        var action = await context.Action($"variable.set(Name=%{varName}%, Value={Formal(expr)})");
        var step = new Step(context.App.type.list) { Goal = goal, Index = index, Text = $"set %{varName}% = {expr}" };
        action = action.In(step);
        step.Code.Add(action);
        goal.Step.Add(step);
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
            System.IO.Path.GetTempPath(), "plang-mid-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("keep", "alive");
        await context.Variable.Set("i", 1L);

        // Start: [0] set a, [1] the call to Sub, [2] post-call marker (the unwind proof).
        var start = new Goal(app.type.list) { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/Start.goal", app.actor.list.User.Context) };
        await SetStep(context, start, 0, "a", "A");
        await SetStep(context, start, 1, "calledSub", "yes");          // stands in for `call Sub`
        await SetStepRef(context, start, 2, "entryReached", "END");    // post-call: only runs on unwind

        // Sub: [0] set b, [1] the throw point (suspended here), [2] continuation reading %i%.
        var sub = new Goal(app.type.list) { Name = "Sub", Path = global::app.type.item.path.@this.Resolve("/Sub.goal", app.actor.list.User.Context) };
        await SetStep(context, sub, 0, "b", "B");
        await SetStep(context, sub, 1, "passedThrow", "ok");           // the `throw if i==1` step
        await SetStepRef(context, sub, 2, "seenI", "%i%");             // reads the patched value

        app.goal.list.Add(start); app.goal.list.Add(sub);

        // Suspend mid-stack, each goal in its own frames as a run holds them: Start at its call step (1,0), Sub at its
        // throw step (1,0).
        string json;
        var setting = await context.call.Setting();
        await using (context.call.Push(start, setting))
        await using (context.call.Push(start.Step[1], setting))
        await using (context.call.Push(start.Step[1].Code[0], setting, context.Variable))
        await using (context.call.Push(sub, setting))
        await using (context.call.Push(sub.Step[1], setting))
        await using (context.call.Push(sub.Step[1].Code[0], setting, context.Variable))
        {
            json = await app.Snapshot(app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);
        }

        // Round-trip through the disk string, then patch %i% 1 → 2 (the fix the
        // operator/builder makes — the C# stand-in for `set %snap.variable.i% = 2`).
        var snap = (await new global::app.data.@this("", json, context: context).Value<global::app.snapshot.@this>())!;
        var iVar = snap.Node(SnapshotPathExtensions.UserVariable).Entries.Get("i", app.actor.list.User.Context)!;   // each captured variable is its own entry
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
    public async Task ResumeInsideARunningGoal_IsACall_GoesOnThroughTheRestoredFramesOnly()
    {
        // resume %snapshot% is a call: the restored tree goes under the resume action's frame and its run ends there —
        // the resuming goal's own next step runs once, after the resume, never inside it
        var src = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-incall-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        var sub = new Goal(src.type.list) { Name = "Sub", Path = global::app.type.item.path.@this.Resolve("/Sub.goal", context) };
        await SetStep(context, sub, 0, "b", "B");
        await SetStep(context, sub, 1, "saved", "here");
        await SetStep(context, sub, 2, "after", "went on");
        src.goal.list.Add(sub);
        string json;
        var setting = await context.call.Setting();
        await using (context.call.Push(sub, setting))
        await using (context.call.Push(sub.Step[1], setting))
        await using (context.call.Push(sub.Step[1].Code[0], setting, context.Variable))
            json = await src.Snapshot(context).Saved().Text(context);

        var dst = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-incall-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        var dstSub = new Goal(dst.type.list) { Name = "Sub", Path = global::app.type.item.path.@this.Resolve("/Sub.goal", into) };
        await SetStep(into, dstSub, 0, "b", "B");
        await SetStep(into, dstSub, 1, "saved", "here");
        await SetStep(into, dstSub, 2, "after", "went on");
        var outer = new Goal(dst.type.list) { Name = "Outer", Path = global::app.type.item.path.@this.Resolve("/Outer.goal", into) };
        await SetStep(into, outer, 0, "resuming", "yes");                 // stands in for `resume %snapshot%`
        await SetStep(into, outer, 1, "outerNext", "ran");
        dst.goal.list.Add(dstSub); dst.goal.list.Add(outer);
        var snap = (await new global::app.data.@this("", json, context: into).Value<global::app.snapshot.@this>())!;

        var running = await into.call.Setting();
        await using (into.call.Push(outer, running))
        await using (into.call.Push(outer.Step[0], running))
        await using (var resuming = into.call.Push(outer.Step[0].Code[0], running, into.Variable))
        {
            var result = await snap.Resume(into);

            await result.IsSuccess();
            await Assert.That(await into.Variable.GetValue("after")).IsEqualTo("went on");     // the restored run went on
            await Assert.That(await into.Variable.GetValue("outerNext")).IsNull();             // and ended at the resume
            await Assert.That(into.call.Current).IsSameReferenceAs(resuming);
        }
    }

    [Test]
    public async Task AnAppsMode_IsNotStored_ARestoreLeavesTheAppAsItWasStarted()
    {
        // how an app was started (test, build, run) is the host's choice, not the program's state
        var src = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-mode-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var snap = src.Snapshot(src.actor.list.User.Context).Saved();
        await Assert.That(snap.Has("mode")).IsFalse();

        var dst = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-mode-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var session = dst.test.list.Session;
        await dst.Restore(snap, dst.actor.list.User.Context);
        await Assert.That(dst.test.list.Session).IsSameReferenceAs(session);
    }

    [Test]
    public async Task PlangPath_AsSnapshotConvert_EditSurvivesResume()
    {
        // Mirrors the .test.goal EXACTLY: the read result (an envelope STRING) goes
        // through `as snapshot` → variable.set's typeEntity.Convert (set.cs:218),
        // NOT Deserialize directly. Isolates whether that conversion yields a
        // navigable snapshot.@this and whether the edit survives resume.
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-aspath-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;

        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        var step0 = await SetStep(context, goal, 0, "x", "1");
        var step1 = await SetStepRef(context, goal, 1, "seen", "%x%");
        app.goal.list.Add(goal);

        await context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.call.Push(goal.Step[1].Code[0], await context.call.Setting(), context.Variable))
        {
            json = await app.Snapshot(app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);
        }

        // `as snapshot` path: typeEntity.Create(envelopeString, context). A wire-raw string
        // defers to a lazy source declared {snapshot}; it materializes to the snapshot on .Value().
        var te = new global::app.type.@this("snapshot");
        var conv = await new global::app.data.@this("", te.Make(json, context), context: context).Value();
        await Assert.That(conv is global::app.snapshot.@this)
            .IsTrue(); // ← if false, the as-snapshot conversion is the bug

        await context.Variable.Set("snap", conv);
        await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Set(2L, context);
        await Assert.That(System.Convert.ToInt64((await (await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Start(context)).Value()))).IsEqualTo(2L);

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
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-fs-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("x", 1L);

        var snap = app.Snapshot(app.actor.list.User.Context).Saved();
        var d = new global::app.data.@this<global::app.snapshot.@this>("", snap,
            new global::app.type.@this("snapshot"), context: context);

        using var ms = new System.IO.MemoryStream();
        // file-save's format is its extension's; one that writes nothing (NoEncoder) falls to text (content).
        var result = await context.App.type.list.Extension(".snapshot", context).kind.Encode(ms, d, context);
        if (!result.Success && result.Error?.Key == "NoEncoder")
        {
            ms.SetLength(0);
            result = await context.App.type.list["text"].kind.Encode(ms, d, context);
        }

        var content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result.Success).IsTrue();
        // json content: the snapshot's own object, its sections present — the value wrote itself.
        await Assert.That(content.StartsWith("{")).IsTrue();
        await Assert.That(content).Contains("\"Variable\"");
    }

    [Test]
    public async Task ThrowTimeSnapshot_EditSurvivesResume()
    {
        // The ONE difference from the passing edit-resume tests: the snapshot comes
        // from app.Snapshot(error).Saved() (throw-time: SnapshotAt + error.CallFrames), the
        // path Error.Callback uses in the .test.goal — not app.Snapshot(app.actor.list.User.Context).Saved() (live).
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-tt-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;

        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        var step0 = await SetStep(context, goal, 0, "x", "1");
        var step1 = await SetStepRef(context, goal, 1, "seen", "%x%");
        app.goal.list.Add(goal);

        await context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.call.Push(goal.Step[1].Code[0], await context.call.Setting(), context.Variable))
        {
            var err = new ServiceError("boom", goal.Step[1],
                context.call.Current!.Chain);
            json = await app.Snapshot(err, app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);   // throw-time overload
        }

        var te = new global::app.type.@this("snapshot");
        // Wire-raw string → lazy source declared {snapshot}; materialize it to the snapshot.
        var snap = await new global::app.data.@this("", te.Make(json, context), context: context).Value()
            as global::app.snapshot.@this;
        await Assert.That(snap).IsNotNull();

        await context.Variable.Set("snap", snap);
        await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Set(2L, context);
        await Assert.That(System.Convert.ToInt64((await (await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Start(context)).Value()))).IsEqualTo(2L);

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
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-typed-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;

        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        var step0 = await SetStep(context, goal, 0, "x", "1");
        var step1 = await SetStepRef(context, goal, 1, "seen", "%x%");
        app.goal.list.Add(goal);

        await context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.call.Push(goal.Step[1].Code[0], await context.call.Setting(), context.Variable))
            json = await app.Snapshot(app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);

        // %snap% = string value, but TYPED as snapshot (what an honored `as snapshot` yields).
        await context.Variable.Set(new global::app.data.@this(
            "snap", json, new global::app.type.@this("snapshot"), context: context));

        await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Set(2L, context);

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
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-nav-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;

        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        var step0 = await SetStep(context, goal, 0, "x", "1");
        var step1 = await SetStepRef(context, goal, 1, "seen", "%x%");   // reads the edited value
        app.goal.list.Add(goal);

        // Suspend at step1 with %x% = 1 captured.
        await context.Variable.Set("x", 1L);
        string json;
        await using (var call = context.call.Push(goal.Step[1].Code[0], await context.call.Setting(), context.Variable))
        {
            json = await app.Snapshot(app.actor.list.User.Context).Saved().Text(app.actor.list.User.Context);
        }

        // Read the snapshot back as a value, bind it under %snap%.
        var snap = (await new global::app.data.@this("", json, context: context).Value<global::app.snapshot.@this>())!;
        await context.Variable.Set("snap", snap);

        // Navigate + read: %snap.variables.x% is 1.
        await Assert.That(System.Convert.ToInt64((await (await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Start(context)).Value()))).IsEqualTo(1L);

        // Edit: set %snap.variables.x% = 2 — the snapshot's own entry navigation edits that entry.
        await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Set(2L, context);
        await Assert.That(System.Convert.ToInt64((await (await new global::app.type.item.variable.@this("snap.actor.list.user.context.variable.x", context.App.type.list).Start(context)).Value()))).IsEqualTo(2L);

        // Resume the edited snapshot — step1 reads the patched %x%.
        var result = await snap.Resume(context);
        await result.IsSuccess();
        await Assert.That(System.Convert.ToInt64((await context.Variable.GetValue("seen")))).IsEqualTo(2L);
    }

    // a snapshot never carries a sensitive value: a variable holding one is not saved, so a resumed run asks again
    [Test]
    public async Task ASensitiveVariable_IsNotInTheSnapshotsText()
    {
        var app = new global::app.@this("/sensitive", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("password", new global::app.type.item.sensitive.@this("hunter2", app.type.list[typeof(global::app.type.item.sensitive.@this)]));

        var text = await app.Snapshot(context).Saved().Text(context);

        await Assert.That(text).DoesNotContain("hunter2");
        await Assert.That((await context.Snapshot(text)).Node(SnapshotPathExtensions.UserVariable).Entries.Get("password", context)).IsNull();
    }

    // a [Sensitive] member of a value a variable holds is not in the snapshot's text (an identity's private key); red
    // until the snapshot's write has a view of its own (plang's own store shows a [Sensitive, Store] member whole)
    [Test, TUnit.Core.Property("carried", "test/plan/snapshot")]
    public async Task ASensitiveMember_IsNotInTheSnapshotsText()
    {
        var app = new global::app.@this("/sensitive", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var identity = new global::app.module.identity.Identity("me", app.type.list[typeof(global::app.module.identity.Identity)])
            { PrivateKey = "the-private-key" };
        await context.Variable.Set("id", identity);

        var text = await app.Snapshot(context).Saved().Text(context);

        await Assert.That(text).DoesNotContain("the-private-key");
    }

    [Test]
    public async Task EmptyApp_WireIsValidJson_AndRestoresClean()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var json = await src.Snapshot(src.actor.list.User.Context).Saved().Text(src.actor.list.User.Context);

        await Assert.That(json.StartsWith("{")).IsTrue();

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        await dst.Restore(await dst.actor.list.User.Context.Snapshot(json), dst.actor.list.User.Context);

        await Assert.That(dst.Build != null).IsFalse();
    }
}
