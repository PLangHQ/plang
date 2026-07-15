using app.actor.context;
using app;
using app.variable;
using app.module.action.variable;

namespace PLang.Tests.App.actions.variable;

public class RemoveTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var (context, memory) = CreateContext();
        memory.Set("testVar", "testValue");

        var action = new Remove(context) { Name = new app.variable.@this("testVar") };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_Succeeds()
    {
        var (context, _) = CreateContext();

        var action = new Remove(context) { Name = new app.variable.@this("nonexistent") };
        var result = await action.Run();

        await result.IsSuccess();
    }
}
