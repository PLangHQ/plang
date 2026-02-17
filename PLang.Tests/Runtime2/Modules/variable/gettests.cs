using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.actions.variable;

namespace PLang.Tests.Runtime2.actions.variable;

public class GetTests
{
    private (PLangContext context, MemoryStack memory) CreateContext(MemoryStack? memoryStack = null)
    {
        var memory = memoryStack ?? new MemoryStack();
        var engine = new Engine("/app");
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Get_ReturnsRawValue()
    {
        var memory = new MemoryStack();
        memory.Set("testVar", "testValue");
        var (context, _) = CreateContext(memory);

        var action = new Get { Context = context, Name = "testVar" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
        await Assert.That(result.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNull()
    {
        var (context, _) = CreateContext();

        var action = new Get { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }
}
