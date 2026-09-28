using app.actor.context;
using app;
using app.type.item.variable;
using app.module.action.variable;

namespace PLang.Tests.App.actions.variable;

public class RemoveTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.actor.list.User.Context, app.actor.list.User.Context.Variable);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var (context, memory) = CreateContext();
        memory.Set("testVar", "testValue");

        var action = new Remove(context) { Name = new app.type.item.variable.@this("testVar") };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_Succeeds()
    {
        var (context, _) = CreateContext();

        var action = new Remove(context) { Name = new app.type.item.variable.@this("nonexistent") };
        var result = await action.Start();

        await result.IsSuccess();
    }
}
