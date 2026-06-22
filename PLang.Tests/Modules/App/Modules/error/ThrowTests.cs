using app.actor.context;
using app;
using app.variable;
using app.module.error;
using Text = global::app.type.text.@this;
using ListType = global::app.type.list.@this;

namespace PLang.Tests.App.actions.error;

/// <summary>
/// Floor for error.throw — the cases with no clean language surface. The
/// message / key / status-code / Format-rendering behaviors moved to
/// <see cref="ErrorThrowGoalRunTests"/> (through the engine). What stays here:
/// re-raising a raw <c>ServiceError</c> (an error object has no language literal to
/// seed it with — it only arises from an <c>on error</c> capture), and the internal
/// <c>Error.Data</c> list-normalization shape (1..N values), which is an internal
/// structure assertion, not something a goal observes as such.
/// </summary>
public class ThrowTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Throw_ReRaisesErrorObject_PreservingKeyMessageStatus()
    {
        // `- throw %!error%` re-raises an existing Error as-is (Key, Message,
        // StatusCode preserved) — not stringified, not re-wrapped as a payload.
        // The existing error arrives through the Data slot (a variable).
        var (context, _) = CreateContext();
        var original = new global::app.error.ServiceError("original boom", "OriginalKey", 418);

        var action = new Throw { Context = context, Data = Data.Ok(original) };
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

        var action = new Throw { Context = context, Data = Data.Ok((Text)"order-123") };
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
        var inner = new ListType(new[] { Data.Ok((Text)"order-123"), Data.Ok((Text)"item-9") });

        var action = new Throw { Context = context, Data = Data.Ok(inner) };
        var result = await action.Run();

        await result.IsFailure();
        var err = (global::app.error.Error)result.Error!;
        var list = err.Data!.Peek() as ListType;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count.ToInt32()).IsEqualTo(2);
        await Assert.That(list.At(0)!.Peek()!.ToString()).IsEqualTo("order-123");
        await Assert.That(list.At(1)!.Peek()!.ToString()).IsEqualTo("item-9");
    }
}
