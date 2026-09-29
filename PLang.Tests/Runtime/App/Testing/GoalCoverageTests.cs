namespace PLang.Tests.App.Tester;

// The report's "Goals reached": every goal of the app under test marked when a test reaches it, the public and
// private totals, and the goals no test reached. A goal is reached when a test's reach takes it in.
public class GoalCoverageTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    // /Main.goal: the public Main, and the private Used and Lonely.
    private (global::app.goal.@this main, global::app.goal.@this used, global::app.goal.@this lonely) File()
    {
        var main = Make.Goal(Ctx, "Main", "/Main.goal");
        var used = Make.Goal(Ctx, "Used", "/Main.goal");
        var lonely = Make.Goal(Ctx, "Lonely", "/Main.goal");
        main.Child.Add(used);
        main.Child.Add(lonely);
        return (main, used, lonely);
    }

    [Test] public async Task TheGoalsATestReaches_AreMarked_AndTheRestListed()
    {
        var (main, used, lonely) = File();
        var coverage = new global::app.test.Coverage();
        coverage.Add(main);
        coverage.Add(used);

        var text = coverage.Text(app.module.list, [main, used, lonely]).Replace("\r\n", "\n");

        await Assert.That(text).Contains("Goals reached:");
        await Assert.That(text).Contains("[x] /Main\n");
        await Assert.That(text).Contains("[x] /Main#Used");
        await Assert.That(text).Contains("[ ] /Main#Lonely");
        await Assert.That(text).Contains("public: 1/1, private: 1/2");
        await Assert.That(text).Contains("Goals no test reached:\n    /Main#Lonely");
    }

    [Test] public async Task WhatATestsAppReached_MergesIntoTheRun()
    {
        var (main, used, lonely) = File();
        var child = new global::app.test.Coverage();
        child.Add(lonely);
        var run = new global::app.test.Coverage();

        run.Merge(child);

        await Assert.That(run.Text(app.module.list, [main, used, lonely])).Contains("[x] /Main#Lonely");
    }
}
