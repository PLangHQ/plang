namespace PLang.Tests.App.Core;

/// A goal list finds the goal it holds for a .pr in one look: an add costs the same however many goals it holds, and a
/// goal works out its .pr once, from its path.
public class GoalListIndexTests
{
    [Test]
    public async Task AnAdd_CostsTheSame_HoweverManyGoalsTheListHolds()
    {
        await using var app = new global::app.@this("/goal-index", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.System.Context;
        var goals = Enumerable.Range(0, 400).Select(i => Make.Goal(context, "G" + i, $"/g{i}/Start.goal")).ToArray();
        foreach (var goal in goals) _ = goal.Pr?.Path; // each goal's .pr worked out before the count

        long Bytes(int from, int to)
        {
            var start = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = from; i < to; i++) app.goal.list.Add(goals[i]);
            return System.GC.GetAllocatedBytesForCurrentThread() - start;
        }
        Bytes(0, 300);
        // a hundred adds to a list holding 300 goals: an add that worked out every held goal's .pr cost about 4.8 MB
        var late = Bytes(300, 400);

        await Assert.That(late).IsLessThan(1_000_000L).Because("an add finds the goal it replaces in one look");
        await Assert.That(app.goal.list[goals[250].Pr!.Path]).IsSameReferenceAs(goals[250]);
    }

    [Test]
    public async Task AGoalReadAgain_TakesThePlaceOfTheOneHeld()
    {
        await using var app = new global::app.@this("/goal-index", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.System.Context;
        var first = Make.Goal(context, "Start", "/g/Start.goal");
        var again = Make.Goal(context, "Start", "/g/Start.goal");

        app.goal.list.Add(first);
        app.goal.list.Add(again);

        await Assert.That(app.goal.list.CountRaw).IsEqualTo(1);
        await Assert.That(app.goal.list[again.Pr!.Path]).IsSameReferenceAs(again);
    }

    // The index is the list's own, made from its rows: a row taken out through a door the goal list doesn't write
    // (the base list's own) is gone from the index too.
    [Test]
    public async Task AGoalTakenOutByAnyDoor_IsNoLongerHeldForItsPr()
    {
        await using var app = new global::app.@this("/goal-index", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.System.Context;
        var goal = Make.Goal(context, "Start", "/g/Start.goal");
        app.goal.list.Add(goal);
        await Assert.That(app.goal.list[goal.Pr!.Path]).IsSameReferenceAs(goal);

        app.goal.list.RemoveAt(0);

        await Assert.That(app.goal.list[goal.Pr!.Path]).IsNull();
    }
}
