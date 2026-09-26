using Modifier = global::app.goal.step.action.modifier.@this;

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
        await Assert.That(app.Module["on"]!["error"] is Modifier).IsTrue();
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

    /// <summary>The layer says what a modifier BOUNDS, and lowest wraps outermost: on.error
    /// bounds the attempts, cache.wrap bounds the outcome of the real work, timeout.after bounds
    /// one attempt.</summary>
    [Test]
    public async Task Order_LivesOnTheModifierType()
    {
        await using var app = TestApp.Create("/app");
        await Assert.That(((Modifier)app.Module["on"]!["error"]!).Layer).IsEqualTo(0);
        await Assert.That(((Modifier)app.Module["cache"]!["wrap"]!).Layer).IsEqualTo(50);
        await Assert.That(((Modifier)app.Module["timeout"]!["after"]!).Layer).IsEqualTo(100);
    }

    #endregion
}
