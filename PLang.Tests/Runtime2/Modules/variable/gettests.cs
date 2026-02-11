using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.variable;
using VariableResult = PLang.Runtime2.modules.variable.types.variable;

namespace PLang.Tests.Runtime2.actions.variable;

public class GetTests
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
    public async Task Get_ReturnsVariable()
    {
        var memory = new MemoryStack();
        memory.Set("testVar", "testValue");
        var (context, _) = CreateContext(memory);

        var action = new Get { Context = context, Name = "testVar" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.name).IsEqualTo("testVar");
        await Assert.That(v.value).IsEqualTo("testValue");
        await Assert.That(v.exists).IsTrue();
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNotExists()
    {
        var (context, _) = CreateContext();

        var action = new Get { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.value).IsNull();
        await Assert.That(v.exists).IsFalse();
    }
}
