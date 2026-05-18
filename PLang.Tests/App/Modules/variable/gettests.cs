using global::app.actor.context;
using app;
using global::app.variables;
using global::app.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class GetTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variables);
    }

    [Test]
    public async Task Get_ReturnsRawValue()
    {
        var (context, _) = CreateContext();
        context.Variables.Set("testVar", "testValue");

        var action = new Get { Context = context, Name = new Variable("testVar") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
        await Assert.That(result.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNull()
    {
        var (context, _) = CreateContext();

        var action = new Get { Context = context, Name = new Variable("nonexistent") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }
}
