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
        app.Code.Register(new global::PLang.Tests.Shared.TestIdentity { IsBuiltIn = true });
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
        await using var frame = context.call.Push(second!, await context.call.Setting());
        await using var call = context.call.Push(second!.Code[0], await context.call.Setting(), context.Variable);
        var saved = await new global::app.module.snapshot.save(context).Start();
        await call.DisposeAsync();
        await frame.DisposeAsync();
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
    public async Task G_TamperedTextSavedAsPlang_ThenReadAndResumed()
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
        await Assert.That(note).IsNotEqualTo("EDITED!!");
    }
}
