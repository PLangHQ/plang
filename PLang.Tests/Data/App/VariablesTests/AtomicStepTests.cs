using app.type.item.variable;
using List = global::app.type.item.list.@this;

namespace PLang.Tests.App.VariablesTests;

/// <summary>
/// The store's one-step writes: Ensure (what the name holds, or a new value stored at once) and
/// Replace (a write-back that happens only if the name still holds what the caller read).
/// </summary>
public class AtomicStepTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/atomic-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Replace_NameReplacedInBetween_LeavesTheNewerValue()
    {
        var store = new Variables(_app.actor.list.User.Context);
        await store.Set("l", new List());
        var held = await store.Get("l");
        var newer = new List();
        await store.Set("l", newer);

        var answer = await store.Replace("l", held, new List());

        // the newer value is left alone, and it is the answer
        await Assert.That(ReferenceEquals(await answer.Value(), newer)).IsTrue();
        await Assert.That(ReferenceEquals(await (await store.Get("l")).Value(), newer)).IsTrue();
    }

    [Test]
    public async Task Replace_WhatIsBoundOnTheSetRefusing_IsTheAnswer_NothingWritten()
    {
        var store = _app.actor.list.User.Context.Variable;
        await store.Set("l", new List());
        var held = await store.Get("l");
        _app.variable.Own().Bind("set", global::app.@event.When.before,
            (_, _, c) => System.Threading.Tasks.Task.FromResult(c.Error(new global::app.error.Error("not today", "Refused", 403))),
            _app.actor.list.User, global::app.@event.binding.Scope.actor);

        var answer = await store.Replace("l", held, new List());

        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("Refused");
        await Assert.That(ReferenceEquals(await store.Get("l"), held)).IsTrue();
    }

    [Test]
    public async Task Replace_NameStillHeld_WritesTheValue()
    {
        var store = new Variables(_app.actor.list.User.Context);
        await store.Set("l", new List());
        var held = await store.Get("l");
        var value = new List();

        var written = await store.Replace("l", held, value);

        await written.IsSuccess();
        await Assert.That(ReferenceEquals(await written.Value(), value)).IsTrue();
        await Assert.That(ReferenceEquals(await (await store.Get("l")).Value(), value)).IsTrue();
    }

    [Test]
    public async Task Ensure_NameHoldsAValue_AnswersIt()
    {
        var store = new Variables(_app.actor.list.User.Context);
        var existing = new List();
        await store.Set("l", existing);

        var held = await store.Ensure("l", () => _app.type.list["list"].Create(System.Array.Empty<object?>(), _app.actor.list.User.Context));

        await Assert.That(ReferenceEquals(await held.Value(), existing)).IsTrue();
    }

    [Test]
    public async Task Ensure_ManyAtOnce_AllAnswerOneValue()
    {
        var store = new Variables(_app.actor.list.User.Context);

        var held = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(async () => await (await store.Ensure("l",
                () => _app.type.list["list"].Create(System.Array.Empty<object?>(), _app.actor.list.User.Context))).Value())));

        await Assert.That(held.Distinct(ReferenceEqualityComparer.Instance).Count()).IsEqualTo(1);
    }
}
