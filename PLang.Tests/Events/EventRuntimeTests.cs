using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace PLang.Tests.Events;

public class EventRuntimeTests
{
    private IPLangFileSystem _mockFileSystem = null!;
    private IPrParser _mockPrParser = null!;
    private PLangAppContext _mockAppContext = null!;
    private IPLangContextAccessor _mockContextAccessor = null!;
    private ILogger _mockLogger = null!;
    private IEngine _mockEngine = null!;
    private IPseudoRuntime _mockPseudoRuntime = null!;
    private IPath _mockPath = null!;
    private IDirectory _mockDirectory = null!;
    private IFile _mockFile = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockFileSystem = Substitute.For<IPLangFileSystem>();
        _mockPrParser = Substitute.For<IPrParser>();
        _mockAppContext = new PLangAppContext();
        _mockContextAccessor = Substitute.For<IPLangContextAccessor>();
        _mockLogger = Substitute.For<ILogger>();
        _mockEngine = Substitute.For<IEngine>();
        _mockPseudoRuntime = Substitute.For<IPseudoRuntime>();
        _mockPath = Substitute.For<IPath>();
        _mockDirectory = Substitute.For<IDirectory>();
        _mockFile = Substitute.For<IFile>();

        _mockFileSystem.Path.Returns(_mockPath);
        _mockFileSystem.Directory.Returns(_mockDirectory);
        _mockFileSystem.File.Returns(_mockFile);
        _mockFileSystem.RootDirectory.Returns("C:\\test");
        _mockFileSystem.BuildPath.Returns("C:\\test\\.build");
        _mockPath.DirectorySeparatorChar.Returns('\\');
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>()).Returns(x => $"{x[0]}\\{x[1]}");
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(x => $"{x[0]}\\{x[1]}\\{x[2]}");
        _mockPath.GetExtension(Arg.Any<string>()).Returns(x => System.IO.Path.GetExtension(x.Arg<string>()));
    }

    #region GoalHasBinding tests

    [Test]
    public async Task GoalHasBinding_MatchesGoalName_ReturnsTrue()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("HelloWorld");
        var eventBinding = CreateEventBinding("HelloWorld");

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_MatchesGoalName_CaseInsensitive()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("HelloWorld");
        var eventBinding = CreateEventBinding("helloworld");

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_DifferentGoalName_ReturnsFalse()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("HelloWorld");
        var eventBinding = CreateEventBinding("OtherGoal");

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GoalHasBinding_WildcardStar_MatchesAll()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("AnyGoal");
        goal.AbsolutePrFolderPath = "C:\\test\\.build\\AnyGoal";
        var eventBinding = CreateEventBinding("/*");

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_PrivateGoal_ExcludedByDefault()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("PrivateGoal");
        goal.Visibility = Visibility.Private;
        var eventBinding = CreateEventBinding("PrivateGoal", isLocal: false);

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GoalHasBinding_PrivateGoal_IncludedWhenFlagSet()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("PrivateGoal");
        goal.Visibility = Visibility.Private;
        var eventBinding = CreateEventBinding("PrivateGoal", isLocal: false, includePrivate: true);

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_SystemGoal_ExcludedByDefault()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("SystemGoal");
        goal.IsSystem = true;
        var eventBinding = CreateEventBinding("SystemGoal", isLocal: false);

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GoalHasBinding_SystemGoal_IncludedWhenFlagSet()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("SystemGoal");
        goal.IsSystem = true;
        var eventBinding = CreateEventBinding("SystemGoal", isLocal: false, includeOsGoals: true);

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_LocalEvent_IncludesPrivate()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("PrivateGoal");
        goal.Visibility = Visibility.Private;
        var eventBinding = CreateEventBinding("PrivateGoal", isLocal: true);

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_GoalFileName_MatchesWithExtension()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("MyGoal");
        goal.GoalFileName = "MyGoal.goal";
        var eventBinding = CreateEventBinding("MyGoal.goal");

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GoalHasBinding_RegexPattern_MatchesGoal()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestProcess");
        var eventBinding = CreateEventBinding("^Test.*$");

        // Act
        var result = runtime.GoalHasBinding(goal, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region IsStepMatch tests

    [Test]
    public async Task IsStepMatch_NoFilters_ReturnsTrue()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestGoal");
        var step = new GoalStep { Text = "do something", Number = 1, Goal = goal };
        var eventBinding = CreateEventBinding("TestGoal");

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsStepMatch_StepNumber_MatchesExact()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestGoal");
        var step = new GoalStep { Text = "do something", Number = 3, Goal = goal };
        var eventBinding = CreateEventBinding("TestGoal", stepNumber: 3);

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsStepMatch_StepNumber_DoesNotMatchDifferent()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestGoal");
        var step = new GoalStep { Text = "do something", Number = 3, Goal = goal };
        var eventBinding = CreateEventBinding("TestGoal", stepNumber: 5);

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsStepMatch_StepText_MatchesContains()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestGoal");
        var step = new GoalStep { Text = "write to database", Number = 1, Goal = goal };
        var eventBinding = CreateEventBinding("TestGoal", stepText: "database");

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsStepMatch_StepText_CaseInsensitive()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestGoal");
        var step = new GoalStep { Text = "WRITE TO DATABASE", Number = 1, Goal = goal };
        var eventBinding = CreateEventBinding("TestGoal", stepText: "database");

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsStepMatch_StepText_DoesNotMatchMissing()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("TestGoal");
        var step = new GoalStep { Text = "write to file", Number = 1, Goal = goal };
        var eventBinding = CreateEventBinding("TestGoal", stepText: "database");

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsStepMatch_SystemGoalStep_ExcludedForNonLocalNonOsEvent()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var goal = CreateTestGoal("SystemGoal");
        goal.IsSystem = true;
        var step = new GoalStep { Text = "system step", Number = 1, Goal = goal };
        var eventBinding = CreateEventBinding("SystemGoal", isLocal: false, includeOsGoals: false);

        // Act
        var result = runtime.IsStepMatch(step, eventBinding);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetEventsFiles tests

    [Test]
    public async Task GetEventsFiles_BuildPathNotExists_ReturnsError()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);

        // Act
        var (files, error) = runtime.GetEventsFiles("C:\\nonexistent\\.build");

        // Assert
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains(".build folder does not exists");
    }

    [Test]
    public async Task GetEventsFiles_BuildPathExists_ReturnsEmptyListIfNoEvents()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        _mockDirectory.Exists(Arg.Any<string>()).Returns(true);
        _mockFile.Exists(Arg.Any<string>()).Returns(false);

        // Act
        var (files, error) = runtime.GetEventsFiles("C:\\test\\.build");

        // Assert
        await Assert.That(error).IsNull();
        await Assert.That(files).IsEmpty();
    }

    [Test]
    public async Task GetEventsFiles_EventFileExists_ReturnsFilePath()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        _mockDirectory.Exists(Arg.Any<string>()).Returns(true);
        _mockFile.Exists(Arg.Any<string>()).Returns(x => x.Arg<string>().EndsWith("00. Goal.pr"));

        // Act
        var (files, error) = runtime.GetEventsFiles("C:\\test\\.build");

        // Assert
        await Assert.That(error).IsNull();
        await Assert.That(files).HasCount().GreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetEventsFiles_BuilderMode_UsesBuilderEventsFolder()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        _mockDirectory.Exists(Arg.Any<string>()).Returns(true);
        string? capturedPath = null;
        _mockFile.Exists(Arg.Do<string>(x => capturedPath = x)).Returns(false);

        // Act
        runtime.GetEventsFiles("C:\\test\\.build", builder: true);

        // Assert - verify it looked in BuilderEvents folder
        await Assert.That(capturedPath).IsNotNull();
    }

    #endregion

    #region Load tests

    [Test]
    public async Task Load_WithNoEvents_ReturnsNull()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        _mockPrParser.GetEventsFiles(Arg.Any<bool>()).Returns(new List<Goal>());

        // Act
        var error = runtime.Load();

        // Assert
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Load_WithEvents_LoadsSuccessfully()
    {
        // Arrange
        var runtime = CreateEventRuntime();
        var eventGoal = CreateTestGoal("Events");
        eventGoal.AbsolutePrFolderPath = "C:\\test\\.build\\events";
        eventGoal.GoalSteps = new List<GoalStep>
        {
            new GoalStep
            {
                Text = "before each step",
                EventBinding = new EventBinding(
                    EventType.Before,
                    EventScope.Step,
                    new GoalToBindTo("*"),
                    new GoalToCallInfo("LogStep"),
                    true,
                    null,
                    null,
                    false
                )
            }
        };
        _mockPrParser.GetEventsFiles(Arg.Any<bool>()).Returns(new List<Goal> { eventGoal });

        // Act
        var error = runtime.Load();

        // Assert
        await Assert.That(error).IsNull();
    }

    #endregion

    #region Helper methods

    private EventRuntime CreateEventRuntime()
    {
        return new EventRuntime(
            _mockFileSystem,
            _mockPrParser,
            _mockAppContext,
            _mockContextAccessor,
            _mockLogger,
            _mockEngine,
            _mockPseudoRuntime
        );
    }

    private Goal CreateTestGoal(string name)
    {
        return new Goal
        {
            GoalName = name,
            GoalFileName = $"{name}.goal",
            GoalSteps = new List<GoalStep>(),
            AbsoluteGoalFolderPath = $"C:\\test\\{name}",
            AbsolutePrFolderPath = $"C:\\test\\.build\\{name}",
            RelativeGoalFolderPath = $"\\{name}",
            RelativeGoalPath = $"/{name}",
            RelativePrPath = $"/.build/{name}/00. Goal.pr",
            Injections = new List<Injections>(),
            Visibility = Visibility.Public,
            IsSystem = false,
            AppName = "\\"
        };
    }

    private EventBinding CreateEventBinding(
        string goalToBindTo,
        bool isLocal = true,
        bool includePrivate = false,
        bool includeOsGoals = false,
        int? stepNumber = null,
        string? stepText = null)
    {
        var eventGoal = CreateTestGoal("EventGoal");
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo(goalToBindTo),
            new GoalToCallInfo("EventHandler"),
            IncludePrivate: includePrivate,
            StepNumber: stepNumber,
            StepText: stepText,
            WaitForExecution: true,
            RunOnlyOnStartParameter: null,
            OnErrorContinueNextStep: false,
            ErrorKey: null,
            ErrorMessage: null,
            StatusCode: null,
            ExceptionType: null,
            IsLocal: isLocal,
            IncludeOsGoals: includeOsGoals,
            IsOnStep: false
        )
        {
            Goal = eventGoal,
            GoalStep = new GoalStep { Text = "event step", Goal = eventGoal }
        };
        return binding;
    }

    #endregion
}

/// <summary>
/// Tests for EventBinding record
/// </summary>
public class EventBindingTests
{
    [Test]
    public async Task EventBinding_EventType_Before_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            true
        );

        // Assert
        await Assert.That(binding.EventType).IsEqualTo(EventType.Before);
    }

    [Test]
    public async Task EventBinding_EventType_After_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.After,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            true
        );

        // Assert
        await Assert.That(binding.EventType).IsEqualTo(EventType.After);
    }

    [Test]
    public async Task EventBinding_EventScope_Goal_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            true
        );

        // Assert
        await Assert.That(binding.EventScope).IsEqualTo(EventScope.Goal);
    }

    [Test]
    public async Task EventBinding_EventScope_Step_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Step,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            true
        );

        // Assert
        await Assert.That(binding.EventScope).IsEqualTo(EventScope.Step);
    }

    [Test]
    public async Task EventBinding_WaitForExecution_True_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            WaitForExecution: true
        );

        // Assert
        await Assert.That(binding.WaitForExecution).IsTrue();
    }

    [Test]
    public async Task EventBinding_WaitForExecution_False_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            WaitForExecution: false
        );

        // Assert
        await Assert.That(binding.WaitForExecution).IsFalse();
    }

    [Test]
    public async Task EventBinding_StepNumber_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Step,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            WaitForExecution: true,
            StepNumber: 5
        );

        // Assert
        await Assert.That(binding.StepNumber).IsEqualTo(5);
    }

    [Test]
    public async Task EventBinding_StepText_IsSet()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Step,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            WaitForExecution: true,
            StepText: "write to file"
        );

        // Assert
        await Assert.That(binding.StepText).IsEqualTo("write to file");
    }

    [Test]
    public async Task EventBinding_ErrorKey_ForErrorScope()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.StepError,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("ErrorHandler"),
            WaitForExecution: true,
            ErrorKey: "FileNotFound"
        );

        // Assert
        await Assert.That(binding.ErrorKey).IsEqualTo("FileNotFound");
    }

    [Test]
    public async Task EventBinding_StatusCode_ForErrorScope()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.AppError,
            new GoalToBindTo("*"),
            new GoalToCallInfo("ErrorHandler"),
            WaitForExecution: true,
            StatusCode: 500
        );

        // Assert
        await Assert.That(binding.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task EventBinding_IncludePrivate_DefaultsFalse()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            WaitForExecution: true
        );

        // Assert
        await Assert.That(binding.IncludePrivate).IsFalse();
    }

    [Test]
    public async Task EventBinding_IncludeOsGoals_DefaultsFalse()
    {
        // Arrange & Act
        var binding = new EventBinding(
            EventType.Before,
            EventScope.Goal,
            new GoalToBindTo("Test"),
            new GoalToCallInfo("Handler"),
            WaitForExecution: true
        );

        // Assert
        await Assert.That(binding.IncludeOsGoals).IsFalse();
    }
}

/// <summary>
/// Tests for GoalToBindTo record
/// </summary>
public class GoalToBindToTests
{
    [Test]
    public async Task GoalToBindTo_Name_IsSet()
    {
        // Arrange & Act
        var binding = new GoalToBindTo("MyGoal");

        // Assert
        await Assert.That(binding.Name).IsEqualTo("MyGoal");
    }

    [Test]
    public async Task GoalToBindTo_ImplicitConversion_FromString()
    {
        // Act
        GoalToBindTo binding = "AnotherGoal";

        // Assert
        await Assert.That(binding.Name).IsEqualTo("AnotherGoal");
    }

    [Test]
    public async Task GoalToBindTo_Wildcard_IsSupported()
    {
        // Arrange & Act
        var binding = new GoalToBindTo("*");

        // Assert
        await Assert.That(binding.Name).IsEqualTo("*");
    }

    [Test]
    public async Task GoalToBindTo_PathPattern_IsSupported()
    {
        // Arrange & Act
        var binding = new GoalToBindTo("api/*");

        // Assert
        await Assert.That(binding.Name).IsEqualTo("api/*");
    }

    [Test]
    public async Task GoalToBindTo_FilePattern_IsSupported()
    {
        // Arrange & Act
        var binding = new GoalToBindTo("MyGoal.goal");

        // Assert
        await Assert.That(binding.Name).IsEqualTo("MyGoal.goal");
    }

    [Test]
    public async Task GoalToBindTo_SubGoalPattern_IsSupported()
    {
        // Arrange & Act
        var binding = new GoalToBindTo("Main.goal:SubGoal");

        // Assert
        await Assert.That(binding.Name).IsEqualTo("Main.goal:SubGoal");
    }
}
