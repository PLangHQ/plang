using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.variable;

namespace PLang.Tests.Runtime2.actions.variable;

public class RemoveTests
{
    private (PLangContext context, MemoryStack memory) CreateContext(MemoryStack? memoryStack = null)
    {
        var memory = memoryStack ?? new MemoryStack();
        var engine = new PLang.Runtime2.Engine.@this("/app");
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var memory = new MemoryStack();
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
