using NSubstitute;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using LightInject;
using Microsoft.Extensions.Logging;

namespace PLang.Tests.Runtime;

public class EngineTests
{
    private ServiceContainer _container = null!;
    private IEventRuntime _mockEventRuntime = null!;
    private ILogger _mockLogger = null!;
    private ISettings _mockSettings = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _mockEventRuntime = Substitute.For<IEventRuntime>();
        _mockLogger = Substitute.For<ILogger>();
        _mockSettings = Substitute.For<ISettings>();

        // Setup default event runtime behavior
        _mockEventRuntime.GetBeforeGoalEvents(Arg.Any<Goal>())
            .Returns(Task.FromResult(new List<EventBinding>()));
        _mockEventRuntime.GetAfterGoalEvents(Arg.Any<Goal>())
            .Returns(Task.FromResult(new List<EventBinding>()));
        _mockEventRuntime.GetBeforeStepEvents(Arg.Any<Goal>(), Arg.Any<GoalStep>())
            .Returns(Task.FromResult(new List<EventBinding>()));
        _mockEventRuntime.GetAfterStepEvents(Arg.Any<Goal>(), Arg.Any<GoalStep>())
            .Returns(Task.FromResult(new List<EventBinding>()));
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
    }

    [Test]
    public async Task Goal_Creation_Works()
    {
        // Arrange & Act
        var goal = CreateTestGoal("TestGoal", new List<GoalStep>());

        // Assert
        await Assert.That(goal.GoalName).IsEqualTo("TestGoal");
        await Assert.That(goal.GoalSteps).IsEmpty();
    }

    [Test]
    public async Task Goal_WithSteps_HasCorrectStepCount()
    {
        // Arrange
        var steps = new List<GoalStep>
        {
            new GoalStep { Text = "Step 1" },
            new GoalStep { Text = "Step 2" }
        };

        // Act
        var goal = CreateTestGoal("TestGoal", steps);

        // Assert
        await Assert.That(goal.GoalSteps).HasCount().EqualTo(2);
    }

    private Goal CreateTestGoal(string name, List<GoalStep> steps)
    {
        return new Goal
        {
            GoalName = name,
            GoalSteps = steps,
            RelativePrPath = $"/.build/{name}/00. Goal.pr",
            AbsolutePrFilePath = $"C:\\test\\.build\\{name}\\00. Goal.pr",
            AbsoluteGoalFolderPath = $"C:\\test\\{name}",
            RelativeGoalFolderPath = $"\\{name}",
            Injections = new List<Injections>()
        };
    }
}
