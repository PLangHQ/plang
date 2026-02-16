using PLang.Runtime2.Context;
using PLang.Runtime2;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.variable;
using VariableResult = PLang.Runtime2.modules.variable.types.variable;

namespace PLang.Tests.Runtime2.actions.variable;

public class ExistsTests
{
    private (PLangContext context, MemoryStack memory) CreateContext(MemoryStack? memoryStack = null)
    {
        var memory = memoryStack ?? new MemoryStack();
        var engine = new Engine("/app");
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Exists_ExistingVariable_ReturnsTrue()
    {
        var memory = new MemoryStack();
        memory.Set("testVar", "testValue");
        var (context, _) = CreateContext(memory);

        var action = new Exists { Context = context, Name = "testVar" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.exists).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentVariable_ReturnsFalse()
    {
        var (context, _) = CreateContext();

        var action = new Exists { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.exists).IsFalse();
    }
}
