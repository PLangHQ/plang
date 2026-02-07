using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Modules.variable;
using TypeInfo = PLang.Runtime2.Memory.TypeInfo;

namespace PLang.Tests.Runtime2.Modules.variable;

public class SetTests
{
    private (SetHandler handler, MemoryStack memory) Create(MemoryStack? memoryStack = null)
    {
        var handler = new SetHandler();
        var appContext = new PLangAppContext("/app");
        var memory = memoryStack ?? new MemoryStack();
        var context = new PLangContext(appContext, memory);
        var engine = new Engine(appContext);
        handler.Initialize(engine, context);
        return (handler, memory);
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var (handler, memory) = Create();

        var result = await handler.ExecuteAsync(new set { name = "testVar", value = "testValue" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var (handler, memory) = Create();

        var result = await handler.ExecuteAsync(new set { name = "count", value = 42, type = "int" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Get("count")!.TypeInfo!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_ReturnsValueInResult()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(new set { name = "testVar", value = "testValue" });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_NullParameters_ReturnsError()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidParameters");
    }

    [Test]
    public async Task Set_MissingName_ReturnsError()
    {
        var (handler, _) = Create();

        var result = await handler.ExecuteAsync(new set { name = "", value = "testValue" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidParameters");
    }
}
