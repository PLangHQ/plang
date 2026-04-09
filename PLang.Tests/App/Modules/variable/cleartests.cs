using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class ClearTests
{
    private (global::App.Actor.Context.@this context, Variables memory) CreateContext()
    {
        var app = new global::App.@this("/app");
        return (app.Context, app.Context.Variables);
    }

    [Test]
    public async Task Clear_ClearsAllVariables()
    {
        var (context, memory) = CreateContext();
        memory.Set("var1", "value1");
        memory.Set("var2", "value2");

        var action = new Clear { Context = context };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memory.Contains("var1")).IsFalse();
        await Assert.That(memory.Contains("var2")).IsFalse();
    }

    [Test]
    public async Task Clear_PreservesSystemVariables()
    {
        var (context, memory) = CreateContext();
        memory.Set("userVar", "value");

        var action = new Clear { Context = context };
        await action.Run();

        await Assert.That(memory.Contains("Now")).IsTrue();
        await Assert.That(memory.Contains("NowUtc")).IsTrue();
        await Assert.That(memory.Contains("GUID")).IsTrue();
    }
}
