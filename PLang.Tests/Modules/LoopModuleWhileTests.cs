using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.LoopModule;
using PLang.Runtime;
using static PLang.Modules.ConditionalModule.ConditionEvaluator;

namespace PLang.Tests.Modules;

public class LoopModuleWhileTests
{
	[Test]
	public async Task While_ConditionFalseInitially_DoesNotCallGoal()
	{
		// Arrange
		var logger = Substitute.For<ILogger>();
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var engine = Substitute.For<IEngine>();

		var program = new Program(logger, pseudoRuntime, engine);

		// Condition: 10 < 5 is false
		var condition = new SimpleCondition
		{
			Kind = ConditionKind.Simple,
			LeftValue = 10,
			Operator = "<",
			RightValue = 5
		};
		var goalToCall = new GoalToCallInfo("TestGoal");

		// Act
		var result = await program.While(condition, goalToCall);

		// Assert - should not call the goal since condition is false
		await Assert.That(result).IsNull();
		await pseudoRuntime.DidNotReceive().RunGoal(
			Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(),
			Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal>(),
			Arg.Any<int>(), Arg.Any<RuntimeEvent?>());
	}

	[Test]
	public async Task While_ExceedsMaxIterations_ReturnsError()
	{
		// Arrange
		var logger = Substitute.For<ILogger>();
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var engine = Substitute.For<IEngine>();

		var program = new Program(logger, pseudoRuntime, engine);

		// Condition: true == true is always true (infinite loop)
		var condition = new SimpleCondition
		{
			Kind = ConditionKind.Simple,
			LeftValue = true,
			Operator = "==",
			RightValue = true
		};
		var goalToCall = new GoalToCallInfo("InfiniteLoop");

		// Mock the goal run to return success each time
		pseudoRuntime.RunGoal(Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(),
			Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal>(),
			Arg.Any<int>(), Arg.Any<RuntimeEvent?>())
			.Returns(Task.FromResult<(IEngine, object?, IError?)>((engine, null, null)));

		// Act - use a small maxIterations for test speed
		var result = await program.While(condition, goalToCall, maxIterations: 5);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result!.Message).Contains("exceeded maximum iterations");
	}

	[Test]
	public async Task While_GoalReturnsError_StopsAndReturnsError()
	{
		// Arrange
		var logger = Substitute.For<ILogger>();
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var engine = Substitute.For<IEngine>();

		var program = new Program(logger, pseudoRuntime, engine);

		// Condition: true == true (would be infinite but we return error)
		var condition = new SimpleCondition
		{
			Kind = ConditionKind.Simple,
			LeftValue = true,
			Operator = "==",
			RightValue = true
		};
		var goalToCall = new GoalToCallInfo("ErrorGoal");
		var expectedError = new ProgramError("Test error");

		// Mock the goal run to return an error
		pseudoRuntime.RunGoal(Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(),
			Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal>(),
			Arg.Any<int>(), Arg.Any<RuntimeEvent?>())
			.Returns(Task.FromResult<(IEngine, object?, IError?)>((engine, null, expectedError)));

		// Act
		var result = await program.While(condition, goalToCall);

		// Assert
		await Assert.That(result).IsEqualTo(expectedError);
	}

	[Test]
	public async Task While_ConditionTrue_CallsGoal()
	{
		// Arrange
		var logger = Substitute.For<ILogger>();
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var engine = Substitute.For<IEngine>();

		var program = new Program(logger, pseudoRuntime, engine);

		// Condition: 1 < 2 is true, but we'll use maxIterations=1 to limit
		var condition = new SimpleCondition
		{
			Kind = ConditionKind.Simple,
			LeftValue = 1,
			Operator = "<",
			RightValue = 2
		};
		var goalToCall = new GoalToCallInfo("TestGoal");

		// Mock the goal run
		pseudoRuntime.RunGoal(Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(),
			Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal>(),
			Arg.Any<int>(), Arg.Any<RuntimeEvent?>())
			.Returns(Task.FromResult<(IEngine, object?, IError?)>((engine, null, null)));

		// Act - limit to 1 iteration to avoid infinite loop
		var result = await program.While(condition, goalToCall, maxIterations: 1);

		// Assert - should have called the goal and then hit max iterations
		await Assert.That(result).IsNotNull();
		await Assert.That(result!.Message).Contains("exceeded maximum iterations");
		await pseudoRuntime.Received(1).RunGoal(
			Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(),
			Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal>(),
			Arg.Any<int>(), Arg.Any<RuntimeEvent?>());
	}

	[Test]
	public async Task While_EqualityConditionFalse_DoesNotLoop()
	{
		// Arrange
		var logger = Substitute.For<ILogger>();
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var engine = Substitute.For<IEngine>();

		var program = new Program(logger, pseudoRuntime, engine);

		// Condition: 5 == 10 is false
		var condition = new SimpleCondition
		{
			Kind = ConditionKind.Simple,
			LeftValue = 5,
			Operator = "==",
			RightValue = 10
		};
		var goalToCall = new GoalToCallInfo("NeverCalled");

		// Act
		var result = await program.While(condition, goalToCall);

		// Assert
		await Assert.That(result).IsNull();
		await pseudoRuntime.DidNotReceive().RunGoal(
			Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(),
			Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal>(),
			Arg.Any<int>(), Arg.Any<RuntimeEvent?>());
	}
}
