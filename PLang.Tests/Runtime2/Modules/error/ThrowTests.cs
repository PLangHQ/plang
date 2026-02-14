using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.error;

namespace PLang.Tests.Runtime2.actions.error;

public class ThrowTests
{
    private (PLangContext context, MemoryStack memory) CreateContext()
    {
        var engine = new Engine("/app");
        var memory = new MemoryStack();
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Throw_ReturnsFailure()
    {
        var (context, _) = CreateContext();

        var action = new Throw { Context = context, Message = "Something went wrong", StatusCode = 500 };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
    }

    [Test]
    public async Task Throw_UsesCustomKey()
    {
        var (context, _) = CreateContext();

        var action = new Throw { Context = context, Message = "Not found", StatusCode = 404, Key = "NotFound" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Throw_DefaultsStatusCode500()
    {
        var (context, _) = CreateContext();

        var action = new Throw { Context = context, Message = "Server error" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }
}
