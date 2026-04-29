namespace PLang.Tests.Generator.Matrix.Markers;

// Matrix entries for marker interfaces — IContext, IChannel, IAction, IStep, IStatic.
// v4 contract: each marker triggers a corresponding generated marker-init in ExecuteAsync
//   (e.g., IContext sets Context = context; IChannel sets Channels = context.App.Channels; etc.).
// Markers are additive — a handler may implement several.

public class IContextHandlerTests
{
    // Handler implements IContext → generated ExecuteAsync sets the Context property to the passed context.
    [Test] public async Task IContextHandler_ContextAssigned_BeforeRun() => Assert.Fail("Not implemented");

    // Context property is non-null and references the same Context.@this passed to ExecuteAsync.
    [Test] public async Task IContextHandler_ContextSameInstance_AsExecuteAsyncArg() => Assert.Fail("Not implemented");
}

public class IChannelHandlerTests
{
    // Handler implements IChannel → generated ExecuteAsync sets Channels from context.App.Channels.
    [Test] public async Task IChannelHandler_ChannelsAssigned_BeforeRun() => Assert.Fail("Not implemented");
}

public class IActionHandlerTests
{
    // Handler implements IAction → generated ExecuteAsync sets Action property to the passed action.
    [Test] public async Task IActionHandler_ActionAssigned_BeforeRun() => Assert.Fail("Not implemented");
}

public class IStepHandlerTests
{
    // Handler implements IStep → generated ExecuteAsync sets Step from action.Step.
    [Test] public async Task IStepHandler_StepAssigned_BeforeRun() => Assert.Fail("Not implemented");
}

public class IStaticHandlerTests
{
    // Handler implements IStatic → generated ExecuteAsync wires the static lifecycle slot.
    [Test] public async Task IStaticHandler_StaticAssigned_BeforeRun() => Assert.Fail("Not implemented");
}

public class MultiMarkerHandlerTests
{
    // Handler implements IContext + IChannel + IStep → all three slots wired before Run().
    [Test] public async Task MultiMarker_AllSlotsAssigned_BeforeRun() => Assert.Fail("Not implemented");
}
