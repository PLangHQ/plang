using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Modules.variable;

namespace PLang.Tests.Runtime2.Modules.variable;

public class RemoveTests
{
    private (RemoveHandler handler, MemoryStack memory) Create(MemoryStack? memoryStack = null)
    {
        var handler = new RemoveHandler();
        var appContext = new PLangAppContext("/app");
        var memory = memoryStack ?? new MemoryStack();
        var context = new PLangContext(appContext, memory);
        var engine = new Engine(appContext);
        handler.Initialize(engine, context);
        return (handler, memory);
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var memory = new MemoryStack();
        memory.Set("testVar", "testValue");
        var (handler, _) = Create(memory);

        var result = await handler.ExecuteAsync(new remove { name = "testVar" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_ReturnsFalse()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(new remove { name = "nonexistent" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Remove_MissingName_ReturnsError()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingName");
    }
}
