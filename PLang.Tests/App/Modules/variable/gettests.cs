using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class GetTests
{
    private (global::App.Actor.Context.@this context, Variables memory) CreateContext(Variables? variables = null)
    {
        var memory = variables ?? new Variables();
        var engine = new global::App.@this("/app");
        var context = new global::App.Actor.Context.@this(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Get_ReturnsRawValue()
    {
        var memory = new Variables();
        memory.Set("testVar", "testValue");
        var (context, _) = CreateContext(memory);

        var action = new Get { Context = context, Name = "testVar" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
        await Assert.That(result.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Get_NonexistentVariable_ReturnsNull()
    {
        var (context, _) = CreateContext();

        var action = new Get { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }
}
