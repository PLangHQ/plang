using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.variable;
using VariableResult = PLang.Runtime2.modules.variable.types.variable;
using Type = PLang.Runtime2.Memory.Type;

namespace PLang.Tests.Runtime2.actions.variable;

public class SetTests
{
    private (PLangContext context, MemoryStack memory) CreateContext(MemoryStack? memoryStack = null)
    {
        var memory = memoryStack ?? new MemoryStack();
        var engine = new Engine("/app");
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var (context, memory) = CreateContext();

        var action = new Set { Context = context, Name = "testVar", Value = "testValue" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var (context, memory) = CreateContext();

        var action = new Set { Context = context, Name = "count", Value = 42, Type = "int" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Get("count")!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_ReturnsTypedVariable()
    {
        var (context, _) = CreateContext();

        var action = new Set { Context = context, Name = "testVar", Value = "testValue" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.name).IsEqualTo("testVar");
        await Assert.That(v.value).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_ReturnsTypeInResult()
    {
        var (context, _) = CreateContext();

        var action = new Set { Context = context, Name = "count", Value = 42, Type = "int" };
        var result = await action.Run();

        var v = result.Value as VariableResult;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.type).IsEqualTo("int");
    }
}
