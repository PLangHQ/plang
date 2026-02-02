using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Runtime.Actors;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.Text;

namespace PLang.Tests.Modules;

public class OutputModuleTests
{
	[Test]
	public async Task Actor_DefaultContentType_IsCorrect()
	{
		var systemActor = new SystemActor();
		var userActor = new UserActor();
		var serviceActor = new ServiceActor();

		await Assert.That(systemActor.ContentType).IsEqualTo("text/plain");
		// UserActor defaults to text/html for web browsers
		// Use application/x-ndjson only when client explicitly requests application/plang
		await Assert.That(userActor.ContentType).IsEqualTo("text/html");
		await Assert.That(serviceActor.ContentType).IsEqualTo("application/json");
	}

	[Test]
	public async Task Actor_DefaultTrust_IsCorrect()
	{
		var systemActor = new SystemActor();
		var userActor = new UserActor();
		var trustedUserActor = new UserActor(isTrusted: true);

		await Assert.That(systemActor.IsTrusted).IsTrue();
		await Assert.That(userActor.IsTrusted).IsFalse();
		await Assert.That(trustedUserActor.IsTrusted).IsTrue();
	}

	[Test]
	public async Task Actor_RegisterChannel_CreatesChannel()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();

		actor.RegisterChannel("custom", sink, "text/html");

		var channel = actor.GetChannel("custom");
		await Assert.That(channel).IsNotNull();
		await Assert.That(channel!.Name).IsEqualTo("custom");
		await Assert.That(channel.ContentType).IsEqualTo("text/html");
		await Assert.That(channel.Sink).IsEqualTo(sink);
	}

	[Test]
	public async Task Actor_GetOrCreateChannel_CreatesIfNotExists()
	{
		var actor = new UserActor();

		var channel1 = actor.GetOrCreateChannel("newchannel");
		var channel2 = actor.GetOrCreateChannel("newchannel");

		await Assert.That(channel1).IsNotNull();
		await Assert.That(channel1).IsEqualTo(channel2);
	}

	[Test]
	public async Task Actor_RegisterChannelHandler_SetsGoalHandler()
	{
		var actor = new UserActor();
		var goalHandler = new GoalToCallInfo("!TestHandler");

		actor.RegisterChannelHandler("log", goalHandler);

		var handler = actor.GetChannelHandler("log");
		await Assert.That(handler).IsNotNull();
		// GoalToCallInfo strips the "!" from the name
		await Assert.That(handler!.Name).IsEqualTo("TestHandler");
	}

	[Test]
	public async Task Actor_GetChannelHandler_ReturnsNullForUnregistered()
	{
		var actor = new UserActor();

		var handler = actor.GetChannelHandler("unregistered");

		await Assert.That(handler).IsNull();
	}

	[Test]
	public async Task ActorChannel_SetScopedContentType_OverridesDefault()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink, "text/plain");
		var goal = new Goal { GoalName = "TestGoal" };
		var callStack = new CallStack();
		callStack.EnterGoal(goal);

		channel.SetScopedContentType(goal, "application/json");

		var effectiveType = channel.GetEffectiveContentType(callStack, "text/plain");
		await Assert.That(effectiveType).IsEqualTo("application/json");
	}

	[Test]
	public async Task ActorChannel_ClearScopedSettings_RemovesOverride()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink, "text/plain");
		var goal = new Goal { GoalName = "TestGoal" };
		var callStack = new CallStack();
		callStack.EnterGoal(goal);

		channel.SetScopedContentType(goal, "application/json");
		channel.ClearScopedSettings(goal);

		var effectiveType = channel.GetEffectiveContentType(callStack, "text/plain");
		await Assert.That(effectiveType).IsEqualTo("text/plain");
	}

	[Test]
	public async Task ActorChannel_SetScopedEncoding_OverridesDefault()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink);
		var goal = new Goal { GoalName = "TestGoal" };
		var callStack = new CallStack();
		callStack.EnterGoal(goal);

		channel.SetScopedEncoding(goal, Encoding.UTF32);

		var effectiveEncoding = channel.GetEffectiveEncoding(callStack, Encoding.UTF8);
		await Assert.That(effectiveEncoding).IsEqualTo(Encoding.UTF32);
	}
}

public class PLangContextActorTests
{
	private IEngine _engine;
	private MemoryStack _memoryStack;
	private IPLangContextAccessor _contextAccessor;

	[Before(Test)]
	public void Setup()
	{
		_engine = Substitute.For<IEngine>();
		_engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		_engine.UserSink.Returns(Substitute.For<IOutputSink>());
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(Substitute.For<LightInject.IServiceContainer>(), Substitute.For<IPLangContextAccessor>()));

		// Create real MemoryStack by mocking its dependencies
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		_contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settings, variableHelper, _contextAccessor);
	}

	[Test]
	public async Task PLangContext_InitializesActors()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);

		await Assert.That(context.SystemActor).IsNotNull();
		await Assert.That(context.UserActor).IsNotNull();
		await Assert.That(context.SystemActor!.Type).IsEqualTo(ActorType.System);
		await Assert.That(context.UserActor!.Type).IsEqualTo(ActorType.User);
	}

	[Test]
	public async Task PLangContext_GetActor_ReturnsCorrectActor()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);

		var userActor = context.GetActor("user");
		var systemActor = context.GetActor("system");
		var defaultActor = context.GetActor(null);

		await Assert.That(userActor).IsEqualTo(context.UserActor);
		await Assert.That(systemActor).IsEqualTo(context.SystemActor);
		await Assert.That(defaultActor).IsEqualTo(context.UserActor);
	}

	[Test]
	public async Task PLangContext_RegisterChannelHandler_Works()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var goalHandler = new GoalToCallInfo("!MyHandler");

		context.RegisterChannelHandler("user", "custom", goalHandler);

		var handler = context.GetChannelHandler("user", "custom");
		await Assert.That(handler).IsNotNull();
		// GoalToCallInfo strips the "!" from the name
		await Assert.That(handler!.Name).IsEqualTo("MyHandler");
	}

	[Test]
	public async Task PLangContext_ConfigureOutput_SetsContentType()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });

		context.ConfigureOutput("user", "default", "application/json", null);

		var contentType = context.GetEffectiveContentType("user", "default");
		await Assert.That(contentType).IsEqualTo("application/json");
	}

	[Test]
	public async Task PLangContext_ConfigureOutput_WithGoalScope_ClearsOnExit()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var goal = new Goal { GoalName = "ScopedGoal" };
		context.CallStack.EnterGoal(goal);

		context.ConfigureOutput("user", "default", "application/json", goal);
		var beforeClear = context.GetEffectiveContentType("user", "default");

		context.ClearGoalScopedSettings(goal);
		var afterClear = context.GetEffectiveContentType("user", "default");

		await Assert.That(beforeClear).IsEqualTo("application/json");
		await Assert.That(afterClear).IsNotEqualTo("application/json");
	}

	[Test]
	public async Task PLangContext_ConfigureOutputEncoding_SetsEncoding()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });

		context.ConfigureOutputEncoding("user", "default", Encoding.UTF32, null);

		var encoding = context.GetEffectiveEncoding("user", "default");
		await Assert.That(encoding).IsEqualTo(Encoding.UTF32);
	}

	[Test]
	public async Task PLangContext_ConsoleMode_UserIsTrusted()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);

		await Assert.That(context.UserActor!.IsTrusted).IsTrue();
	}

	[Test]
	public async Task PLangContext_HttpMode_UserIsNotTrusted()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.HttpRequest);

		await Assert.That(context.UserActor!.IsTrusted).IsFalse();
	}
}
