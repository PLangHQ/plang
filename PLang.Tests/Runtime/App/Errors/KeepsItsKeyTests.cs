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
        await Assert.That(result.Error!.Status.Code.ToInt32()).IsEqualTo(418);
    }

    private sealed class Throwing { public string Name => throw new System.InvalidOperationException("no name"); }

    // A value whose property can't be read fails its write under the write's own key.
    [Test] public async Task AGetterThatThrows_FailsTheEncode_AsOutputGetterThrew()
    {
        using var ms = new System.IO.MemoryStream();
        var encoded = await Ctx.Format("application/json").Encode(ms, new global::app.data.@this("x", new Throwing(), context: Ctx), Ctx);

        await Assert.That(encoded.Error?.Key).IsEqualTo("OutputGetterThrew");
    }

    // A value a type declines to make is the birth's answer, with the reason the type gave — never thrown.
    [Test] public async Task ATypeThatDeclines_AnswersItsOwnReason()
    {
        var born = await Ctx.App.type.list["number"].Create(new global::app.type.item.text.@this("abc"), Ctx);

        await born.IsFailure();
        await Assert.That(born.Error!.Key).IsEqualTo("NumberConversionFailed");
    }

    // An input channel that is a goal asking again from its own body has no input to ask on: NoInputChannel,
    // answered — not thrown.
    [Test] public async Task AnAskWithNoInputToAskOn_IsNoInputChannel()
    {
        app.goal.list.Add(await RealGoalLoad.ViaChannel(app, Make.Goal(Ctx, "AskAgain", "/AskAgain.goal",
            Make.Step("ask \"again?\"", Make.Action(Ctx, "output", "ask", ("Question", "again?"))))));
        await (await new global::app.module.channel.Set(Ctx)
            { Name = new global::app.type.item.text.@this("input"), Goal = Make.Call(Ctx, "AskAgain") }.Start()).IsSuccess();

        var asked = await Make.Action(Ctx, "output", "ask", ("Question", "hi?")).Start(Ctx);

        await asked.IsFailure();
        await Assert.That(asked.Error!.Key).IsEqualTo("NoInputChannel");
    }

    private sealed class Refusing : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string Read(ref System.Text.Json.Utf8JsonReader reader, System.Type type, System.Text.Json.JsonSerializerOptions options) => "";
        public override void Write(System.Text.Json.Utf8JsonWriter writer, string value, System.Text.Json.JsonSerializerOptions options)
            => throw new global::app.data.OutputException("refused", "OutputGetterThrew");
    }

    // A program error thrown inside a System.Text.Json converter reaches its catcher as itself — STJ doesn't
    // wrap it — so a catch answering AppException sees its Error.
    [Test] public async Task AnAppExceptionThrownInAConverter_PassesThroughUnwrapped()
    {
        var options = new System.Text.Json.JsonSerializerOptions { Converters = { new Refusing() } };

        var thrown = await Assert.ThrowsAsync<global::app.data.OutputException>(async () =>
        {
            System.Text.Json.JsonSerializer.Serialize("x", options);
            await Task.CompletedTask;
        });

        await Assert.That(thrown!.Error.Key).IsEqualTo("OutputGetterThrew");
    }

    // A path template (as the build marks one) resolves when read: a scheme no path kind holds is the read's answer.
    [Test] public async Task ATemplatePathToAnUnknownScheme_ReadsAsSchemeNotRegistered()
    {
        await Ctx.Variable.Set("where", Ctx.Ok("s3://bucket"));
        var path = Make.Built(Ctx, "p", "%where%/x", new global::app.type.@this("path", template: "plang"));

        await path.Value();

        await Assert.That(path.Error?.Key).IsEqualTo("SchemeNotRegistered");
    }

    // The wire marker can't be a dict key: the write answers ReservedKey.
    [Test] public async Task TheWireMarkerAsADictKey_IsReservedKey()
    {
        var d = new global::app.data.@this("d", new global::app.type.item.dict.@this(), context: Ctx);

        var written = await d.Set("@schema", false, 1);

        await Assert.That(written.Error?.Key).IsEqualTo("ReservedKey");
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
