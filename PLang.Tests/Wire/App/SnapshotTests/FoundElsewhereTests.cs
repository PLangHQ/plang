namespace PLang.Tests.App.SnapshotTests;

// In a snapshot, a value found elsewhere (the program, the disk) is saved as what finds it, and comes back as what
// that finds: the live goal, the file on disk read again.
public class FoundElsewhereTests
{
    private static string Folder() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-found-" + System.Guid.NewGuid().ToString("N")[..8]);

    // A goal G at /G.goal with one step, held by the app's goal list.
    private static async Task<Goal> Holds(global::app.@this app, string text = "set %x% = 1")
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = 0, Text = text };
        step.Code.Add((await context.Action("variable.set(Name=%x%, Value=1)")).In(step));
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        return goal;
    }

    [Test]
    public async Task AGoalInAVariable_IsSavedAsWhatFindsIt_AndComesBackAsTheLiveGoal()
    {
        var folder = Folder();
        await using var src = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        await context.Variable.Set("g", new global::app.data.@this("g", await Holds(src), context: context));

        var text = await src.Snapshot(context).Saved().Text(context);
        await Assert.That(text).Contains("\"pr\"");
        await Assert.That(text).Contains("\"key\"");
        await Assert.That(text).DoesNotContain("set %x% = 1");

        await using var dst = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        var live = await Holds(dst);
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var back = await (await into.Variable.Get("g")).Value<Goal>();
        await Assert.That(ReferenceEquals(back, live)).IsTrue();
    }

    [Test]
    public async Task AGoalChangedSinceTheSave_IsRefused()
    {
        var folder = Folder();
        await using var src = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        await context.Variable.Set("g", new global::app.data.@this("g", await Holds(src), context: context));
        var text = await src.Snapshot(context).Saved().Text(context);

        await using var dst = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        await Holds(dst, "set %x% = 2");
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var g = await into.Variable.Get("g");
        await Assert.That(await g.Value<Goal>()).IsNull();
        await Assert.That(g.Error!.Key).IsEqualTo("GoalHashMismatch");
    }

    [Test]
    public async Task AFileReadBeforeTheSave_ComesBackUnread_AndReadsTheDiskAgain()
    {
        var folder = Folder();
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "note.txt"), "before");
        await using var src = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        var file = new global::app.type.item.file.@this(global::app.type.item.path.@this.Resolve("/note.txt", context), context);
        await file.Content(context);
        await context.Variable.Set("note", new global::app.data.@this("note", file, context: context));

        var text = await src.Snapshot(context).Saved().Text(context);
        await Assert.That(text).DoesNotContain("before");
        // a file is its own key: its record's type is file, no kind
        var note = text[text.IndexOf("\"note\"", System.StringComparison.Ordinal)..];
        var type = note[note.IndexOf("\"type\"", System.StringComparison.Ordinal)..note.IndexOf("\"value\"", System.StringComparison.Ordinal)];
        await Assert.That(type).Contains("\"file\"");
        await Assert.That(type).DoesNotContain("\"kind\"");

        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "note.txt"), "after");
        await using var dst = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        await Assert.That((await (await into.Variable.Get("note")).Value())?.ToString()).IsEqualTo("after");
    }
}
