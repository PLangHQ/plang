using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class ExistsTests
{
    private (global::App.Actor.Context.@this context, Variables memory) CreateContext()
    {
        var app = new global::App.@this("/app");
        return (app.Context, app.Context.Variables);
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
