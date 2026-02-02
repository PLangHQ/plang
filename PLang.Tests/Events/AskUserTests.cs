using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Events.Types;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;

namespace PLang.Tests.Events;

public class AskUserTests
{
	private IEngine _engine;
	private MemoryStack _memoryStack;
	private IPLangContextAccessor _contextAccessor;
	private PLangContext _context;

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
		_context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);

		// Setup PrParser mock
		_engine.PrParser.Returns(Substitute.For<IPrParser>());
	}

	/// <summary>
	/// This test validates the bug where ProcessGoalResult looks for Return in Error field,
	/// but Engine actually puts ReturnVariables in the Result field.
	///
	/// The AskSystem goal does:
	///   - ask "%__plang_question%", write to %__plang_answer%
	///   - return %__plang_answer%
	///
	/// When "return" executes, Engine returns (ReturnVariables, null) - value in Result, not Error.
	/// ProcessGoalResult was incorrectly checking goalResult.Error for the Return.
	/// </summary>
	[Test]
	public async Task GetAnswer_WhenGoalReturnsValue_ExtractsAnswerFromResult()
	{
		// Arrange
		var askSystemGoal = new Goal { GoalName = "AskSystem" };
		_engine.PrParser.GetEvent("AskSystem").Returns(askSystemGoal);
		_engine.PrParser.GetSystemEvent("AskSystem").Returns(askSystemGoal);

		// This is what Engine.RunGoalInternal returns when a goal has "return %var%":
		// - ReturnVariables are extracted from the Return error and put in Result
		// - Error is set to null
		var returnVariables = new List<ObjectValue>
		{
			new ObjectValue("__plang_answer", "user-provided-value")
		};

		_engine.RunGoal(askSystemGoal, _context)
			.Returns(Task.FromResult<(object?, IError?)>((returnVariables, null)));

		// Act
		var (answer, error) = await AskUser.GetAnswer(_engine, _context, "Enter your API key:");

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(answer).IsNotNull();
		await Assert.That(answer).IsEqualTo("user-provided-value");
	}

	[Test]
	public async Task GetAnswer_WhenGoalReturnsEmptyList_ReturnsNull()
	{
		// Arrange
		var askSystemGoal = new Goal { GoalName = "AskSystem" };
		_engine.PrParser.GetEvent("AskSystem").Returns(askSystemGoal);

		// Empty return variables
		var returnVariables = new List<ObjectValue>();

		_engine.RunGoal(askSystemGoal, _context)
			.Returns(Task.FromResult<(object?, IError?)>((returnVariables, null)));

		// Act
		var (answer, error) = await AskUser.GetAnswer(_engine, _context, "Enter value:");

		// Assert - should handle gracefully
		await Assert.That(answer).IsNull();
	}

	[Test]
	public async Task GetAnswer_WhenGoalReturnsError_PropagatesError()
	{
		// Arrange
		var askSystemGoal = new Goal { GoalName = "AskSystem" };
		_engine.PrParser.GetEvent("AskSystem").Returns(askSystemGoal);

		var expectedError = new Error("Something went wrong");

		_engine.RunGoal(askSystemGoal, _context)
			.Returns(Task.FromResult<(object?, IError?)>((null, expectedError)));

		// Act
		var (answer, error) = await AskUser.GetAnswer(_engine, _context, "Enter value:");

		// Assert
		await Assert.That(answer).IsNull();
		await Assert.That(error).IsNotNull();
		await Assert.That(error!.Message).IsEqualTo("Something went wrong");
	}
}
