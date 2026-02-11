using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.variable;

namespace PLang.Tests.Runtime2.actions.variable;

public class ClearTests
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
    public async Task Clear_ClearsAllVariables()
    {
        var memory = new MemoryStack();
        memory.Set("var1", "value1");
        memory.Set("var2", "value2");
        var (context, _) = CreateContext(memory);

        var action = new Clear { Context = context };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Contains("var1")).IsFalse();
        await Assert.That(memory.Contains("var2")).IsFalse();
    }

    [Test]
    public async Task Clear_PreservesSystemVariables()
    {
        var memory = new MemoryStack();
        memory.Set("userVar", "value");
        var (context, _) = CreateContext(memory);

        var action = new Clear { Context = context };
        await action.Run();

        await Assert.That(memory.Contains("Now")).IsTrue();
        await Assert.That(memory.Contains("NowUtc")).IsTrue();
        await Assert.That(memory.Contains("GUID")).IsTrue();
    }
}
