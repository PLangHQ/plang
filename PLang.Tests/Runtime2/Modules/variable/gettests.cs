using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Modules.variable;

namespace PLang.Tests.Runtime2.Modules.variable;

public class GetTests
{
    private (GetHandler handler, MemoryStack memory) Create(MemoryStack? memoryStack = null)
    {
        var handler = new GetHandler();
        var appContext = new PLangAppContext("/app");
        var memory = memoryStack ?? new MemoryStack();
        var context = new PLangContext(appContext, memory);
        var engine = new Engine(appContext);
        handler.Initialize(engine, context);
        return (handler, memory);
    }

    [Test]
    public async Task Get_ReturnsVariable()
    {
        var memory = new MemoryStack();
        memory.Set("testVar", "testValue");
        var (handler, _) = Create(memory);

        var result = await handler.ExecuteAsync(new get { name = "testVar" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNull()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(new get { name = "nonexistent" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task Get_MissingName_ReturnsError()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingName");
    }

    [Test]
    public async Task Get_EmptyName_ReturnsError()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(new get { name = "" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingName");
    }
}
