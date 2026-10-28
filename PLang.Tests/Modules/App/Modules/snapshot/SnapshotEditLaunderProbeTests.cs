using Identity = global::app.module.identity.Identity;

namespace PLang.Tests.App.Modules.snapshot;

// Security probe (snapshot 283): a navigation set into a held snapshot (%snap.app....% = v) must never turn bytes the app
// did not sign into bytes it vouches for. Each case names where the edit lands and what a resume or a re-read then does.
public class SnapshotEditLaunderProbeTests
{
    private static global::app.@this App()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-launder-" + System.Guid.NewGuid().ToString("N")[..8]),
            global::PLang.Tests.Parent.Actor);
        app.test.list.Open(app.actor.list.User);
        app.Code.Register<global::app.module.identity.code.IIdentity>(new global::PLang.Tests.Shared.TestIdentity { IsBuiltIn = true });
        app.Code.SetDefault<global::app.module.identity.code.IIdentity>("test-identity");
        return app;
    }

    private static async Task<global::app.data.@this> Saved(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        Step? second = null;
        foreach (var (index, line) in new[] { (0, "variable.set(Name=%s0%, Value=\"first\")"), (1, "variable.set(Name=%s1%, Value=\"second\")"),
                     (2, "goal.return(Data={\"s1\": %s1%, \"note\": %note%, \"x\": %x%})") })
        {
            var step = new Step(app.type.list) { Goal = goal, Index = index, Text = line };
            step.Code.Add((await context.Action(line)).In(step));
            goal.Step.Add(step);
            if (index == 1) second = step;
        }
        app.goal.list.Add(goal);
        await context.Variable.Set("note", "original");
        await context.Variable.Set("x", 1);
        await using var running = context.call.Push(second!.Goal!, await context.call.Setting());
        await using var frame = context.call.Push(second!, await context.call.Setting());
        await using var call = context.call.Push(second!.Code[0], await context.call.Setting(), context.Variable);
        var saved = await new global::app.module.snapshot.save(context).Start();
        await call.DisposeAsync();
        await frame.DisposeAsync();
        await running.DisposeAsync();
        return saved;
    }

    private static async Task<byte[]> Plang(global::app.data.@this data, global::app.actor.context.@this context)
    {
        using var file = new MemoryStream();
        await (await context.App.type.list.Format("plang")!.Encode(file, data, context)).IsSuccess();
        return file.ToArray();
    }

    private static Task<global::app.data.@this> Read(byte[] file, global::app.actor.context.@this context)
        => context.App.type.list.Format("plang")!.Decode(file, context, "snap");

    private const string Edit = "variable.set(Name=%snap.app.actor.list.user.context.variable.row.note%, Value=\"EDITED\")";

    // A: the snapshot the app just took, edited by a navigation set, then resumed.
    [Test]
    public async Task A_TakenThenEdited_ThenResumed()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        var edit = await (await context.Action(Edit)).Start(context);
        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        var note = result.Success ? (await (await result.Get("note")).Value())?.ToString() : "REFUSED:" + result.Error!.Key;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"A edit.Success={edit.Success} edit.err={edit.Error?.Key} resume.Success={result.Success} s1={(result.Success ? (await (await result.Get("s1")).Value())?.ToString() : "-")} note={note} snapIsSigned={(await context.Variable.Get("snap")).Signature != null}");
        await Assert.That(note).IsNotEqualTo("EDITED");
    }

    // B: read from a file (verified on read), edited, then resumed.
    [Test]
    public async Task B_ReadFromFile_ThenEdited_ThenResumed()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var file = await Plang(await Saved(app), context);
        var read = await Read(file, context);
        await read.IsSuccess();
        await context.Variable.Set("snap", read);
        var edit = await (await context.Action(Edit)).Start(context);
        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        var note = result.Success ? (await (await result.Get("note")).Value())?.ToString() : "REFUSED:" + result.Error!.Key;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"B edit.Success={edit.Success} resume => {note}");
        await Assert.That(note).IsNotEqualTo("EDITED");
    }

    // C: edited in memory, then written out as a .plang and read again: the file must not read back as the app's.
    [Test]
    public async Task C_EditedThenWritten_ThenReadAgain()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        await (await context.Action(Edit)).Start(context);
        var edited = await context.Variable.Get("snap");

        var file = System.Text.Encoding.UTF8.GetString(await Plang(edited, context));
        var again = await Read(System.Text.Encoding.UTF8.GetBytes(file), context);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"C file has EDITED={file.Contains("EDITED")} original={file.Contains("original")} reread.Success={again.Success} key={again.Error?.Key}");
        await Assert.That(again.Success && file.Contains("EDITED")).IsFalse();
    }

    // E: the coder's shape — a numeric %x% = 1, edited to "2" by a navigation set, then resumed.
    [Test]
    public async Task E_NumericX_EditedToTwo_ThenResumed()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        var edit = await (await context.Action("variable.set(Name=%snap.app.actor.list.user.context.variable.row.x%, Value=\"2\")")).Start(context);
        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        var x = result.Success ? (await (await result.Get("x")).Value())?.ToString() : "REFUSED:" + result.Error!.Key;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"E edit.Success={edit.Success} edit.err={edit.Error?.Key} resume.Success={result.Success} x={x}");
        await Assert.That(x).IsNotEqualTo("2");
    }

    // G: informational, the road that does launder. The gate asks "signed by one of the app's identities", not "taken by save
    // snapshot": the app's own egress signs a Data that has none, so outside bytes the app writes out as .plang come back
    // vouched for. The resume then stops later in this harness (ChoiceInvalid: the test identity provider), past every
    // signature and identity check. Needs the app's own program to write the outside bytes; its deliberate act.
    // G (was): the other road — tampered snapshot TEXT (unsigned, from outside) is saved to a .plang by the app (Wire signs a Data
    // that has no signature), read back, and resumed. Does the egress signing make outside bytes the app's own?
    [Test]
    public async Task G_TamperedTextSavedAsPlangByTheApp_IsTheAppsOwn_ByDesign()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app);
        var shot = saved.Peek() as global::app.snapshot.@this ?? await saved.Value() as global::app.snapshot.@this;
        var text = shot == null ? "" : await shot.Text(context);
        var tampered = new global::app.data.@this("snap", new global::app.type.item.text.@this(text.Replace("original", "EDITED!!"), app.type.list[typeof(global::app.type.item.text.@this)]), context: context);
        var wrote = await Plang(tampered, context);
        var read = await Read(wrote, context);
        await context.Variable.Set("snap", read);
        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        var note = result.Success ? (await (await result.Get("note")).Value())?.ToString() : "REFUSED:" + result.Error!.Key;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"G text.has original={text.Contains("original")} read.Success={read.Success} signed={read.Signature != null} resume.Success={result.Success} note={note}");
        // accepted Low (snapshot 287): the app's own program signed outside bytes, so it vouches for them
        await Assert.That(note).IsEqualTo("EDITED!!");
    }

    // I: Ingi's ruling (snapshot 287): an edit is resumed only after the program signs it again, explicitly (`sign %snap%`).
    [Test]
    public async Task I_EditThenSign_ThenResume_GoesOn_AndEditThenResume_IsRefused()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        await (await context.Action(Edit)).Start(context);

        var refused = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);
        var signed = await (await context.Action("signing.sign(Data=%snap%)")).Start(context);
        await context.Variable.Set("snap", signed);
        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        var note = result.Success ? (await (await result.Get("note")).Value())?.ToString() : "REFUSED:" + result.Error!.Key;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"I before={refused.Error?.Key} sign.Success={signed.Success} sign.err={signed.Error?.Key} resume.Success={result.Success} note={note}");
        await Assert.That(refused.Error?.Key).IsEqualTo("SnapshotSignatureInvalid");
    }

    // J: probe I with an edit that writes DIFFERENT bytes in the Out view and the Snapshot view (the stop point), run to the end:
    // edit → resume refused → `sign` (default view) → resume goes on and answers. A text edit passes by coincidence (same bytes
    // in both views), so this one is the real test of sign's default view.
    [Test]
    public async Task J_EditTheStopPoint_ThenSign_ThenResume_GoesOnToTheEnd()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        var edit = await (await context.Action("variable.set(Name=%snap.app.actor.list.user.context.call.current.caller.index%, Value=\"0\")")).Start(context);

        var refused = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);
        var signed = await (await context.Action("signing.sign(Data=%snap%)")).Start(context);
        await context.Variable.Set("snap", signed);
        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"J edit={edit.Success}/{edit.Error?.Key} refused={refused.Error?.Key} sign={signed.Success} resume.Success={result.Success} key={result.Error?.Key}");
        await Assert.That(refused.Error?.Key).IsEqualTo("SnapshotSignatureInvalid");
        await Assert.That(result.Success).IsTrue();
    }

    // ---- snapshot 298: navigating and editing a held snapshot must change nothing live ----

    // What a running program is, in the parts an edit must never reach: the goals' steps, the variables, the call stack.
    private static async Task<string> Live(global::app.@this app, global::app.actor.context.@this context)
    {
        var goals = string.Join(";", app.goal.list.Items().OrderBy(g => g.Name).Select(g =>
            g.Name + ":" + string.Join(",", g.Step.Items().Select(step => step.Index + "=" + step.Text))));
        var vars = string.Join(";", new[] { "note", "x", "s0", "s1" }.Select(name => name + "=" + context.Variable.GetValue(name).GetAwaiter().GetResult()));
        var depth = context.call.Current?.Depth.ToString() ?? "none";
        await Task.CompletedTask;
        return $"goals[{goals}] vars[{vars}] depth[{depth}]";
    }

    // K0: only OPENING the snapshot for navigation (a read, no set) changes nothing live.
    [Test]
    public async Task K0_NavigatingASnapshot_ChangesNothingLive()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        var before = await Live(app, context);

        await (await context.Action("variable.set(Name=%seen%, Value=%snap.app.actor.list.user.context.call.current.caller.index%)")).Start(context);
        await (await context.Action("variable.set(Name=%seen2%, Value=%snap.app.actor.list.user.context.variable.row.note%)")).Start(context);

        await Assert.That(await Live(app, context)).IsEqualTo(before);
    }

    // K1: a navigation SET into the snapshot (the stop point, a variable) changes nothing live.
    [Test]
    [Arguments("variable.set(Name=%snap.app.actor.list.user.context.call.current.caller.index%, Value=\"2\")")]
    [Arguments("variable.set(Name=%snap.app.actor.list.user.context.variable.row.note%, Value=\"EDITED\")")]
    [Arguments("variable.set(Name=%snap.app.actor.list.user.context.call.current.index%, Value=\"2\")")]
    public async Task K1_EditingASnapshot_ChangesNothingLive(string edit)
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", await Saved(app));
        var before = await Live(app, context);
        var frame = context.call.Current;
        var propertiesBefore = (frame?.Errors.Count ?? 0) + (frame?.Tags.Count ?? 0);

        await (await context.Action(edit)).Start(context);

        await Assert.That(await Live(app, context)).IsEqualTo(before);
        await Assert.That((context.call.Current?.Errors.Count ?? 0) + (context.call.Current?.Tags.Count ?? 0)).IsEqualTo(propertiesBefore);
    }

    // every leaf of a snapshot's text as path=value, a record {type: dict, value} taken as the bare dict it holds, the
    // signature layer left out: what a snapshot says, not how it is wrapped
    private static bool IsRecord(System.Text.Json.Nodes.JsonObject json)
        => json.Count == 2 && json.ContainsKey("value") && json["type"] is System.Text.Json.Nodes.JsonObject type
            && type["name"] is System.Text.Json.Nodes.JsonValue name && name.ToString() == "dict";

    private static void Leaves(System.Text.Json.Nodes.JsonNode? node, string path, HashSet<string> into)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject json when IsRecord(json):
                Leaves(json["value"], path, into); break;
            case System.Text.Json.Nodes.JsonObject json:
                foreach (var (key, child) in json) if (key is not ("signature" or "@schema" or "hash" or "nonce" or "created")) Leaves(child, path + "/" + key, into);
                break;
            case System.Text.Json.Nodes.JsonArray list:
                for (var at = 0; at < list.Count; at++) Leaves(list[at], path + "[" + at + "]", into);
                break;
            default: into.Add(path + "=" + node?.ToJsonString()); break;
        }
    }

    // K2: the re-written edited snapshot holds what was saved plus the edit, and nothing else.
    [Test]
    public async Task K2_AReWrittenEditedSnapshot_HoldsOnlyWhatWasSavedPlusTheEdit()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app);
        var original = System.Text.Encoding.UTF8.GetString(await Plang(saved, context));
        await context.Variable.Set("snap", saved);

        await (await context.Action(Edit)).Start(context);
        var edited = System.Text.Encoding.UTF8.GetString(await Plang(await context.Variable.Get("snap"), context));

        var was = new HashSet<string>(); var now = new HashSet<string>();
        Leaves(System.Text.Json.Nodes.JsonNode.Parse(original), "", was);
        Leaves(System.Text.Json.Nodes.JsonNode.Parse(edited), "", now);
        var added = now.Except(was).ToList(); var gone = was.Except(now).ToList();
        await TestContext.Current!.OutputWriter.WriteLineAsync($"K2 added={string.Join(" | ", added)} gone={string.Join(" | ", gone)}");
        await Assert.That(edited).DoesNotContain("snap.app");
        await Assert.That(added.Count).IsEqualTo(1);
        await Assert.That(added[0]).Contains("EDITED");
        await Assert.That(gone.Count).IsEqualTo(1);
        await Assert.That(gone[0]).Contains("original");
    }
}
