using App.Engine.Context;
using App.Engine;
using App.Engine.Variables;
using App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class RemoveTests
{
    private (PLangContext context, Variables memory) CreateContext(Variables? memoryStack = null)
    {
        var memory = memoryStack ?? new Variables();
        var engine = new App.Engine.@this("/app");
        var context = new PLangContext(engine, memory);
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
