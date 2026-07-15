namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for modifier awareness in the module registry (IsModifier, GetModifierOrder)
/// and Step.Clone() support for action modifiers.
/// </summary>
public class ModifierRegistryTests
{
    #region IsModifier

    [Test]
    public async Task IsModifier_ModifierAttributedHandler_ReturnsTrue()
    {
        var modules = new global::app.module.list.@this();

        await Assert.That(modules.IsModifier("timeout", "after")).IsTrue();
        await Assert.That(modules.IsModifier("cache", "wrap")).IsTrue();
        await Assert.That(modules.IsModifier("error", "handle")).IsTrue();
    }

    [Test]
    public async Task IsModifier_RegularHandler_ReturnsFalse()
    {
        var modules = new global::app.module.list.@this();

        await Assert.That(modules.IsModifier("variable", "set")).IsFalse();
        await Assert.That(modules.IsModifier("file", "read")).IsFalse();
    }

    [Test]
    public async Task IsModifier_UnknownAction_ReturnsFalse()
    {
        var modules = new global::app.module.list.@this();

        await Assert.That(modules.IsModifier("nonexistent", "nope")).IsFalse();
    }

    #endregion

    #region GetModifierOrder

    [Test]
    public async Task GetModifierOrder_ReturnsCorrectOrder()
    {
        var modules = new global::app.module.list.@this();

        await Assert.That(modules.GetModifierOrder("timeout", "after")).IsEqualTo(1);
        await Assert.That(modules.GetModifierOrder("cache", "wrap")).IsEqualTo(2);
        await Assert.That(modules.GetModifierOrder("error", "handle")).IsEqualTo(3);
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
                        new PrAction { Module = "cache", ActionName = "wrap" }
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
