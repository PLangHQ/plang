using app.actor.context;
using app;
using app.type.item.variable;
using app.module.action.variable;

namespace PLang.Tests.App.actions.variable;

public class ExistsTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.actor.list.User.Context, app.actor.list.User.Context.Variable);
    }

    [Test]
    public async Task Exists_ExistingVariable_ReturnsTrue()
    {
        var (context, _) = CreateContext();
        context.Variable.Set("testVar", "testValue");

        var action = new Exists(context) { Name = new app.type.item.variable.@this("testVar") };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentVariable_ReturnsFalse()
    {
        var (context, _) = CreateContext();

        var action = new Exists(context) { Name = new app.type.item.variable.@this("nonexistent") };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsFalse();
    }
}
