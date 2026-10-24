namespace PLang.Tests.App.Core;

/// <summary>Goals through the parent: a child finds the goal its parent read — the same goal, no second read of its
/// .pr — and a goal changed on disk is read again on its next use, kept fresh by its .pr's stat, with no time window.</summary>
public class SharedGoalTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sharedgoal-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, ".build"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, "Start.goal"), "Start\n");
        Built("""{"name":"Start","path":"/Start.goal","step":[]}""");
    }

    [After(Test)]
    public void Cleanup()
    {
        if (System.IO.Directory.Exists(_dir)) System.IO.Directory.Delete(_dir, true);
    }

    // the goal's .pr as the builder would write it
    private void Built(string pr) => System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, ".build", "start.pr"), pr);

    private async Task<Goal> Loaded(global::app.@this app)
    {
        var loaded = await app.goal.Load(System.IO.Path.Combine(_dir, "Start.goal"));
        await loaded.IsSuccess();
        return (Goal)(await loaded.Value())!;
    }

    [Test]
    public async Task AChild_FindsItsParentsGoal()
    {
        await using var parent = new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Testing();
        var read = await Loaded(parent);
        await using var child = new global::app.@this(_dir, parent.actor.list.User).Testing();

        await Assert.That(await Loaded(child)).IsSameReferenceAs(read);
    }

    [Test]
    public async Task AGoalChangedOnDisk_IsReadAgain()
    {
        await using var parent = new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Testing();
        var before = await Loaded(parent);
        Built("""{"name":"Start","path":"/Start.goal","step":[],"child":[{"name":"Added","path":"/Start.goal","step":[]}]}""");
        await using var child = new global::app.@this(_dir, parent.actor.list.User).Testing();

        var after = await Loaded(child);

        await Assert.That(after).IsNotSameReferenceAs(before);
        await Assert.That(after.Child.Items().Select(g => g.Name).ToList()).Contains("Added");
        await Assert.That(await Loaded(parent)).IsSameReferenceAs(after).Because("the goal read again is the shared one");
    }
}
