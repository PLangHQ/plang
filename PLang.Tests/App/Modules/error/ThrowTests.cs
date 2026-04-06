using App.Context;
using App;
using App.Variables;
using App.modules.error;

namespace PLang.Tests.App.actions.error;

public class ThrowTests
{
    private (PLangContext context, Variables memory) CreateContext()
    {
        var engine = new App.@this("/app");
        var memory = new Variables();
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
