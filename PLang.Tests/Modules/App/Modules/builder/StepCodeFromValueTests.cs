namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// A step's code is actions: a value handed to it that is not one (a dict, a list of dicts) is refused with why, and the
/// step keeps the code it had — never an empty code with the reason swallowed.
/// </summary>
public class StepCodeFromValueTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this(
        "/tmp/step-code-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task ADictHandedToAStepsCode_IsRefusedWithWhy()
    {
        var context = _app.actor.list.User.Context;
        var goal = new Goal(context.App.type.list)
        {
            Name = "G",
            Path = global::app.type.item.path.@this.Resolve("/G.goal", context),
        };
        goal.Step.Add(new Step(context.App.type.list) { Goal = goal, Index = 0, Text = "do stuff" });
        await context.Variable.Set("goal", goal);

        var row = global::app.type.item.@this.Create(
            new Dictionary<string, object?> { ["module"] = "output", ["name"] = "write" }, context);
        var set = await new global::app.type.item.variable.@this("goal.Step[0].Code").Set(
            new global::app.data.@this("code", new List<object?> { row }, context: context), context);

        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("NotAnAction");
        await Assert.That(set.Error.Message).Contains("formal");
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(0);
    }
}
