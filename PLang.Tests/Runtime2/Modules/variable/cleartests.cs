using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Modules.variable;

namespace PLang.Tests.Runtime2.Modules.variable;

public class ClearTests
{
    private (ClearHandler handler, MemoryStack memory) Create(MemoryStack? memoryStack = null)
    {
        var handler = new ClearHandler();
        var appContext = new PLangAppContext("/app");
        var memory = memoryStack ?? new MemoryStack();
        var context = new PLangContext(appContext, memory);
        var engine = new Engine(appContext);
        handler.Initialize(engine, context);
        return (handler, memory);
    }

    [Test]
    public async Task Clear_ClearsAllVariables()
    {
        var memory = new MemoryStack();
        memory.Set("var1", "value1");
        memory.Set("var2", "value2");
        var (handler, _) = Create(memory);

        var result = await handler.ExecuteAsync(null);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Contains("var1")).IsFalse();
        await Assert.That(memory.Contains("var2")).IsFalse();
    }

    [Test]
    public async Task Clear_PreservesSystemVariables()
    {
        var memory = new MemoryStack();
        memory.Set("userVar", "value");
        var (handler, _) = Create(memory);

        await handler.ExecuteAsync(null);

        await Assert.That(memory.Contains("Now")).IsTrue();
        await Assert.That(memory.Contains("NowUtc")).IsTrue();
        await Assert.That(memory.Contains("GUID")).IsTrue();
    }
}
