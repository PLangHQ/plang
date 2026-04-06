using App.Engine.Context;
using App.Engine;
using App.Engine.Variables;
using App.modules.variable;

namespace PLang.Tests.App.actions.variable;

public class ExistsTests
{
    private (PLangContext context, Variables memory) CreateContext(Variables? memoryStack = null)
    {
        var memory = memoryStack ?? new Variables();
        var engine = new App.Engine.@this("/app");
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Exists_ExistingVariable_ReturnsTrue()
    {
        var memory = new Variables();
        memory.Set("testVar", "testValue");
        var (context, _) = CreateContext(memory);

        var action = new Exists { Context = context, Name = "testVar" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentVariable_ReturnsFalse()
    {
        var (context, _) = CreateContext();

        var action = new Exists { Context = context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }
}
