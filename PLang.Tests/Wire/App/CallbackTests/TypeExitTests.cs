using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app;
using app.module.action.output;

namespace PLang.Tests.App.CallbackTests;

/// The one exit owner: a result Data answers whether it exits the goal (`Exits`, and the step
/// loop's `ShouldExit()`), asking through its own context — an `ask` result stops the goal,
/// including a typed absence (an `ask` slot with no answer yet) whose type names no class.
public class TypeExitTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/tmp/typeexit-" + System.Guid.NewGuid().ToString("N")[..6]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // An `ask` typed absence — the type names `ask`, the value carries no class.
    private global::app.data.@this AskAbsent()
        => new("", new global::app.type.item.@null.@this(_app.Type[typeof(Ask)].Name), context: Ctx);

    [Test] public async Task Exits_TrueFor_TypedAbsentAsk()
        => await Assert.That(AskAbsent().Exits).IsTrue();

    [Test] public async Task ShouldExit_TrueFor_TypedAbsentAsk()
        => await Assert.That(AskAbsent().ShouldExit()).IsTrue();

    [Test] public async Task Exits_FalseFor_Text()
        => await Assert.That(new global::app.data.@this("", "hello", context: Ctx).Exits).IsFalse();

    [Test] public async Task ShouldExit_FalseFor_SuccessfulText()
        => await Assert.That(new global::app.data.@this("", "hello", context: Ctx).ShouldExit()).IsFalse();

    [Test] public async Task Ask_ImplementsIExitsGoal()
        => await Assert.That(typeof(IExitsGoal).IsAssignableFrom(typeof(Ask))).IsTrue();
}
