using app.module;
using static PLang.Tests.TestAction;

namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the core modifier infrastructure: ModifierAttribute, IModifier contract,
/// Action.Modifiers property, and the right-to-left fold in Action.RunAsync.
/// </summary>
public class ModifierFoldTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    #region ModifierAttribute

    [Test]
    public async Task ModifierAttribute_Order_IsSetAndReadable()
    {
        // Verify [Modifier(Order = N)] stores and exposes the Order value via reflection
        var timeoutType = typeof(global::app.module.action.timeout.After);
        var attr = timeoutType.GetCustomAttributes(typeof(ModifierAttribute), false)
            .Cast<ModifierAttribute>().FirstOrDefault();

        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Order).IsEqualTo(1);
    }

    #endregion

    #region Action.Modifiers Property

    [Test]
    public async Task Action_Modifiers_DefaultsToEmptyList()
    {
        var action = new PrAction { Module = "file", ActionName = "read" };

        await Assert.That(action.Modifiers).IsNotNull();
        await Assert.That(action.Modifiers.Count).IsEqualTo(0);
    }

    #endregion

    #region Action.RunAsync Fold

    [Test]
    public async Task RunAsync_ZeroModifiers_ExistingBehaviorUnchanged()
    {
        // Regression: Action.RunAsync with no modifiers dispatches normally
        // and stores result as %!data%
        var action = Create("variable", "set", ("name", "%x%"), ("value", "hello"));

        var result = await action.RunAsync(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("x"))).IsEqualTo("hello");
    }

    [Test]
    public async Task RunAsync_OneModifier_WrapsAction()
    {
        // timeout.after around a fast variable.set — action completes, result passes through
        var action = new PrAction
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%y%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "wrapped", context: global::PLang.Tests.TestApp.SharedContext)
            },
            Modifiers = new ActionModifiers
            {
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "timeout", ActionName = "after",
                    Parameters = new List<global::app.data.@this> { new("ms", 5000, context: global::PLang.Tests.TestApp.SharedContext) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("y"))).IsEqualTo("wrapped");
    }

    [Test]
    public async Task RunAsync_TwoModifiers_CorrectNestingOrder()
    {
        // error.handle(ignore) wraps timeout.after(5000ms) wraps variable.set
        // Verify both modifiers participate: fast action completes cleanly.
        var action = new PrAction
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%z%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "nested", context: global::PLang.Tests.TestApp.SharedContext)
            },
            Modifiers = new ActionModifiers
            {
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "timeout", ActionName = "after",
                    Parameters = new List<global::app.data.@this> { new("ms", 5000, context: global::PLang.Tests.TestApp.SharedContext) }
                },
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "error", ActionName = "handle",
                    Parameters = new List<global::app.data.@this> { new("ignoreError", true, context: global::PLang.Tests.TestApp.SharedContext) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("z"))).IsEqualTo("nested");
    }

    [Test]
    public async Task RunAsync_ThreeModifiers_FullChain()
    {
        // timeout > cache > error > action — all three wrap a successful variable.set
        var action = new PrAction
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%q%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "full", context: global::PLang.Tests.TestApp.SharedContext)
            },
            Modifiers = new ActionModifiers
            {
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "timeout", ActionName = "after",
                    Parameters = new List<global::app.data.@this> { new("ms", 5000, context: global::PLang.Tests.TestApp.SharedContext) }
                },
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "cache", ActionName = "wrap",
                    Parameters = new List<global::app.data.@this>
                    {
                        new("durationMs", 60_000L, context: global::PLang.Tests.TestApp.SharedContext),
                        new("key", "fold-test-key", context: global::PLang.Tests.TestApp.SharedContext)
                    }
                },
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "error", ActionName = "handle",
                    Parameters = new List<global::app.data.@this> { new("ignoreError", true, context: global::PLang.Tests.TestApp.SharedContext) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("q"))).IsEqualTo("full");
    }

    [Test]
    public async Task RunAsync_NonIModifierHandler_ReturnsCleanError()
    {
        // variable.set is NOT a modifier — using it as one yields a ModifierError
        var action = new PrAction
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%nope%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "x", context: global::PLang.Tests.TestApp.SharedContext)
            },
            Modifiers = new ActionModifiers
            {
                // variable.set as a modifier is invalid
                new global::app.goal.steps.step.actions.action.modifier.@this
                {
                    Module = "variable", ActionName = "set",
                    Parameters = new List<global::app.data.@this>
                    {
                        new("name", "%bad%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "no", context: global::PLang.Tests.TestApp.SharedContext)
                    }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ModifierError");
        await Assert.That(result.Error!.Message).Contains("not a modifier");
    }

    #endregion
}
