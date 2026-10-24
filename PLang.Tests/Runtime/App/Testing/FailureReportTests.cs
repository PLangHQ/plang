using app.error;

namespace PLang.Tests.App.Tester;

// A failed assertion keeps what it compared as it read it in the test's own context; the runner's report, written
// in the runner's context, shows those — a template is never filled again there, where the test's variables (and
// its %!app.goal%) are not the runner's.
public class FailureReportTests
{
    [Test] public async Task AFailedAssertion_IsReportedAsTheTestReadIt_NotFilledAgainInTheRunner()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        context.Variable.Set("who", "Checkout");
        var goal = Make.Goal(context, "Checkout", "/Checkout.test.goal",
            Make.Step("assert %who% equals \"Start\"",
                Make.Action(context, "assert", "equals", ("Expected", "Start"), ("Actual", "%who%"))));
        var result = await goal.Start(context);
        await result.IsFailure();

        // the runner, in its own app, inside its own goal, holding a %who% of its own
        await using var runner = new global::app.@this("/runner", global::PLang.Tests.Parent.Actor).Testing();
        var reporting = runner.actor.list.User.Context;
        reporting.Variable.Set("who", "Test");
        var run = new global::app.test.@this(runner.type.list) { Goal = goal };
        run.Complete(global::app.test.Status.Fail, (AssertionError)result.Error!);

        var report = await run.Failure(reporting);
        await Assert.That(report).Contains("Actual:   \"Checkout\"");
        await Assert.That(report).DoesNotContain("\"Test\"");
    }
}
