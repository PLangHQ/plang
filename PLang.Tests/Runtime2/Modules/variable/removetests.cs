using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.variable;
using VariableResult = PLang.Runtime2.modules.variable.types.variable;

namespace PLang.Tests.Runtime2.actions.variable;

public class RemoveTests
{
    private (PLangContext context, MemoryStack memory) CreateContext(MemoryStack? memoryStack = null)
    {
        var appContext = new PLangAppContext("/app");
        var memory = memoryStack ?? new MemoryStack();
        var context = new PLangContext(appContext, memory);
        var engine = new Engine(appContext);
        context.RegisterContextVariables(engine);
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
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.exists).IsTrue();
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_ReturnsFalse()
    {
        var (context, _) = CreateContext();

        var action = new Remove { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.exists).IsFalse();
    }
}
