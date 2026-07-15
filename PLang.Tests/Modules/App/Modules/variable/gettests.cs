using app.actor.context;
using app;
using app.variable;
using app.module.action.variable;

namespace PLang.Tests.App.actions.variable;

public class GetTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Get_ReturnsRawValue()
    {
        var (context, _) = CreateContext();
        context.Variable.Set("testVar", "testValue");

        var action = new Get(context) { Name = new app.variable.@this("testVar") };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("testValue");
        await Assert.That(result.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNull()
    {
        var (context, _) = CreateContext();

        var action = new Get(context) { Name = new app.variable.@this("nonexistent") };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue();
    }
}
