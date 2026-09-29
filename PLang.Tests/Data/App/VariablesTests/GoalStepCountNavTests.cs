using PLang.Tests.Shared;

namespace PLang.Tests.App.VariablesTests;

// Disambiguation: does %goal.step.Count% navigate goal -> Step (step.list node) -> Count
// after the node-list conversion? If this passes, the builder's Validate.goal failure is a
// scope issue (%goal% not reaching the deep call), not node navigation.
public class GoalStepCountNavTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task GoalStepCount_navigates_to_step_node_count()
    {
        var goal = Make.Goal("G",
            Make.Step("write out %x%"),
            Make.Step("write out %y%"));

        var stack = _app.actor.list.User.Context.Variable;
        stack.Set("goal", goal);

        var count = await new global::app.type.item.variable.@this("goal.step.Count").Start(stack.Context);

        await Assert.That(count).IsNotNull();
        await Assert.That(count!.IsInitialized).IsTrue();
        await Assert.That(count.GetValue<long>()).IsEqualTo(2L);
    }
}
