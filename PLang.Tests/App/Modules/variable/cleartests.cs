using App.Engine.Context;
using App.Engine;
using App.Engine.Variables;
using App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class ClearTests
{
    private (PLangContext context, Variables memory) CreateContext(Variables? memoryStack = null)
    {
        var memory = memoryStack ?? new Variables();
        var engine = new App.Engine.@this("/app");
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Clear_ClearsAllVariables()
    {
        var memory = new Variables();
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
        var memory = new Variables();
        memory.Set("userVar", "value");
        var (context, _) = CreateContext(memory);

        var action = new Clear { Context = context };
        await action.Run();

        await Assert.That(memory.Contains("Now")).IsTrue();
        await Assert.That(memory.Contains("NowUtc")).IsTrue();
        await Assert.That(memory.Contains("GUID")).IsTrue();
    }
}
