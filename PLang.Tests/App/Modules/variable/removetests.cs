using App.Actor.Context;
using App;
using App.Variables;
using App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class RemoveTests
{
    private (global::App.Actor.Context.@this context, Variables memory) CreateContext(Variables? variables = null)
    {
        var memory = variables ?? new Variables();
        var engine = new App.@this("/app");
        var context = new global::App.Actor.Context.@this(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var memory = new Variables();
        memory.Set("testVar", "testValue");
        var (context, _) = CreateContext(memory);

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
