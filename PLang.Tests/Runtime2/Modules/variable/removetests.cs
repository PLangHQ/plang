using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.actions.variable;
using VariableResult = PLang.Runtime2.actions.variable.types.variable;

namespace PLang.Tests.Runtime2.actions.variable;

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
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.exists).IsTrue();
        await Assert.That(memory.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentVariable_ReturnsFalse()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(new remove { name = "nonexistent" });

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.exists).IsFalse();
    }

    [Test]
    public async Task Remove_NullParameters_ReturnsError()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync((object?)null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");
    }
}
