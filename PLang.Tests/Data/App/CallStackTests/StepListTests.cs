namespace PLang.Tests.App.CallStackTests;

// A step taken into a step list knows the list it goes on in, wherever it was added from; a body read off the list is
// a view, which takes no step from it.
public class StepListTests
{
    private static global::app.@this App() => new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    private static Step Line(global::app.@this app, Goal goal, int index, int indent)
        => new(app.type.list) { Index = index, Text = $"step {index}", Goal = goal, Line = new global::app.goal.step.line.@this { Number = index, Indent = indent } };

    [Test]
    public async Task AStepAdded_KnowsTheListItGoesOnIn()
    {
        await using var app = App();
        var goal = new Goal(app.type.list) { Name = "G" };
        var step = Line(app, goal, 0, 0);

        goal.Step.Add(step);

        await Assert.That(step.List).IsSameReferenceAs(goal.Step);
    }

    [Test]
    public async Task ABody_TakesNoStepFromItsList()
    {
        await using var app = App();
        var goal = new Goal(app.type.list) { Name = "G" };
        var gate = Line(app, goal, 0, 0);
        var under = Line(app, goal, 1, 1);
        goal.Step.Add(gate);
        goal.Step.Add(under);

        var body = goal.Step.Body(0);

        await Assert.That(body.CountRaw).IsEqualTo(1);
        await Assert.That(under.List).IsSameReferenceAs(goal.Step);
    }
}
