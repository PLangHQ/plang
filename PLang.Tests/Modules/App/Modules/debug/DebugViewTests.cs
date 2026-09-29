namespace PLang.Tests.App.Modules.debug;

// The Debug view writes what a value holds, never what points back at it: an action names its module,
// a module doesn't carry every module — so a goal writes, however large the app.
public class DebugViewTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/debugview-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Test]
    public async Task AGoalHoldingAnAction_Writes_NamingItsModule()
    {
        var goal = Make.Goal(Ctx, "Start", "/Start.goal",
            Make.Step("read a.txt", Ctx.Action("file.read(Path=\"a.txt\")")));

        var written = await goal.Debug(Ctx);

        await Assert.That(written).Contains("\"module\":\"file\"");
        await Assert.That(written).Contains("\"name\":\"read\"");
    }

    // A parameter is program text: the dump shows it as written and never runs it against live variables.
    [Test]
    public async Task AParameterNamingAnUnsetVariable_IsShownAsWritten()
    {
        var goal = Make.Goal(Ctx, "Start", "/Start.goal",
            Make.Step("assert %refused% is true", Ctx.Action("assert.isTrue(Value=%refused%)")));

        var written = await goal.Debug(Ctx);

        await Assert.That(written).Contains("%refused%");
    }

    // A value a parameter holds keeps its Debug mask: a sensitive member is never written.
    [Test]
    public async Task ASensitiveValueInAParameter_StaysMasked()
    {
        var action = Ctx.Action("file.read(Path=\"a.txt\")");
        action.Property.Set(new global::app.type.property.@this
        {
            Name = "Identity",
            Type = Ctx.App.type.list["identity"],
            Value = new global::app.module.identity.Identity("k") { PrivateKey = "the-private-key" },
        });
        var goal = Make.Goal(Ctx, "Start", "/Start.goal", Make.Step("read a.txt", action));

        var written = await goal.Debug(Ctx);

        await Assert.That(written).DoesNotContain("the-private-key");
        await Assert.That(written).Contains("****");
    }

    [Test]
    public async Task TheModuleList_StillNavigates()
    {
        var modules = await (await new global::app.type.item.variable.@this("!app.module.list").Start(Ctx)).Value();
        await Assert.That(modules).IsAssignableTo<global::app.type.item.list.@this>();
        await Assert.That(((global::app.type.item.list.@this)modules!).Items(Ctx).Any()).IsTrue();

        var file = await (await new global::app.type.item.variable.@this("!app.module.file").Start(Ctx)).Value();
        await Assert.That(file).IsTypeOf<global::app.module.@this>();
    }

    // A dump shows a step's line and its warnings — [Debug] members.
    [Test]
    public async Task AGoalDump_ShowsAStepsLineAndWarning()
    {
        var goal = Make.Goal(Ctx, "Start", "/Start.goal",
            Make.Step("read a.txt", Ctx.Action("file.read(Path=\"a.txt\")")));
        goal.Step[0].Line = new() { Number = 7, Indent = 1 };
        goal.Step[0].Warning.Add(new global::app.warning.@this { Key = "Unsure", Message = "maybe file.read" });

        var written = await goal.Debug(Ctx);

        await Assert.That(written).Contains("\"number\":7");
        await Assert.That(written).Contains("maybe file.read");
    }

    // A runtime structure that declares no face is written by name in a dump, never walked:
    // a channel's actor and its channel list.
    [Test]
    public async Task AChannelDump_NamesItsActorAndChannels_NeverWalksThem()
    {
        var channel = _app.actor.list.User.Channel[global::app.channel.list.@this.Output];

        var written = await channel.Debug(Ctx);

        await Assert.That(written).Contains("\"name\":\"output\"");
        await Assert.That(written).Contains("\"actor\":\"");
        await Assert.That(written).Contains("\"channels\":\"");
    }

    // A binding (an event's runtime structure) dumps as its name, never walking its event or actor.
    [Test]
    public async Task ABindingDump_IsItsName()
    {
        var binding = _app.type.list["action"].Own().Bind("start", global::app.@event.When.before,
            (_, _, c) => System.Threading.Tasks.Task.FromResult(c.Ok()), _app.actor.list.User, global::app.@event.binding.Scope.actor);

        var written = await binding.Debug(Ctx);

        await Assert.That(written).StartsWith("\"");
        await Assert.That(written).DoesNotContain("{");
    }

    // On the wire a type that declares no face still refuses: nothing it holds leaks out.
    [Test]
    public async Task TheOutView_OfATypeWithNoWireFace_StillRefuses()
    {
        var channel = _app.actor.list.User.Channel[global::app.channel.list.@this.Output];
        using var ms = new System.IO.MemoryStream();

        var written = await Ctx.Format("application/json").Encode(ms, new global::app.data.@this("c", channel, context: Ctx), Ctx);

        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("NoWireContract");
    }
}
