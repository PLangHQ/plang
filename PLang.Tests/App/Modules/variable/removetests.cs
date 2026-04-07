using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class RemoveTests
{
    private (global::App.Actor.Context.@this context, Variables memory) CreateContext()
    {
        var app = new global::App.@this("/app");
        return (app.Context, app.Context.Variables);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var (context, memory) = CreateContext();
        memory.Set("testVar", "testValue");

        var action = new Remove { Context = context, Name = "testVar" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_Succeeds()
    {
        var (context, _) = CreateContext();

        var action = new Remove { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }
}
