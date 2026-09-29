using app.actor.context;
using app;
using app.type.item.variable;
using app.module.variable;

namespace PLang.Tests.App.actions.variable;

public class GetTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app").Testing();
        return (app.actor.list.User.Context, app.actor.list.User.Context.Variable);
    }

    [Test]
    public async Task Get_ReturnsRawValue()
    {
        var (context, _) = CreateContext();
        context.Variable.Set("testVar", "testValue");

        var action = new Get(context) { Name = new app.type.item.variable.@this("testVar") };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("testValue");
        await Assert.That(result.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNull()
    {
        var (context, _) = CreateContext();

        var action = new Get(context) { Name = new app.type.item.variable.@this("nonexistent") };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue();
    }
}
