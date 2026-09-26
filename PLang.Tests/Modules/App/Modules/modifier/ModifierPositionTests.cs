namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// A modifier's Position says what it BOUNDS, and lowest wraps outermost:
/// <c>on.error</c> bounds the attempts, <c>cache.wrap</c> bounds the outcome of the real work,
/// <c>timeout.after</c> bounds one attempt. Get the order backwards and the language quietly
/// changes meaning — a deadline outside the handler swallows the recovery, and an `on error` can
/// never catch a Timeout. These pin both halves: the catalog numbers, and the nesting they produce.
/// </summary>
public class ModifierPositionTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // The catalog numbers themselves are pinned by
    // ModifierRegistryTests.Order_LivesOnTheModifierType — this file pins the nesting they produce.

    /// <summary>A step whose modifiers arrive in the WRONG order still nests correctly — Nest
    /// sorts by Position, so authoring order cannot change what wraps what.</summary>
    [Test]
    public async Task Nest_SortsModifiers_OutermostFirst_RegardlessOfAuthoredOrder()
    {
        var goal = new Goal
        {
            Name = "G",
            Path = global::app.type.item.path.@this.Resolve("/G.goal", global::PLang.Tests.TestApp.SharedContext)
        };
        var step = new Step { Goal = goal, Index = 0, Text = "step" };

        // Authored innermost-first — the reverse of the nesting we expect out.
        step.Code.Add(TestAction.Create("variable", "set", ("name", "%x%"), ("value", "v")));
        step.Code.Add(TestAction.Create("timeout", "after", ("ms", 1L)));
        step.Code.Add(TestAction.Create("cache", "wrap", ("key", "k")));
        step.Code.Add(TestAction.Create("on", "error"));

        step.Nest(_app.Module);

        var modifiers = step.Code[0].Modifier;
        await Assert.That(modifiers.Count).IsEqualTo(3);
        await Assert.That($"{modifiers[0].Module}.{modifiers[0].Name}").IsEqualTo("on.error");
        await Assert.That($"{modifiers[1].Module}.{modifiers[1].Name}").IsEqualTo("cache.wrap");
        await Assert.That($"{modifiers[2].Module}.{modifiers[2].Name}").IsEqualTo("timeout.after");
    }
}
