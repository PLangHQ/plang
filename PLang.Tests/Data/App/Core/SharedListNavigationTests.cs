namespace PLang.Tests.App.Core;

/// <summary>
/// A program list (a goal's steps) is shared by every run and stores no context. Navigating into it
/// hands each asker a Data for the item, born with the asker's context — the shared list and its
/// stored items are never written.
/// </summary>
public class SharedListNavigationTests
{
    [Test]
    public async Task TwoActors_NavigateSameProgramList_StoredItemsUnchanged()
    {
        await using var app = TestApp.Create("/tmp/sharedlist-" + System.Guid.NewGuid().ToString("N")[..8]);
        var goal = Make.Goal("Start", Make.Step("first step"), Make.Step("second step"));
        var before = goal.Step.Slots().ToList();
        var user = new Data("goal", goal, context: app.User.Context);
        var system = new Data("goal", goal, context: app.System.Context);

        var reads = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            (i % 2 == 0 ? user : system).Get("Step[0].Text").AsTask()));

        foreach (var read in reads)
            await Assert.That((await read.Value())?.ToString()).IsEqualTo("first step");
        await Assert.That((await user.Get("Step[0]")).Context).IsSameReferenceAs(app.User.Context);
        await Assert.That((await system.Get("Step[0]")).Context).IsSameReferenceAs(app.System.Context);

        // The stored slots are the same step items as before — nothing was wrapped or stamped.
        var after = goal.Step.Slots().ToList();
        await Assert.That(after.Count).IsEqualTo(before.Count);
        for (int i = 0; i < before.Count; i++)
            await Assert.That(after[i]).IsSameReferenceAs(before[i]);
        await Assert.That(after.Any(s => s is Data)).IsFalse();
    }
}
