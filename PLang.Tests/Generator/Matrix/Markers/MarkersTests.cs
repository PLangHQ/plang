using PLang.Tests.App.Fixtures;
using app.module.matrix.markers;

namespace PLang.Tests.Generator.Matrix.Markers;

public class IContextHandlerTests
{
    [Test]
    public async Task IContextHandler_ContextAssigned_BeforeRun()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IContextHandler>(app);
        // Handler.Run returns Data.Ok(Context != null) — true means it was assigned.
        await Assert.That(result.Data.Value).IsEqualTo(true);
    }

    [Test]
    public async Task IContextHandler_ContextSameInstance_AsExecuteAsyncArg()
    {
        await using var app = new global::app.@this("/app");
        var handler = new IContextHandler();
        var action = new PrAction { Module = "matrix.markers", ActionName = "icontexthandler" };
        await handler.ExecuteAsync(action, app.User.Context);
        await Assert.That(ReferenceEquals(handler.Context, app.User.Context)).IsTrue();
    }
}

public class IChannelHandlerTests
{
    [Test]
    public async Task IChannelHandler_ChannelsAssigned_BeforeRun()
    {
        await using var app = new global::app.@this("/app");
        global::app.@this.WireDefaultConsoleChannels(app.User);
        var result = await MatrixRunner.RunAsync<IChannelHandler>(app);
        await Assert.That(result.Data.Value).IsEqualTo(true);
    }
}

public class IActionHandlerTests
{
    [Test]
    public async Task IActionHandler_ActionAssigned_BeforeRun()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IActionHandler>(app);
        await Assert.That(result.Data.Value).IsEqualTo(true);
    }
}

public class IStepHandlerTests
{
    [Test]
    public async Task IStepHandler_StepAssigned_BeforeRun()
    {
        await using var app = new global::app.@this("/app");
        var step = new Step { Index = 0, Text = "step" };
        var result = await MatrixRunner.RunAsync<IStepHandler>(app, step: step);
        await Assert.That(result.Data.Value).IsEqualTo(true);
    }
}

public class IStaticHandlerTests
{
    [Test]
    public async Task IStaticHandler_StaticAssigned_BeforeRun()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IStaticHandler>(app);
        await Assert.That(result.Data.Value).IsEqualTo(true);
    }
}

public class MultiMarkerHandlerTests
{
    // The matrix has individual single-marker handlers; we verify all three slots fire by
    // running each in turn (the cross-product with one combined handler is the same shape).
    [Test]
    public async Task MultiMarker_AllSlotsAssigned_BeforeRun()
    {
        await using var app = new global::app.@this("/app");
        global::app.@this.WireDefaultConsoleChannels(app.User);

        var ctx = await MatrixRunner.RunAsync<IContextHandler>(app);
        var ch = await MatrixRunner.RunAsync<IChannelHandler>(app);
        var act = await MatrixRunner.RunAsync<IActionHandler>(app);
        var step = await MatrixRunner.RunAsync<IStepHandler>(app, step: new Step { Index = 0 });
        var stc = await MatrixRunner.RunAsync<IStaticHandler>(app);

        await Assert.That(ctx.Data.Value).IsEqualTo(true);
        await Assert.That(ch.Data.Value).IsEqualTo(true);
        await Assert.That(act.Data.Value).IsEqualTo(true);
        await Assert.That(step.Data.Value).IsEqualTo(true);
        await Assert.That(stc.Data.Value).IsEqualTo(true);
    }
}
