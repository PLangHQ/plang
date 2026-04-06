using App.Context;
using App;
using App.Variables;
using App.modules.variable;
using Type = App.Variables.Type;

namespace PLang.Tests.App.actions.variable;

public class SetTests
{
    private (PLangContext context, Variables memory) CreateContext(Variables? memoryStack = null)
    {
        var memory = memoryStack ?? new Variables();
        var engine = new App.@this("/app");
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
    public async Task Set_ReturnsDataFromStack()
    {
        var (context, _) = CreateContext();

        var action = new Set { Context = context, Name = "testVar", Value = "testValue" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Name).IsEqualTo("testVar");
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_ReturnsTypeOnData()
    {
        var (context, _) = CreateContext();

        var action = new Set { Context = context, Name = "count", Value = 42, Type = "int" };
        var result = await action.Run();

        await Assert.That(result.Type!.Value).IsEqualTo("int");
    }
}
