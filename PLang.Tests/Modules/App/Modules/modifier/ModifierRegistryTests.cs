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
        await Assert.That(((Modifier)app.Module["timeout"]!["after"]!).Order).IsEqualTo(1);
        await Assert.That(((Modifier)app.Module["cache"]!["wrap"]!).Order).IsEqualTo(2);
        await Assert.That(((Modifier)app.Module["error"]!["handle"]!).Order).IsEqualTo(3);
    }

    #endregion

    #region Step.Clone

    [Test]
    public async Task StepClone_ClonesActionModifiers()
    {
        // Step with action that has modifiers -> Clone() copies modifiers too
        var step = new Step
        {
            Index = 0,
            Text = "read file with cache",
            Actions = new StepActions
            {
                new PrAction
                {
                    Module = "file",
                    ActionName = "read",
                    Modifiers = new ActionModifiers
                    {
                        new global::app.goal.steps.step.actions.action.modifier.@this { Module = "cache", ActionName = "wrap" }
                    }
                }
            }
        };

        var clone = step.Clone();

        await Assert.That(clone.Actions[0].Modifiers.Count).IsEqualTo(1);
        await Assert.That(clone.Actions[0].Modifiers[0].Module).IsEqualTo("cache");
        // Verify it's a copy, not the same reference
        await Assert.That(clone.Actions[0].Modifiers).IsNotSameReferenceAs(step.Actions[0].Modifiers);
    }

    #endregion

    #region Describe

    [Test]
    public async Task Describe_ModifierActions_AppearInSummary()
    {
        // Modifier modules appear in the action summary so the LLM can discover them
        // — they go through the same [Action] registration as any other handler.
        var modules = new global::app.module.list.@this();
        var described = await modules.Describe();

        var names = described.Select(a => $"{a.Module}.{a.ActionName}").ToHashSet();
        await Assert.That(names).Contains("timeout.after");
        await Assert.That(names).Contains("cache.wrap");
        await Assert.That(names).Contains("error.handle");
    }

    #endregion
}
