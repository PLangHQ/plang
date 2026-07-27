using PLang.Tests.Shared;

namespace PLang.Tests.App.VariablesTests;

// Disambiguation: does %goal.step.Count% navigate goal -> Step (step.list node) -> Count
// after the node-list conversion? If this passes, the builder's Validate.goal failure is a
// scope issue (%goal% not reaching the deep call), not node navigation.
public class GoalStepCountNavTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/test");
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task GoalStepCount_navigates_to_step_node_count()
    {
        var goal = Make.Goal("G",
            Make.Step("write out %x%"),
            Make.Step("write out %y%"));

        var stack = new Variables(_app.User.Context);
        stack.Set("goal", goal);

        var count = await stack.Get("goal.step.Count");

        await Assert.That(count).IsNotNull();
        await Assert.That(count!.IsInitialized).IsTrue();
        await Assert.That(count.GetValue<long>()).IsEqualTo(2L);
    }
}
