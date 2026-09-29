namespace PLang.Tests.App.Errors;

// A program's mistake reaches the result under its own key: a catch that turns an exception into an error keeps
// the key the exception carries, never a generic one of its own.
public class KeepsItsKeyTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    // A before-binding runs outside the action's dispatch; what it throws ends the step, under its key.
    [Test] public async Task AStepsKeyedFailure_IsItsResultsKey()
    {
        var goal = Make.Goal(Ctx, "Start", "/Start.goal", Make.Step("write out hi", Make.Action(Ctx, "output", "write", ("Data", "hi"))));
        app.type.list["action"].Own().Bind("start", global::app.@event.When.before,
            (item, result, c) => Task.FromException<global::app.data.@this>(new global::app.error.AppException("refused", "MyRefusal", 418)),
            app.actor.list.User, global::app.@event.binding.Scope.actor);

        var result = await goal.Step.Items().First().Start(Ctx);

        await Assert.That(result.Error?.Key).IsEqualTo("MyRefusal");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(418);
    }

    private sealed class Throwing { public string Name => throw new System.InvalidOperationException("no name"); }

    // A value whose property can't be read fails its write under the write's own key.
    [Test] public async Task AGetterThatThrows_FailsTheEncode_AsOutputGetterThrew()
    {
        using var ms = new System.IO.MemoryStream();
        var encoded = await Ctx.Format("application/json").Encode(ms, new global::app.data.@this("x", new Throwing(), context: Ctx), Ctx);

        await Assert.That(encoded.Error?.Key).IsEqualTo("OutputGetterThrew");
    }

    // An option given a value it can't take is refused with the reason the option gives.
    [Test] public async Task AnOptionsRefusedValue_KeepsTheOptionsKey()
    {
        var set = Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "%!app.goal.list.setting.os%", "variable"), ("Value", "notabool"));

        var result = await set.Start(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsNotEqualTo("CannotSetChild");
        await Assert.That(result.Error.Key).IsNotEqualTo("ServiceError");
    }
}
