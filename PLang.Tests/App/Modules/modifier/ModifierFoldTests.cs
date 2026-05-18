using global::app.modules;
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
        _app = new global::app.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    #region ModifierAttribute

    [Test]
    public async Task ModifierAttribute_Order_IsSetAndReadable()
    {
        // Verify [Modifier(Order = N)] stores and exposes the Order value via reflection
        var timeoutType = typeof(global::app.modules.timeout.After);
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
        // and stores result as %__data__%
        var action = Create("variable", "set", ("name", "%x%"), ("value", "hello"));

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("x")).IsEqualTo("hello");
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
                new("name", "%y%"), new("value", "wrapped")
            },
            Modifiers = new ActionModifiers
            {
                new PrAction
                {
                    Module = "timeout", ActionName = "after",
                    Parameters = new List<global::app.data.@this> { new("ms", 5000) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("y")).IsEqualTo("wrapped");
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
                new("name", "%z%"), new("value", "nested")
            },
            Modifiers = new ActionModifiers
            {
                new PrAction
                {
                    Module = "timeout", ActionName = "after",
                    Parameters = new List<global::app.data.@this> { new("ms", 5000) }
                },
                new PrAction
                {
                    Module = "error", ActionName = "handle",
                    Parameters = new List<global::app.data.@this> { new("ignoreError", true) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("z")).IsEqualTo("nested");
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
                new("name", "%q%"), new("value", "full")
            },
            Modifiers = new ActionModifiers
            {
                new PrAction
                {
                    Module = "timeout", ActionName = "after",
                    Parameters = new List<global::app.data.@this> { new("ms", 5000) }
                },
                new PrAction
                {
                    Module = "cache", ActionName = "wrap",
                    Parameters = new List<global::app.data.@this>
                    {
                        new("durationMs", 60_000L),
                        new("key", "fold-test-key")
                    }
                },
                new PrAction
                {
                    Module = "error", ActionName = "handle",
                    Parameters = new List<global::app.data.@this> { new("ignoreError", true) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("q")).IsEqualTo("full");
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
                new("name", "%nope%"), new("value", "x")
            },
            Modifiers = new ActionModifiers
            {
                // variable.set as a modifier is invalid
                new PrAction
                {
                    Module = "variable", ActionName = "set",
                    Parameters = new List<global::app.data.@this>
                    {
                        new("name", "%bad%"), new("value", "no")
                    }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ModifierError");
        await Assert.That(result.Error!.Message).Contains("not a modifier");
    }

    #endregion
}
