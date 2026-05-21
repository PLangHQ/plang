using app.actor.context;
using app;
using app.variables;
using app.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class RemoveTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variables);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var (context, memory) = CreateContext();
        memory.Set("testVar", "testValue");

        var action = new Remove { Context = context, Name = new Variable("testVar") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_Succeeds()
    {
        var (context, _) = CreateContext();

        var action = new Remove { Context = context, Name = new Variable("nonexistent") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }
}
