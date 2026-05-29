using app.actor.context;
using app;
using app.variable;
using app.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class ExistsTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variables);
    }

    [Test]
    public async Task Exists_ExistingVariable_ReturnsTrue()
    {
        var (context, _) = CreateContext();
        context.Variables.Set("testVar", "testValue");

        var action = new Exists { Context = context, Name = new Variable("testVar") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentVariable_ReturnsFalse()
    {
        var (context, _) = CreateContext();

        var action = new Exists { Context = context, Name = new Variable("nonexistent") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }
}
