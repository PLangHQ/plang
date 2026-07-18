using Modifier = global::app.goal.steps.step.actions.action.modifier.@this;

namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests that the catalog element answers the modifier ROLE structurally — a [Modifier] handler
/// is a `modifier` type in the module's Modifiers home; Order lives on the type. And Step.Clone().
/// </summary>
public class ModifierRegistryTests
{

    #region role by type

    [Test]
    public async Task ModifierAttributedHandler_IsAModifierElement()
    {
        await using var app = TestApp.Create("/app");
        // the module element routes [Modifier] handlers to the Modifiers home; the type IS the role.
        await Assert.That(app.Module["timeout"]!["after"] is Modifier).IsTrue();
        await Assert.That(app.Module["cache"]!["wrap"] is Modifier).IsTrue();
        await Assert.That(app.Module["error"]!["handle"] is Modifier).IsTrue();
    }

    [Test]
    public async Task RegularHandler_IsNotAModifierElement()
    {
        await using var app = TestApp.Create("/app");
        await Assert.That(app.Module["variable"]!["set"] is Modifier).IsFalse();
        await Assert.That(app.Module["file"]!["read"] is Modifier).IsFalse();
    }

    #endregion

    #region Order on the type

    [Test]
    public async Task Order_LivesOnTheModifierType()
    {
        await using var app = TestApp.Create("/app");
        await Assert.That(((Modifier)app.Module["timeout"]!["after"]!).Position).IsEqualTo(1);
        await Assert.That(((Modifier)app.Module["cache"]!["wrap"]!).Position).IsEqualTo(2);
        await Assert.That(((Modifier)app.Module["error"]!["handle"]!).Position).IsEqualTo(3);
    }

    #endregion

    #region Describe

    [Test]
    public async Task Describe_ModifierActions_AppearInSummary()
    {
        // Modifier modules appear in the action summary so the LLM can discover them
        // — they go through the same [Action] registration as any other handler.
        await using var app = TestApp.Create("/app");
        var described = await app.Module.Describe();

        var names = described.Select(a => $"{a.Module}.{a.ActionName}").ToHashSet();
        await Assert.That(names).Contains("timeout.after");
        await Assert.That(names).Contains("cache.wrap");
        await Assert.That(names).Contains("error.handle");
    }

    #endregion
}
