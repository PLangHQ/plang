using app.actor.context;
using app;
using app.variable;
using app.module.variable;

namespace PLang.Tests.App.actions.variable;

public class ExistsTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Exists_ExistingVariable_ReturnsTrue()
    {
        var (context, _) = CreateContext();
        context.Variable.Set("testVar", "testValue");

        var action = new Exists { Context = context, Name = new app.variable.@this("testVar") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentVariable_ReturnsFalse()
    {
        var (context, _) = CreateContext();

        var action = new Exists { Context = context, Name = new app.variable.@this("nonexistent") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }
}
