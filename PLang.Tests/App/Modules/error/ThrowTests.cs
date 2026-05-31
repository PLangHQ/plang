using app.actor.context;
using app;
using app.variable;
using app.module.error;

namespace PLang.Tests.App.actions.error;

public class ThrowTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Throw_ReturnsFailure()
    {
        var (context, _) = CreateContext();

        var action = new Throw { Context = context, Message = Data.Ok("Something went wrong"), StatusCode = 500 };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
    }

    [Test]
    public async Task Throw_UsesCustomKey()
    {
        var (context, _) = CreateContext();

        var action = new Throw { Context = context, Message = Data.Ok("Not found"), StatusCode = 404, Key = "NotFound" };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Throw_DefaultsStatusCode500()
    {
        var (context, _) = CreateContext();

        var action = new Throw { Context = context, Message = Data.Ok("Server error") };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Throw_ReRaisesErrorObject_PreservingKeyMessageStatus()
    {
        // `- throw %!error%` re-raises an existing Error as-is (Key, Message,
        // StatusCode preserved) — not stringified into the string slot.
        var (context, _) = CreateContext();
        var original = new global::app.error.ServiceError("original boom", "OriginalKey", 418);

        var action = new Throw { Context = context, Message = Data.Ok(original) };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("OriginalKey");
        await Assert.That(result.Error.Message).IsEqualTo("original boom");
        await Assert.That(result.Error.StatusCode).IsEqualTo(418);
    }
}
