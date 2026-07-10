using app.actor.context;
using app;
using app.variable;
using app.module.error;
using Text = global::app.type.item.text.@this;
using ListType = global::app.type.list.@this;

namespace PLang.Tests.App.actions.error;

public class ThrowTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Throw_ReturnsFailure()
    {
        var (context, _) = CreateContext();

        var action = new Throw(context) { Message = (Text)"Something went wrong", StatusCode = (global::app.type.item.number.@this)500 };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
    }

    [Test]
    public async Task Throw_UsesCustomKey()
    {
        var (context, _) = CreateContext();

        var action = new Throw(context) { Message = (Text)"Not found", StatusCode = (global::app.type.item.number.@this)404, Key = (Text)"NotFound" };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Throw_DefaultsStatusCode500()
    {
        var (context, _) = CreateContext();

        var action = new Throw(context) { Message = (Text)"Server error" };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Throw_ReRaisesErrorObject_PreservingKeyMessageStatus()
    {
        // `- throw %!error%` re-raises an existing Error as-is (Key, Message,
        // StatusCode preserved) — not stringified, not re-wrapped as a payload.
        // The existing error arrives through the Data slot (a variable).
        var (context, _) = CreateContext();
        var original = new global::app.error.ServiceError("original boom", "OriginalKey", 418);

        var action = new Throw(context) { Data = context.Ok(original) };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("OriginalKey");
        await Assert.That(result.Error.Message).IsEqualTo("original boom");
        await Assert.That(result.Error.StatusCode).IsEqualTo(418);
    }

    [Test]
    public async Task Throw_AttachesSingleValue_AsListOfOne()
    {
        // `- throw %order%` — a single value attaches as a list of one, kept typed,
        // never flattened. Reachable as %!error.data%.
        var (context, _) = CreateContext();

        var action = new Throw(context) { Data = Data.Ok((Text)"order-123") };
        var result = await action.Run();

        await result.IsFailure();
        var err = (global::app.error.Error)result.Error!;
        await Assert.That(err.Data).IsNotNull();
        var list = err.Data!.Peek() as ListType;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count.ToInt32()).IsEqualTo(1);
        await Assert.That(list.First!.Peek()!.ToString()).IsEqualTo("order-123");
    }

    [Test]
    public async Task Throw_AttachesMultipleValues_AsList()
    {
        // `- throw %order%, %item%` — multiple values ride as a plang list, each
        // element keeping its own value/type.
        var (context, _) = CreateContext();
        var inner = new ListType(new[] { Data.Ok((Text)"order-123"), Data.Ok((Text)"item-9") }, global::PLang.Tests.TestApp.SharedContext);

        var action = new Throw(context) { Data = Data.Ok(inner) };
        var result = await action.Run();

        await result.IsFailure();
        var err = (global::app.error.Error)result.Error!;
        var list = err.Data!.Peek() as ListType;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count.ToInt32()).IsEqualTo(2);
        await Assert.That(list.At(0)!.Peek()!.ToString()).IsEqualTo("order-123");
        await Assert.That(list.At(1)!.Peek()!.ToString()).IsEqualTo("item-9");
    }

    [Test]
    public async Task Throw_DataRendersFullyInFormat()
    {
        // The attached value shows in the error display (Format), not as a type name.
        var (context, _) = CreateContext();

        var action = new Throw(context) { Message = (Text)"checkout failed", Data = Data.Ok((Text)"order-123") };
        var result = await action.Run();

        var formatted = result.Error!.Format();
        await Assert.That(formatted).Contains("checkout failed");
        await Assert.That(formatted).Contains("order-123");
    }
}
