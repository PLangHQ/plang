using app.actor.context;
using app;
using app.variable;
using app.module.action.error;
using Text = global::app.type.item.text.@this;
using ListType = global::app.type.item.list.@this;

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
    public async Task Throw_DefaultsStatusCodeAndKey()
    {
        var (context, _) = CreateContext();

        var action = new Throw(context) { Message = (Text)"Server error" };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
        await Assert.That(result.Error.Key).IsEqualTo("error");
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
    public async Task Throw_ReRaiseWithFixSuggestion_SameErrorGainsTheFix()
    {
        // `- throw %!error%, fix suggestion %fix%` — the same error instance, now with its fix.
        var (context, _) = CreateContext();
        var original = new global::app.error.ServiceError("else apart from its if", "ElseWithoutIf", 400);

        var action = new Throw(context)
        {
            Data = context.Ok(original),
            FixSuggestion = (Text)"- if %x% == 1, write out \"one\", else write out \"other\""
        };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(ReferenceEquals(result.Error, original)).IsTrue();
        await Assert.That(result.Error!.FixSuggestion).IsEqualTo("- if %x% == 1, write out \"one\", else write out \"other\"");
    }

    [Test]
    public async Task Throw_NewErrorWithFixSuggestion_IsBornWithIt()
    {
        var (context, _) = CreateContext();

        var action = new Throw(context) { Message = (Text)"bad input", FixSuggestion = (Text)"pass a number" };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.FixSuggestion).IsEqualTo("pass a number");
    }

    [Test]
    public async Task Throw_ReRaiseWithoutFixSuggestion_LeavesTheErrorsOwn()
    {
        var (context, _) = CreateContext();
        var original = new global::app.error.ServiceError("boom", "K", 400) { FixSuggestion = "its own" };

        var result = await new Throw(context) { Data = context.Ok(original) }.Run();

        await Assert.That(result.Error!.FixSuggestion).IsEqualTo("its own");
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
        await Assert.That(list.First(global::PLang.Tests.TestApp.SharedContext)!.Peek()!.ToString()).IsEqualTo("order-123");
    }

    [Test]
    public async Task Throw_AttachesMultipleValues_AsList()
    {
        // `- throw %order%, %item%` — multiple values ride as a plang list, each
        // element keeping its own value/type.
        var (context, _) = CreateContext();
        var inner = new ListType(new[] { Data.Ok((Text)"order-123"), Data.Ok((Text)"item-9") });

        var action = new Throw(context) { Data = Data.Ok(inner) };
        var result = await action.Run();

        await result.IsFailure();
        var err = (global::app.error.Error)result.Error!;
        var list = err.Data!.Peek() as ListType;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count.ToInt32()).IsEqualTo(2);
        await Assert.That(list.At(0, global::PLang.Tests.TestApp.SharedContext)!.Peek()!.ToString()).IsEqualTo("order-123");
        await Assert.That(list.At(1, global::PLang.Tests.TestApp.SharedContext)!.Peek()!.ToString()).IsEqualTo("item-9");
    }

}
