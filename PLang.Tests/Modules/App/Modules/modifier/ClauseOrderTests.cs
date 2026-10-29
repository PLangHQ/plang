namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// An action's own clauses run around its catalog action's list, in decision 11's order: the actor's before events, the
/// cache lookup, the action, the cache store, its own on error, the actor's error events, the actor's after events.
/// </summary>
public class ClauseOrderTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // error.throw with its clauses after it.
    private PrAction Throwing(params PrAction[] clauses)
        => global::PLang.Tests.Shared.Make.With(new PrAction(_app.type.list)
        {
            Module = _app.Module("error"), Name = "throw",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("message", "boom", context: Ctx) }),
        }, clauses);

    private PrAction Clause(string name, params (string, object?)[] parameters)
        => global::PLang.Tests.Shared.Make.Action(Ctx, "on", name, parameters);

    // The user's event on error.throw's moment, recording what it ran on.
    private async Task Record(string moment, global::app.@event.When when, List<string> ran, string name)
        => await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(_app.Module("error")["throw"]!, moment, when,
            (_, result, ctx) => { ran.Add($"{name} {(result.Success ? "ok" : result.Error!.Key)}"); return Task.FromResult(result); },
            _app.actor.list.User, global::app.@event.code.@this.Always));

    [Test]
    public async Task OnErrorIgnore_NeverPutsAnEmptyResultInTheCache()
    {
        var action = Throwing(Clause("cache", ("Duration", System.TimeSpan.FromMinutes(1)), ("Key", "ignored-key")),
            Clause("error", ("ignore", true)));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That(await Ctx.App!.Cache.GetAsync("ignored-key")).IsNull();
    }

    [Test]
    public async Task TheStepsOwnOnError_TakesTheFailure_BeforeTheActorsErrorEventSeesIt()
    {
        var ran = new List<string>();
        await Record("error", global::app.@event.When.before, ran, "actor's error");
        var action = Throwing(Clause("error", ("ignore", true)));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That(ran).IsEmpty();
    }

    [Test]
    public async Task TheActorsAfter_RunsLast_OnWhatOnErrorAnswered()
    {
        var ran = new List<string>();
        await Record("start", global::app.@event.When.after, ran, "after");
        var action = Throwing(Clause("error", ("ignore", true)));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That(ran).IsEquivalentTo(new[] { "after ok" });
    }
}
