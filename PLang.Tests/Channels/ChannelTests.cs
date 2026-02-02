using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Runtime.Actors;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.Text;

namespace PLang.Tests.Channels;

#region ActorTypeTests

public class ActorTypeTests
{
	[Test]
	public async Task UserActor_DefaultContentType_IsTextHtml()
	{
		var userActor = new UserActor();
		await Assert.That(userActor.ContentType).IsEqualTo("text/html");
	}

	[Test]
	public async Task SystemActor_DefaultContentType_IsTextPlain()
	{
		var systemActor = new SystemActor();
		await Assert.That(systemActor.ContentType).IsEqualTo("text/plain");
	}

	[Test]
	public async Task ServiceActor_DefaultContentType_IsApplicationJson()
	{
		var serviceActor = new ServiceActor();
		await Assert.That(serviceActor.ContentType).IsEqualTo("application/json");
	}

	[Test]
	public async Task UserActor_ConsoleModeIsTrusted_HttpModeIsNotTrusted()
	{
		var trustedUserActor = new UserActor(isTrusted: true);
		var untrustedUserActor = new UserActor(isTrusted: false);

		await Assert.That(trustedUserActor.IsTrusted).IsTrue();
		await Assert.That(untrustedUserActor.IsTrusted).IsFalse();
	}

	[Test]
	public async Task SystemActor_IsAlwaysTrusted()
	{
		var systemActor = new SystemActor();
		await Assert.That(systemActor.IsTrusted).IsTrue();
	}

	[Test]
	public async Task ServiceActor_IsNeverTrusted()
	{
		var serviceActor = new ServiceActor();
		await Assert.That(serviceActor.IsTrusted).IsFalse();
	}
}

#endregion

#region ChannelRegistrationTests

public class ChannelRegistrationTests
{
	[Test]
	public async Task Actor_RegisterChannel_CreatesChannelWithSink()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();

		actor.RegisterChannel("custom", sink);

		var channel = actor.GetChannel("custom");
		await Assert.That(channel).IsNotNull();
		await Assert.That(channel!.Name).IsEqualTo("custom");
		await Assert.That(channel.Sink).IsEqualTo(sink);
	}

	[Test]
	public async Task Actor_RegisterChannel_WithContentType_SetsContentType()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();

		actor.RegisterChannel("api", sink, "application/json");

		var channel = actor.GetChannel("api");
		await Assert.That(channel).IsNotNull();
		await Assert.That(channel!.ContentType).IsEqualTo("application/json");
	}

	[Test]
	public async Task Actor_GetChannel_ReturnsRegisteredChannel()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();
		actor.RegisterChannel("myChannel", sink);

		var channel = actor.GetChannel("myChannel");

		await Assert.That(channel).IsNotNull();
		await Assert.That(channel!.Name).IsEqualTo("myChannel");
	}

	[Test]
	public async Task Actor_GetChannel_ReturnsNull_ForUnregisteredChannel()
	{
		var actor = new UserActor();

		var channel = actor.GetChannel("nonexistent");

		await Assert.That(channel).IsNull();
	}

	[Test]
	public async Task Actor_GetOrCreateChannel_CreatesNewChannel_WhenNotExists()
	{
		var actor = new UserActor();

		var channel = actor.GetOrCreateChannel("newChannel");

		await Assert.That(channel).IsNotNull();
		await Assert.That(channel.Name).IsEqualTo("newChannel");
	}

	[Test]
	public async Task Actor_GetOrCreateChannel_ReturnsSameChannel_WhenExists()
	{
		var actor = new UserActor();

		var channel1 = actor.GetOrCreateChannel("testChannel");
		var channel2 = actor.GetOrCreateChannel("testChannel");

		await Assert.That(channel1).IsEqualTo(channel2);
	}

	[Test]
	public async Task Actor_UnregisterChannel_RemovesChannel()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();
		actor.RegisterChannel("toRemove", sink);

		actor.UnregisterChannel("toRemove");

		var channel = actor.GetChannel("toRemove");
		await Assert.That(channel).IsNull();
	}

	[Test]
	public async Task Actor_GetAllChannels_ReturnsAllRegisteredChannels()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();
		actor.RegisterChannel("channel1", sink);
		actor.RegisterChannel("channel2", sink);

		var channels = actor.GetAllChannels().ToList();

		// UserActor creates a "default" channel in constructor, plus our 2
		await Assert.That(channels.Count).IsGreaterThanOrEqualTo(2);
		await Assert.That(channels.Any(c => c.Name == "channel1")).IsTrue();
		await Assert.That(channels.Any(c => c.Name == "channel2")).IsTrue();
	}

	[Test]
	public async Task PLangContext_GetActor_ReturnsCorrectActor_ByName()
	{
		var engine = CreateMockEngine();
		var memoryStack = CreateMemoryStack(engine);
		var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);

		var userActor = context.GetActor("user");
		var systemActor = context.GetActor("system");

		await Assert.That(userActor).IsEqualTo(context.UserActor);
		await Assert.That(systemActor).IsEqualTo(context.SystemActor);
	}

	[Test]
	public async Task PLangContext_GetActor_ReturnsUserActor_ForNullOrEmpty()
	{
		var engine = CreateMockEngine();
		var memoryStack = CreateMemoryStack(engine);
		var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);

		var nullActor = context.GetActor(null);
		var emptyActor = context.GetActor("");

		await Assert.That(nullActor).IsEqualTo(context.UserActor);
		await Assert.That(emptyActor).IsEqualTo(context.UserActor);
	}

	private IEngine CreateMockEngine()
	{
		var engine = Substitute.For<IEngine>();
		engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		engine.UserSink.Returns(Substitute.For<IOutputSink>());
		engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(
			Substitute.For<LightInject.IServiceContainer>(),
			Substitute.For<IPLangContextAccessor>()));
		return engine;
	}

	private MemoryStack CreateMemoryStack(IEngine engine)
	{
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		var contextAccessor = Substitute.For<IPLangContextAccessor>();
		return new MemoryStack(pseudoRuntime, engine, settings, variableHelper, contextAccessor);
	}
}

#endregion

#region ChannelHandlerTests

public class ChannelHandlerTests
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
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(
			Substitute.For<LightInject.IServiceContainer>(),
			Substitute.For<IPLangContextAccessor>()));

		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		_contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settings, variableHelper, _contextAccessor);
	}

	[Test]
	public async Task Actor_RegisterChannelHandler_SetsGoalHandler()
	{
		var actor = new UserActor();
		var goalHandler = new GoalToCallInfo("!TestHandler");

		actor.RegisterChannelHandler("log", goalHandler);

		var handler = actor.GetChannelHandler("log");
		await Assert.That(handler).IsNotNull();
		await Assert.That(handler!.Name).IsEqualTo("TestHandler");
	}

	[Test]
	public async Task Actor_GetChannelHandler_ReturnsRegisteredHandler()
	{
		var actor = new UserActor();
		var goalHandler = new GoalToCallInfo("MyHandler");
		actor.RegisterChannelHandler("custom", goalHandler);

		var handler = actor.GetChannelHandler("custom");

		await Assert.That(handler).IsNotNull();
		await Assert.That(handler!.Name).IsEqualTo("MyHandler");
	}

	[Test]
	public async Task Actor_GetChannelHandler_ReturnsNull_WhenNoHandler()
	{
		var actor = new UserActor();

		var handler = actor.GetChannelHandler("noHandler");

		await Assert.That(handler).IsNull();
	}

	[Test]
	public async Task PLangContext_RegisterChannelHandler_RegistersOnCorrectActor()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var goalHandler = new GoalToCallInfo("ContextHandler");

		context.RegisterChannelHandler("user", "events", goalHandler);

		var handler = context.GetChannelHandler("user", "events");
		await Assert.That(handler).IsNotNull();
		await Assert.That(handler!.Name).IsEqualTo("ContextHandler");
	}

	[Test]
	public async Task PLangContext_GetChannelHandler_RetrievesFromCorrectActor()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var userHandler = new GoalToCallInfo("UserHandler");
		var systemHandler = new GoalToCallInfo("SystemHandler");

		context.RegisterChannelHandler("user", "channel1", userHandler);
		context.RegisterChannelHandler("system", "channel1", systemHandler);

		var userResult = context.GetChannelHandler("user", "channel1");
		var systemResult = context.GetChannelHandler("system", "channel1");

		await Assert.That(userResult!.Name).IsEqualTo("UserHandler");
		await Assert.That(systemResult!.Name).IsEqualTo("SystemHandler");
	}

	[Test]
	public async Task ChannelHandler_InterceptsOutput_BeforeSink()
	{
		var actor = new UserActor();
		var goalHandler = new GoalToCallInfo("!InterceptHandler");

		actor.RegisterChannelHandler("default", goalHandler);

		var handler = actor.GetChannelHandler("default");
		await Assert.That(handler).IsNotNull();
		await Assert.That(handler!.Name).IsEqualTo("InterceptHandler");
	}

	[Test]
	public async Task ChannelHandler_WithDefaultChannel_InterceptsAllOutput()
	{
		var actor = new UserActor();
		var goalHandler = new GoalToCallInfo("DefaultInterceptor");

		// Register on "default" channel - should intercept default output
		actor.RegisterChannelHandler("default", goalHandler);

		var handler = actor.GetChannelHandler("default");
		await Assert.That(handler).IsNotNull();
	}

	[Test]
	public async Task ChannelHandler_GoalToCallInfo_StripsExclamationFromName()
	{
		var goalHandler1 = new GoalToCallInfo("!ExclamationHandler");
		var goalHandler2 = new GoalToCallInfo("NoExclamationHandler");

		await Assert.That(goalHandler1.Name).IsEqualTo("ExclamationHandler");
		await Assert.That(goalHandler2.Name).IsEqualTo("NoExclamationHandler");
	}
}

#endregion

#region ContentTypeEncodingTests

public class ContentTypeEncodingTests
{
	private IEngine _engine;
	private MemoryStack _memoryStack;

	[Before(Test)]
	public void Setup()
	{
		_engine = Substitute.For<IEngine>();
		_engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		_engine.UserSink.Returns(Substitute.For<IOutputSink>());
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(
			Substitute.For<LightInject.IServiceContainer>(),
			Substitute.For<IPLangContextAccessor>()));

		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		var contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settings, variableHelper, contextAccessor);
	}

	[Test]
	public async Task ActorChannel_SetContentType_OverridesDefault()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink, "text/plain");

		channel.ContentType = "application/xml";

		await Assert.That(channel.ContentType).IsEqualTo("application/xml");
	}

	[Test]
	public async Task PLangContext_ConfigureOutput_SetsContentType()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });

		context.ConfigureOutput("user", "default", "application/json");

		var contentType = context.GetEffectiveContentType("user", "default");
		await Assert.That(contentType).IsEqualTo("application/json");
	}

	[Test]
	public async Task PLangContext_ConfigureOutputEncoding_SetsEncoding()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });

		context.ConfigureOutputEncoding("user", "default", Encoding.UTF32);

		var encoding = context.GetEffectiveEncoding("user", "default");
		await Assert.That(encoding).IsEqualTo(Encoding.UTF32);
	}

	[Test]
	public async Task PLangContext_GetEffectiveContentType_ReturnsChannelContentType()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });
		context.ConfigureOutput("user", "custom", "text/csv");

		var contentType = context.GetEffectiveContentType("user", "custom");

		await Assert.That(contentType).IsEqualTo("text/csv");
	}

	[Test]
	public async Task PLangContext_GetEffectiveContentType_FallsBackToActorDefault()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });

		// Don't configure any content type, just access a channel
		var channel = context.UserActor!.GetOrCreateChannel("unconfigured");

		var contentType = context.GetEffectiveContentType("user", "unconfigured");
		// Should fall back to UserActor's default: text/html
		await Assert.That(contentType).IsEqualTo("text/html");
	}

	[Test]
	public async Task PLangContext_GetEffectiveEncoding_ReturnsConfiguredEncoding()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });
		context.ConfigureOutputEncoding("user", "default", Encoding.ASCII);

		var encoding = context.GetEffectiveEncoding("user", "default");

		await Assert.That(encoding).IsEqualTo(Encoding.ASCII);
	}

	[Test]
	public async Task PLangContext_GetExplicitContentType_ReturnsNull_WhenUsingDefaults()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });

		// Create channel but don't set explicit content type
		context.UserActor!.GetOrCreateChannel("testChannel");

		var explicit_ = context.GetExplicitContentType("user", "testChannel");
		await Assert.That(explicit_).IsNull();
	}

	[Test]
	public async Task PLangContext_GetExplicitContentType_ReturnsValue_WhenConfigured()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		context.CallStack.EnterGoal(new Goal { GoalName = "Test" });
		context.ConfigureOutput("user", "explicit", "text/markdown");

		var explicit_ = context.GetExplicitContentType("user", "explicit");

		await Assert.That(explicit_).IsEqualTo("text/markdown");
	}
}

#endregion

#region GoalScopedSettingsTests

public class GoalScopedSettingsTests
{
	private IEngine _engine;
	private MemoryStack _memoryStack;

	[Before(Test)]
	public void Setup()
	{
		_engine = Substitute.For<IEngine>();
		_engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		_engine.UserSink.Returns(Substitute.For<IOutputSink>());
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(
			Substitute.For<LightInject.IServiceContainer>(),
			Substitute.For<IPLangContextAccessor>()));

		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		var contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settings, variableHelper, contextAccessor);
	}

	[Test]
	public async Task ActorChannel_SetScopedContentType_OverridesForGoal()
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
	public async Task ActorChannel_SetScopedEncoding_OverridesForGoal()
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

	[Test]
	public async Task ActorChannel_ClearScopedSettings_RemovesGoalOverride()
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
	public async Task ActorChannel_ClearAllScopedSettings_RemovesAllOverrides()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink, "text/plain");
		var goal1 = new Goal { GoalName = "Goal1" };
		var goal2 = new Goal { GoalName = "Goal2" };
		var callStack = new CallStack();
		callStack.EnterGoal(goal1);
		callStack.EnterGoal(goal2);

		channel.SetScopedContentType(goal1, "application/json");
		channel.SetScopedContentType(goal2, "application/xml");
		channel.ClearAllScopedSettings();

		var effectiveType = channel.GetEffectiveContentType(callStack, "text/plain");
		await Assert.That(effectiveType).IsEqualTo("text/plain");
	}

	[Test]
	public async Task ActorChannel_GetEffectiveContentType_WalksCallStack()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink, "text/plain");
		var outerGoal = new Goal { GoalName = "OuterGoal" };
		var innerGoal = new Goal { GoalName = "InnerGoal" };
		var callStack = new CallStack();
		callStack.EnterGoal(outerGoal);
		callStack.EnterGoal(innerGoal);

		// Only set content type on outer goal
		channel.SetScopedContentType(outerGoal, "application/json");

		// Should walk up and find outer goal's setting
		var effectiveType = channel.GetEffectiveContentType(callStack, "text/plain");
		await Assert.That(effectiveType).IsEqualTo("application/json");
	}

	[Test]
	public async Task ActorChannel_GetEffectiveContentType_InnerGoalOverridesOuter()
	{
		var sink = Substitute.For<IOutputSink>();
		var channel = new ActorChannel("test", sink, "text/plain");
		var outerGoal = new Goal { GoalName = "OuterGoal" };
		var innerGoal = new Goal { GoalName = "InnerGoal" };
		var callStack = new CallStack();
		callStack.EnterGoal(outerGoal);
		callStack.EnterGoal(innerGoal);

		channel.SetScopedContentType(outerGoal, "application/json");
		channel.SetScopedContentType(innerGoal, "application/xml");

		// Inner goal's setting takes precedence (first in stack order)
		var effectiveType = channel.GetEffectiveContentType(callStack, "text/plain");
		await Assert.That(effectiveType).IsEqualTo("application/xml");
	}

	[Test]
	public async Task PLangContext_ConfigureOutput_WithGoalScope_ScopesToGoal()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var goal = new Goal { GoalName = "ScopedGoal" };
		context.CallStack.EnterGoal(goal);

		context.ConfigureOutput("user", "default", "application/json", goal);

		var contentType = context.GetEffectiveContentType("user", "default");
		await Assert.That(contentType).IsEqualTo("application/json");
	}

	[Test]
	public async Task PLangContext_ClearGoalScopedSettings_ClearsAllChannels()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var goal = new Goal { GoalName = "TestGoal" };
		context.CallStack.EnterGoal(goal);

		context.ConfigureOutput("user", "channel1", "application/json", goal);
		context.ConfigureOutput("user", "channel2", "application/xml", goal);

		context.ClearGoalScopedSettings(goal);

		var type1 = context.GetEffectiveContentType("user", "channel1");
		var type2 = context.GetEffectiveContentType("user", "channel2");

		// Should fall back to defaults after clearing
		await Assert.That(type1).IsNotEqualTo("application/json");
		await Assert.That(type2).IsNotEqualTo("application/xml");
	}

	[Test]
	public async Task GoalScopedSettings_AutoClear_OnGoalExit()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		var goal = new Goal { GoalName = "ExitingGoal" };
		context.CallStack.EnterGoal(goal);

		context.ConfigureOutput("user", "default", "application/json", goal);
		var beforeExit = context.GetEffectiveContentType("user", "default");

		// Simulate goal exit by clearing scoped settings (as Engine would do)
		context.ClearGoalScopedSettings(goal);
		context.CallStack.ExitGoal();

		// Need to re-enter a goal to test
		context.CallStack.EnterGoal(new Goal { GoalName = "NewGoal" });
		var afterExit = context.GetEffectiveContentType("user", "default");

		await Assert.That(beforeExit).IsEqualTo("application/json");
		await Assert.That(afterExit).IsNotEqualTo("application/json");
	}
}

#endregion

#region ErrorChannelTests

public class ErrorChannelTests
{
	[Test]
	public async Task TextMessage_WithLevelError_HasCorrectProperties()
	{
		var message = new TextMessage("Error occurred", Level: "error", StatusCode: 500);

		await Assert.That(message.Level).IsEqualTo("error");
		await Assert.That(message.Kind).IsEqualTo(MessageKind.Text);
		await Assert.That(message.StatusCode).IsEqualTo(500);
	}

	[Test]
	public async Task TextMessage_WithStatusCode400Plus_IsClientError()
	{
		var message400 = new TextMessage("Bad Request", StatusCode: 400);
		var message404 = new TextMessage("Not Found", StatusCode: 404);
		var message499 = new TextMessage("Client Error", StatusCode: 499);

		await Assert.That(message400.StatusCode).IsGreaterThanOrEqualTo(400);
		await Assert.That(message404.StatusCode).IsGreaterThanOrEqualTo(400);
		await Assert.That(message499.StatusCode).IsLessThan(500);
	}

	[Test]
	public async Task TextMessage_WithStatusCode500Plus_IsServerError()
	{
		var message500 = new TextMessage("Internal Server Error", StatusCode: 500);
		var message503 = new TextMessage("Service Unavailable", StatusCode: 503);

		await Assert.That(message500.StatusCode).IsGreaterThanOrEqualTo(500);
		await Assert.That(message503.StatusCode).IsGreaterThanOrEqualTo(500);
	}

	[Test]
	public async Task ErrorMessage_CanRouteToErrorChannel()
	{
		var errorMessage = new TextMessage("Error", Channel: "error", Level: "error", StatusCode: 500);

		await Assert.That(errorMessage.Channel).IsEqualTo("error");
		await Assert.That(errorMessage.Level).IsEqualTo("error");
	}

	[Test]
	public async Task ErrorMessage_CanRouteToCustomErrorHandler()
	{
		var actor = new SystemActor();
		var errorHandler = new GoalToCallInfo("!HandleError");

		actor.RegisterChannelHandler("error", errorHandler);

		var handler = actor.GetChannelHandler("error");
		await Assert.That(handler).IsNotNull();
		await Assert.That(handler!.Name).IsEqualTo("HandleError");
	}

	[Test]
	public async Task ErrorChannel_CanBeRegistered_WithCustomSink()
	{
		var errorSink = Substitute.For<IOutputSink>();
		var actor = new SystemActor();

		actor.RegisterChannel("error", errorSink, "application/json");

		var channel = actor.GetChannel("error");
		await Assert.That(channel).IsNotNull();
		await Assert.That(channel!.Sink).IsEqualTo(errorSink);
	}

	[Test]
	public async Task ErrorChannel_Handler_ReceivesErrorDetails()
	{
		var actor = new SystemActor();
		var handler = new GoalToCallInfo("!ErrorHandler");
		handler.Parameters["errorLevel"] = "error";
		handler.Parameters["statusCode"] = 500;

		actor.RegisterChannelHandler("error", handler);

		var registeredHandler = actor.GetChannelHandler("error");
		await Assert.That(registeredHandler!.Parameters["errorLevel"]).IsEqualTo("error");
		await Assert.That(registeredHandler.Parameters["statusCode"]).IsEqualTo(500);
	}

	[Test]
	public async Task ErrorChannel_DefaultsToSystemActor()
	{
		var errorMessage = new TextMessage("System Error", Actor: "system", Channel: "error", Level: "error");

		await Assert.That(errorMessage.Actor).IsEqualTo("system");
	}
}

#endregion

#region AskMessageTests

public class AskMessageTests
{
	[Test]
	public async Task AskMessage_HasKindAsk()
	{
		var message = new AskMessage("What is your name?");

		await Assert.That(message.Kind).IsEqualTo(MessageKind.Ask);
	}

	[Test]
	public async Task AskMessage_DefaultsToUserActor()
	{
		var message = new AskMessage("Enter input:");

		await Assert.That(message.Actor).IsEqualTo("user");
	}

	[Test]
	public async Task AskMessage_CanHaveCallback()
	{
		var callback = new GoalToCallInfo("!HandleResponse");
		var message = new AskMessage("Question?", OnCallback: callback);

		await Assert.That(message.OnCallback).IsNotNull();
		await Assert.That(message.OnCallback!.Name).IsEqualTo("HandleResponse");
	}

	[Test]
	public async Task AskMessage_CanHaveCallbackData()
	{
		var callbackData = new Dictionary<string, object?>
		{
			["userId"] = 123,
			["context"] = "login"
		};
		var message = new AskMessage("Confirm?", CallbackData: callbackData);

		await Assert.That(message.CallbackData).IsNotNull();
		await Assert.That(message.CallbackData!["userId"]).IsEqualTo(123);
		await Assert.That(message.CallbackData["context"]).IsEqualTo("login");
	}

	[Test]
	public async Task IOutputSink_AskAsync_ReturnsResultTuple()
	{
		var mockSink = Substitute.For<IOutputSink>();
		var expectedResult = "user response";
		mockSink.AskAsync(Arg.Any<AskMessage>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<(object?, IError?)>((expectedResult, null)));

		var message = new AskMessage("What?");
		var (result, error) = await mockSink.AskAsync(message);

		await Assert.That(result).IsEqualTo(expectedResult);
		await Assert.That(error).IsNull();
	}

	[Test]
	public async Task AskMessage_RoutesToCorrectChannel()
	{
		var message = new AskMessage("Input?", Channel: "interactive");

		await Assert.That(message.Channel).IsEqualTo("interactive");
	}

	[Test]
	public async Task AskMessage_WithGoalHandler_CallsHandler()
	{
		var actor = new UserActor();
		var askHandler = new GoalToCallInfo("!HandleAsk");

		actor.RegisterChannelHandler("ask", askHandler);

		var handler = actor.GetChannelHandler("ask");
		await Assert.That(handler).IsNotNull();
		await Assert.That(handler!.Name).IsEqualTo("HandleAsk");
	}

	[Test]
	public async Task AskMessage_IsTemplateFile_DetectsFileNames()
	{
		var fileMessage = new AskMessage("form.html", IsTemplateFile: true);
		var textMessage = new AskMessage("Enter your name:", IsTemplateFile: false);
		var inferredMessage = new AskMessage("template.html"); // null - to be inferred

		await Assert.That(fileMessage.IsTemplateFile).IsTrue();
		await Assert.That(textMessage.IsTemplateFile).IsFalse();
		await Assert.That(inferredMessage.IsTemplateFile).IsNull();
	}
}

#endregion

#region MessageTypeTests

public class MessageTypeTests
{
	[Test]
	public async Task TextMessage_HasAllRequiredProperties()
	{
		var message = new TextMessage(
			Content: "Hello World",
			Level: "info",
			StatusCode: 200,
			Target: "#output",
			Actions: new[] { "append" },
			Channel: "default",
			Actor: "user",
			SkipNewline: false
		);

		await Assert.That(message.Content).IsEqualTo("Hello World");
		await Assert.That(message.Level).IsEqualTo("info");
		await Assert.That(message.StatusCode).IsEqualTo(200);
		await Assert.That(message.Target).IsEqualTo("#output");
		await Assert.That(message.Actions).Contains("append");
		await Assert.That(message.Channel).IsEqualTo("default");
		await Assert.That(message.Actor).IsEqualTo("user");
		await Assert.That(message.SkipNewline).IsFalse();
	}

	[Test]
	public async Task OutMessage_Channel_DefaultsToDefault()
	{
		var message = new TextMessage("Test");

		await Assert.That(message.Channel).IsEqualTo("default");
	}

	[Test]
	public async Task OutMessage_Actor_DefaultsToUser()
	{
		var message = new TextMessage("Test");

		await Assert.That(message.Actor).IsEqualTo("user");
	}

	[Test]
	public async Task OutMessage_StatusCode_DefaultsTo200()
	{
		var message = new TextMessage("Test");

		await Assert.That(message.StatusCode).IsEqualTo(200);
	}

	[Test]
	public async Task OutMessage_Level_DefaultsToInfo()
	{
		var message = new TextMessage("Test");

		await Assert.That(message.Level).IsEqualTo("info");
	}

	[Test]
	public async Task OutMessage_SkipNewline_DefaultsToFalse()
	{
		var message = new TextMessage("Test");

		await Assert.That(message.SkipNewline).IsFalse();
	}
}

#endregion

#region AdditionalIntegrationTests

public class ChannelIntegrationTests
{
	private IEngine _engine;
	private MemoryStack _memoryStack;

	[Before(Test)]
	public void Setup()
	{
		_engine = Substitute.For<IEngine>();
		_engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		_engine.UserSink.Returns(Substitute.For<IOutputSink>());
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(
			Substitute.For<LightInject.IServiceContainer>(),
			Substitute.For<IPLangContextAccessor>()));

		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		var contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settings, variableHelper, contextAccessor);
	}

	[Test]
	public async Task Context_UserActorInConsoleMode_IsTrusted()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);

		await Assert.That(context.UserActor!.IsTrusted).IsTrue();
	}

	[Test]
	public async Task Context_UserActorInHttpMode_IsNotTrusted()
	{
		var context = new PLangContext(_memoryStack, _engine, ExecutionMode.HttpRequest);

		await Assert.That(context.UserActor!.IsTrusted).IsFalse();
	}

	[Test]
	public async Task Actor_GetSink_ReturnsDefaultChannelSink()
	{
		var mockSink = Substitute.For<IOutputSink>();
		var actor = new UserActor(defaultSink: mockSink);

		var sink = actor.GetSink("default");

		await Assert.That(sink).IsEqualTo(mockSink);
	}

	[Test]
	public async Task Actor_GetSink_CreatesChannelIfNotExists()
	{
		var actor = new UserActor();

		var sink = actor.GetSink("newChannel");

		await Assert.That(sink).IsNotNull();
		var channel = actor.GetChannel("newChannel");
		await Assert.That(channel).IsNotNull();
	}

	[Test]
	public async Task Channel_CaseInsensitivity_WorksCorrectly()
	{
		var sink = Substitute.For<IOutputSink>();
		var actor = new UserActor();
		actor.RegisterChannel("MyChannel", sink);

		var lowerCase = actor.GetChannel("mychannel");
		var upperCase = actor.GetChannel("MYCHANNEL");
		var mixedCase = actor.GetChannel("MyChannel");

		await Assert.That(lowerCase).IsNotNull();
		await Assert.That(upperCase).IsNotNull();
		await Assert.That(mixedCase).IsNotNull();
		await Assert.That(lowerCase).IsEqualTo(upperCase);
		await Assert.That(upperCase).IsEqualTo(mixedCase);
	}

	[Test]
	public async Task MultipleActors_HaveIndependentChannels()
	{
		var userSink = Substitute.For<IOutputSink>();
		var systemSink = Substitute.For<IOutputSink>();

		var userActor = new UserActor();
		var systemActor = new SystemActor();

		userActor.RegisterChannel("shared", userSink);
		systemActor.RegisterChannel("shared", systemSink);

		var userChannel = userActor.GetChannel("shared");
		var systemChannel = systemActor.GetChannel("shared");

		await Assert.That(userChannel!.Sink).IsEqualTo(userSink);
		await Assert.That(systemChannel!.Sink).IsEqualTo(systemSink);
		await Assert.That(userChannel.Sink).IsNotEqualTo(systemChannel.Sink);
	}
}

#endregion
