using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace PLang.Tests.Runtime;

public class PseudoRuntimeTests
{
    private IPLangFileSystem _mockFileSystem = null!;
    private IPrParser _mockPrParser = null!;
    private ILogger _mockLogger = null!;
    private IEnginePool _mockEnginePool = null!;
    private IPath _mockPath = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockFileSystem = Substitute.For<IPLangFileSystem>();
        _mockPrParser = Substitute.For<IPrParser>();
        _mockLogger = Substitute.For<ILogger>();
        _mockEnginePool = Substitute.For<IEnginePool>();
        _mockPath = Substitute.For<IPath>();

        _mockFileSystem.Path.Returns(_mockPath);
        _mockFileSystem.RootDirectory.Returns("C:\\test");
        _mockPath.DirectorySeparatorChar.Returns('\\');
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>()).Returns(x => $"{x[0]}\\{x[1]}");
    }

    #region GetAppAbsolutePath tests

    [Test]
    public async Task GetAppAbsolutePath_WithAppsFolder_ExtractsAppPath()
    {
        // Arrange
        var runtime = new PseudoRuntime(_mockFileSystem, _mockPrParser, _mockLogger, _mockEnginePool);

        // Act
        var (absolutePath, goalName) = runtime.GetAppAbsolutePath("C:\\test\\apps\\MyApp\\SomeGoal");

        // Assert
        await Assert.That(absolutePath).IsEqualTo("C:\\test\\apps\\MyApp");
    }

    [Test]
    public async Task GetAppAbsolutePath_WithModulesFolder_ExtractsModulePath()
    {
        // Arrange
        var runtime = new PseudoRuntime(_mockFileSystem, _mockPrParser, _mockLogger, _mockEnginePool);

        // Act
        var (absolutePath, goalName) = runtime.GetAppAbsolutePath("C:\\test\\.modules\\MyModule\\Goal");

        // Assert
        await Assert.That(absolutePath).IsEqualTo("C:\\test\\.modules\\MyModule");
    }

    [Test]
    public async Task GetAppAbsolutePath_WithServicesFolder_ExtractsServicePath()
    {
        // Arrange
        var runtime = new PseudoRuntime(_mockFileSystem, _mockPrParser, _mockLogger, _mockEnginePool);

        // Act
        var (absolutePath, goalName) = runtime.GetAppAbsolutePath("C:\\test\\.services\\MyService\\Goal");

        // Assert
        await Assert.That(absolutePath).IsEqualTo("C:\\test\\.services\\MyService");
    }

    [Test]
    public async Task GetAppAbsolutePath_NoSpecialFolder_ReturnsOriginalPath()
    {
        // Arrange
        var runtime = new PseudoRuntime(_mockFileSystem, _mockPrParser, _mockLogger, _mockEnginePool);

        // Act
        var (absolutePath, goalName) = runtime.GetAppAbsolutePath("C:\\other\\path\\goal");

        // Assert
        await Assert.That(absolutePath).IsEqualTo("C:\\other\\path\\goal");
    }

    [Test]
    public async Task GetAppAbsolutePath_WithForwardSlashes_AdjustsToOs()
    {
        // Arrange
        var runtime = new PseudoRuntime(_mockFileSystem, _mockPrParser, _mockLogger, _mockEnginePool);

        // Act - forward slashes should be adjusted to OS separators
        var (absolutePath, goalName) = runtime.GetAppAbsolutePath("C:/test/apps/MyApp/Goal");

        // Assert
        await Assert.That(absolutePath).Contains("apps");
    }

    #endregion

    #region GoalToCallInfo execution options tests

    [Test]
    public async Task GoalToCallInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var goalToCall = new GoalToCallInfo("TestGoal");

        // Assert
        await Assert.That(goalToCall.WaitForExecution).IsTrue();
        await Assert.That(goalToCall.DelayWhenNotWaitingInMilliseconds).IsEqualTo(50);
        await Assert.That(goalToCall.WaitForXMillisecondsBeforeRunningGoal).IsEqualTo((uint)0);
        await Assert.That(goalToCall.Isolated).IsFalse();
        await Assert.That(goalToCall.DisableSystemGoals).IsFalse();
    }

    [Test]
    public async Task GoalToCallInfo_CanSetExecutionOptions()
    {
        // Arrange & Act
        var goalToCall = new GoalToCallInfo("TestGoal")
        {
            WaitForExecution = false,
            DelayWhenNotWaitingInMilliseconds = 100,
            WaitForXMillisecondsBeforeRunningGoal = 500,
            Isolated = true,
            DisableSystemGoals = true
        };

        // Assert
        await Assert.That(goalToCall.WaitForExecution).IsFalse();
        await Assert.That(goalToCall.DelayWhenNotWaitingInMilliseconds).IsEqualTo(100);
        await Assert.That(goalToCall.WaitForXMillisecondsBeforeRunningGoal).IsEqualTo((uint)500);
        await Assert.That(goalToCall.Isolated).IsTrue();
        await Assert.That(goalToCall.DisableSystemGoals).IsTrue();
    }

    [Test]
    public async Task GoalToCallInfo_WithParameters_PreservesParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            { "param1", "value1" },
            { "param2", 42 }
        };

        // Act
        var goalToCall = new GoalToCallInfo("TestGoal", parameters);

        // Assert
        await Assert.That(goalToCall.Parameters).ContainsKey("param1");
        await Assert.That(goalToCall.Parameters["param1"]).IsEqualTo("value1");
        await Assert.That(goalToCall.Parameters["param2"]).IsEqualTo(42);
    }

    [Test]
    public async Task GoalToCallInfo_ImplicitConversion_FromString()
    {
        // Act
        GoalToCallInfo goalToCall = "TestGoal";

        // Assert
        await Assert.That(goalToCall.Name).IsEqualTo("TestGoal");
    }

    #endregion

    #region Constructor tests

    [Test]
    public async Task PseudoRuntime_Constructor_AcceptsDependencies()
    {
        // Act
        var runtime = new PseudoRuntime(_mockFileSystem, _mockPrParser, _mockLogger, _mockEnginePool);

        // Assert
        await Assert.That(runtime).IsNotNull();
    }

    #endregion
}

/// <summary>
/// Tests for GoalToCallInfo as a data transfer object
/// </summary>
public class GoalToCallInfoTests
{
    [Test]
    public async Task GoalToCallInfo_Name_IsSet()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert
        await Assert.That(info.Name).IsEqualTo("MyGoal");
    }

    [Test]
    public async Task GoalToCallInfo_Path_DefaultsToNull()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert - Path defaults to null until explicitly set
        await Assert.That(info.Path).IsNull();
    }

    [Test]
    public async Task GoalToCallInfo_Path_CanBeSet()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal") { Path = "/custom/path" };

        // Assert
        await Assert.That(info.Path).IsEqualTo("/custom/path");
    }

    [Test]
    public async Task GoalToCallInfo_Parameters_InitializedEmpty()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert
        await Assert.That(info.Parameters).IsNotNull();
        await Assert.That(info.Parameters).IsEmpty();
    }

    [Test]
    public async Task GoalToCallInfo_WaitForExecution_DefaultsToTrue()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert - blocking execution is the default
        await Assert.That(info.WaitForExecution).IsTrue();
    }

    [Test]
    public async Task GoalToCallInfo_WaitForExecution_CanBeDisabled()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal") { WaitForExecution = false };

        // Assert - async execution
        await Assert.That(info.WaitForExecution).IsFalse();
    }

    [Test]
    public async Task GoalToCallInfo_Isolated_DefaultsToFalse()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert - by default, goals share context
        await Assert.That(info.Isolated).IsFalse();
    }

    [Test]
    public async Task GoalToCallInfo_Isolated_CanBeEnabled()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal") { Isolated = true };

        // Assert - isolated context
        await Assert.That(info.Isolated).IsTrue();
    }

    [Test]
    public async Task GoalToCallInfo_DisableSystemGoals_DefaultsToFalse()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert - system goals are enabled by default
        await Assert.That(info.DisableSystemGoals).IsFalse();
    }

    [Test]
    public async Task GoalToCallInfo_DisableSystemGoals_CanBeEnabled()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal") { DisableSystemGoals = true };

        // Assert
        await Assert.That(info.DisableSystemGoals).IsTrue();
    }

    [Test]
    public async Task GoalToCallInfo_DelayWhenNotWaiting_DefaultsTo50Ms()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert
        await Assert.That(info.DelayWhenNotWaitingInMilliseconds).IsEqualTo(50);
    }

    [Test]
    public async Task GoalToCallInfo_WaitBeforeRunning_DefaultsToZero()
    {
        // Arrange & Act
        var info = new GoalToCallInfo("MyGoal");

        // Assert
        await Assert.That(info.WaitForXMillisecondsBeforeRunningGoal).IsEqualTo((uint)0);
    }

    [Test]
    public async Task GoalToCallInfo_ImplicitConversion_CreatesWithName()
    {
        // Act
        GoalToCallInfo info = "SomeGoal";

        // Assert
        await Assert.That(info.Name).IsEqualTo("SomeGoal");
        await Assert.That(info.Parameters).IsEmpty();
    }

    [Test]
    public async Task GoalToCallInfo_WithDictionaryConstructor_SetsParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            { "key1", "value1" },
            { "key2", 123 },
            { "key3", true }
        };

        // Act
        var info = new GoalToCallInfo("MyGoal", parameters);

        // Assert
        await Assert.That(info.Parameters).HasCount().EqualTo(3);
        await Assert.That(info.Parameters["key1"]).IsEqualTo("value1");
        await Assert.That(info.Parameters["key2"]).IsEqualTo(123);
        await Assert.That(info.Parameters["key3"]).IsEqualTo(true);
    }
}
