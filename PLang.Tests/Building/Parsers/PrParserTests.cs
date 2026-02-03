using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using System.IO.Abstractions;

namespace PLang.Tests.Building.Parsers;

public class PrParserTests
{
    private IPLangFileSystem _fileSystem = null!;
    private ILogger _logger = null!;

    private IFile _fileMock = null!;
    private IDirectory _directoryMock = null!;
    private IPath _pathMock = null!;

    [Before(Test)]
    public void Setup()
    {
        _fileSystem = Substitute.For<IPLangFileSystem>();
        _logger = Substitute.For<ILogger>();

        SetupFileSystemMocks();
    }

    private void SetupFileSystemMocks()
    {
        _fileMock = Substitute.For<IFile>();
        _directoryMock = Substitute.For<IDirectory>();
        _pathMock = Substitute.For<IPath>();

        _fileSystem.File.Returns(_fileMock);
        _fileSystem.Directory.Returns(_directoryMock);
        _fileSystem.Path.Returns(_pathMock);
        _fileSystem.RootDirectory.Returns(@"C:\app");
        _fileSystem.SystemDirectory.Returns(@"C:\app\system");
        _fileSystem.BuildPath.Returns(@"C:\app\.build");
        _fileSystem.GoalsPath.Returns(@"C:\app");

        // Setup Path methods
        _pathMock.DirectorySeparatorChar.Returns(Path.DirectorySeparatorChar);
        _pathMock.Join(Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Join((string)x[0], (string)x[1]));
        _pathMock.Join(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Join((string)x[0], (string)x[1], (string)x[2]));
        _pathMock.Join(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Join((string)x[0], (string)x[1], (string)x[2], (string)x[3]));
        _pathMock.GetExtension(Arg.Any<string>())
            .Returns(x => Path.GetExtension((string)x[0]));
        _pathMock.GetFileName(Arg.Any<string>())
            .Returns(x => Path.GetFileName((string)x[0]));
        _pathMock.GetDirectoryName(Arg.Any<string>())
            .Returns(x => Path.GetDirectoryName((string)x[0]));
        _pathMock.GetFullPath(Arg.Any<string>())
            .Returns(x => Path.GetFullPath((string)x[0]));
        _pathMock.TrimEndingDirectorySeparator(Arg.Any<string>())
            .Returns(x => ((string)x[0]).TrimEnd(Path.DirectorySeparatorChar));

        // Default to no files/directories existing
        _directoryMock.Exists(Arg.Any<string>()).Returns(false);
        _fileMock.Exists(Arg.Any<string>()).Returns(false);
        _directoryMock.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());
    }

    private string CreateGoalJson(string goalName, string relativeGoalPath = "\\TestGoal.goal",
        string relativePrPath = ".build\\TestGoal\\00. Goal.pr",
        Visibility visibility = Visibility.Public,
        List<GoalStep>? steps = null)
    {
        var goal = new Goal
        {
            GoalName = goalName,
            RelativeGoalPath = relativeGoalPath,
            RelativeGoalFolderPath = Path.GetDirectoryName(relativeGoalPath) ?? "\\",
            RelativePrPath = relativePrPath,
            RelativePrFolderPath = Path.GetDirectoryName(relativePrPath) ?? ".build",
            Visibility = visibility,
            GoalSteps = steps ?? new List<GoalStep>
            {
                new GoalStep { Text = "do something", PrFileName = "01. do something.pr", Number = 0 }
            }
        };
        return JsonConvert.SerializeObject(goal);
    }

    private PrParser CreateParser()
    {
        return new PrParser(_fileSystem, _logger);
    }

    #region ParsePrFile Tests

    [Test]
    public async Task ParsePrFile_ValidPrFile_ReturnsGoal()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task ParsePrFile_NonPrFile_ThrowsArgumentException()
    {
        // Arrange
        var filePath = @"C:\app\notPrFile.txt";

        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            parser.ParsePrFile(filePath);
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("does not contain .pr file");
    }

    [Test]
    public async Task ParsePrFile_FileNotFound_ReturnsNull()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\Missing\00. Goal.pr";
        _fileMock.Exists(prFilePath).Returns(false);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNull();
    }

    [Test]
    public async Task ParsePrFile_SetsAbsolutePaths()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.AbsoluteAppStartupFolderPath).IsNotNull();
        await Assert.That(goal.AbsoluteGoalPath).IsNotNull();
        await Assert.That(goal.AbsolutePrFilePath).IsNotNull();
    }

    [Test]
    public async Task ParsePrFile_AppsFolder_SetsAppName()
    {
        // Arrange
        var prFilePath = @"C:\app\apps\myapp\.build\Start\00. Goal.pr";
        var goalJson = CreateGoalJson("Start",
            relativeGoalPath: "\\Start.goal",
            relativePrPath: ".build\\Start\\00. Goal.pr");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.AppName).IsEqualTo("myapp");
    }

    [Test]
    public async Task ParsePrFile_ServicesFolder_SetsAppName()
    {
        // Arrange
        var prFilePath = @"C:\app\.services\myservice\.build\Start\00. Goal.pr";
        var goalJson = CreateGoalJson("Start",
            relativeGoalPath: "\\Start.goal",
            relativePrPath: ".build\\Start\\00. Goal.pr");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.AppName).IsEqualTo("myservice");
    }

    [Test]
    public async Task ParsePrFile_SystemFolder_SetsIsSystemTrue()
    {
        // Arrange
        var prFilePath = @"C:\app\system\.build\SystemGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("SystemGoal");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.IsSystem).IsTrue();
    }

    [Test]
    public async Task ParsePrFile_RegularPath_SetsIsSystemFalse()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.IsSystem).IsFalse();
    }

    [Test]
    public async Task ParsePrFile_SetsStepGoalReference()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalSteps[0].Goal).IsEqualTo(goal);
    }

    [Test]
    public async Task ParsePrFile_SetsStepAbsolutePrFilePath()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalSteps[0].AbsolutePrFilePath).IsNotNull();
        await Assert.That(goal.GoalSteps[0].AbsolutePrFilePath).Contains("01. do something.pr");
    }

    [Test]
    public async Task ParsePrFile_SetsStepIndexAndNumber()
    {
        // Arrange
        var steps = new List<GoalStep>
        {
            new GoalStep { Text = "step one", PrFileName = "01. step one.pr" },
            new GoalStep { Text = "step two", PrFileName = "02. step two.pr" }
        };
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal", steps: steps);

        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.ParsePrFile(prFilePath);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalSteps[0].Index).IsEqualTo(0);
        await Assert.That(goal.GoalSteps[0].Number).IsEqualTo(0);
        await Assert.That(goal.GoalSteps[1].Index).IsEqualTo(1);
        await Assert.That(goal.GoalSteps[1].Number).IsEqualTo(1);
    }

    #endregion

    #region GetGoal(GoalToCallInfo) Tests

    [Test]
    public async Task GetGoal_ByPath_ReturnsGoal()
    {
        // Arrange - setup a goal that will be found
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal", relativePrPath: ".build\\TestGoal\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();
        var goalToCall = new GoalToCallInfo("TestGoal") { Path = ".build\\TestGoal\\00. Goal.pr" };

        // Act
        var (goal, error) = parser.GetGoal(goalToCall);

        // Assert
        await Assert.That(error).IsNull();
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task GetGoal_ByName_ReturnsGoal()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();
        var goalToCall = new GoalToCallInfo("TestGoal");

        // Act
        var (goal, error) = parser.GetGoal(goalToCall);

        // Assert
        await Assert.That(error).IsNull();
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task GetGoal_NotFound_ReturnsError()
    {
        // Arrange
        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        var parser = CreateParser();
        var goalToCall = new GoalToCallInfo("NonExistentGoal");

        // Act
        var (goal, error) = parser.GetGoal(goalToCall);

        // Assert
        await Assert.That(goal).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("could not be found");
    }

    [Test]
    public async Task GetGoal_MultipleGoalsWithSameName_ReturnsError()
    {
        // Arrange - two goals with same name
        var prFilePath1 = @"C:\app\.build\folder1\TestGoal\00. Goal.pr";
        var prFilePath2 = @"C:\app\.build\folder2\TestGoal\00. Goal.pr";
        var goalJson1 = CreateGoalJson("DuplicateName",
            relativeGoalPath: "\\folder1\\TestGoal.goal",
            relativePrPath: ".build\\folder1\\TestGoal\\00. Goal.pr");
        var goalJson2 = CreateGoalJson("DuplicateName",
            relativeGoalPath: "\\folder2\\TestGoal.goal",
            relativePrPath: ".build\\folder2\\TestGoal\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath1, prFilePath2 });
        _fileMock.Exists(prFilePath1).Returns(true);
        _fileMock.Exists(prFilePath2).Returns(true);
        _fileMock.ReadAllText(prFilePath1).Returns(goalJson1);
        _fileMock.ReadAllText(prFilePath2).Returns(goalJson2);

        var parser = CreateParser();
        var goalToCall = new GoalToCallInfo("DuplicateName");

        // Act
        var (goal, error) = parser.GetGoal(goalToCall);

        // Assert
        await Assert.That(goal).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("Found more the one goal");
    }

    #endregion

    #region GetGoalByAppAndGoalName Tests

    [Test]
    public async Task GetGoalByAppAndGoalName_NullAppStartupPath_ThrowsArgumentNullException()
    {
        // Arrange
        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
        {
            parser.GetGoalByAppAndGoalName(null!, "SomeGoal");
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_NullGoalName_ThrowsArgumentNullException()
    {
        // Arrange
        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
        {
            parser.GetGoalByAppAndGoalName(@"C:\app", null!);
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_SimpleGoalName_FindsGoal()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal", relativePrPath: ".build\\TestGoal\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.GetGoalByAppAndGoalName(@"C:\app", "TestGoal");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_GoalNotFound_ReturnsNull()
    {
        // Arrange
        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        var parser = CreateParser();

        // Act
        var goal = parser.GetGoalByAppAndGoalName(@"C:\app", "NonExistent");

        // Assert
        await Assert.That(goal).IsNull();
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_PathWithFolder_FindsGoal()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\ui\List\00. Goal.pr";
        var goalJson = CreateGoalJson("List",
            relativeGoalPath: "\\ui\\List.goal",
            relativePrPath: ".build\\ui\\List\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.GetGoalByAppAndGoalName(@"C:\app", "ui\\List");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("List");
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_RootPath_FindsGoal()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\Start\00. Goal.pr";
        var goalJson = CreateGoalJson("Start", relativePrPath: ".build\\Start\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goal = parser.GetGoalByAppAndGoalName(@"C:\app", "\\Start");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("Start");
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_MultipleMatchingGoals_ThrowsGoalNotFoundException()
    {
        // Arrange - two goals ending with same path
        var prFilePath1 = @"C:\app\.build\a\List\00. Goal.pr";
        var prFilePath2 = @"C:\app\.build\b\List\00. Goal.pr";
        var goalJson1 = CreateGoalJson("List",
            relativeGoalPath: "\\a\\List.goal",
            relativePrPath: ".build\\a\\List\\00. Goal.pr");
        var goalJson2 = CreateGoalJson("List",
            relativeGoalPath: "\\b\\List.goal",
            relativePrPath: ".build\\b\\List\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath1, prFilePath2 });
        _fileMock.Exists(prFilePath1).Returns(true);
        _fileMock.Exists(prFilePath2).Returns(true);
        _fileMock.ReadAllText(prFilePath1).Returns(goalJson1);
        _fileMock.ReadAllText(prFilePath2).Returns(goalJson2);

        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GoalNotFoundException>(() =>
        {
            parser.GetGoalByAppAndGoalName(@"C:\app", "List");
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("to choose from");
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_RemovesGoalExtension()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal", relativePrPath: ".build\\TestGoal\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act - pass with .goal extension
        var goal = parser.GetGoalByAppAndGoalName(@"C:\app", "TestGoal.goal");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task GetGoalByAppAndGoalName_RemovesExclamationMark()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal", relativePrPath: ".build\\TestGoal\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act - pass with ! prefix
        var goal = parser.GetGoalByAppAndGoalName(@"C:\app", "!TestGoal");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("TestGoal");
    }

    #endregion

    #region GetEvents Tests

    [Test]
    public async Task GetEvents_EventNotFound_ReturnsEmptyList()
    {
        // Arrange
        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        var parser = CreateParser();

        // Act
        var events = parser.GetEvents("NonExistentEvent");

        // Assert
        await Assert.That(events).IsEmpty();
    }

    [Test]
    public async Task GetEvents_EventFound_ReturnsGoals()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\OnAppStart\00. Goal.pr";
        var goalJson = CreateGoalJson("OnAppStart");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var events = parser.GetEvents("OnAppStart");

        // Assert
        await Assert.That(events).HasCount().EqualTo(1);
        await Assert.That(events[0].GoalName).IsEqualTo("OnAppStart");
    }

    [Test]
    public async Task GetEvents_CacheHit_ReturnsSameList()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\OnAppStart\00. Goal.pr";
        var goalJson = CreateGoalJson("OnAppStart");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act - call twice
        var events1 = parser.GetEvents("OnAppStart");
        var events2 = parser.GetEvents("OnAppStart");

        // Assert - should be same reference (cached)
        await Assert.That(events1).IsEqualTo(events2);
    }

    [Test]
    public async Task GetEvent_ReturnsFirstMatch()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\OnAppStart\00. Goal.pr";
        var goalJson = CreateGoalJson("OnAppStart");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var eventGoal = parser.GetEvent("OnAppStart");

        // Assert
        await Assert.That(eventGoal).IsNotNull();
        await Assert.That(eventGoal!.GoalName).IsEqualTo("OnAppStart");
    }

    [Test]
    public async Task GetEvent_NotFound_ReturnsNull()
    {
        // Arrange
        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        var parser = CreateParser();

        // Act
        var eventGoal = parser.GetEvent("NonExistent");

        // Assert
        await Assert.That(eventGoal).IsNull();
    }

    #endregion

    #region GetSystemEvents Tests

    [Test]
    public async Task GetSystemEvents_EventFound_ReturnsGoals()
    {
        // Arrange
        var prFilePath = @"C:\app\system\.build\OnAppError\00. Goal.pr";
        var goalJson = CreateGoalJson("OnAppError");

        _directoryMock.Exists(@"C:\app\system\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\system\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var events = parser.GetSystemEvents("OnAppError");

        // Assert
        await Assert.That(events).HasCount().EqualTo(1);
        await Assert.That(events[0].GoalName).IsEqualTo("OnAppError");
    }

    [Test]
    public async Task GetSystemEvent_ReturnsFirstMatch()
    {
        // Arrange
        var prFilePath = @"C:\app\system\.build\OnAppError\00. Goal.pr";
        var goalJson = CreateGoalJson("OnAppError");

        _directoryMock.Exists(@"C:\app\system\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\system\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var eventGoal = parser.GetSystemEvent("OnAppError");

        // Assert
        await Assert.That(eventGoal).IsNotNull();
        await Assert.That(eventGoal!.GoalName).IsEqualTo("OnAppError");
    }

    #endregion

    #region LoadAllGoalsByPath Tests

    [Test]
    public async Task LoadAllGoalsByPath_NoBuildDirectory_ReturnsEmptyList()
    {
        // Arrange
        _directoryMock.Exists(@"C:\app\.build").Returns(false);

        var parser = CreateParser();

        // Act
        var goals = parser.LoadAllGoalsByPath(@"C:\app");

        // Assert
        await Assert.That(goals).IsEmpty();
    }

    [Test]
    public async Task LoadAllGoalsByPath_WithGoals_ReturnsAllGoals()
    {
        // Arrange
        var prFilePath1 = @"C:\app\.build\Goal1\00. Goal.pr";
        var prFilePath2 = @"C:\app\.build\Goal2\00. Goal.pr";
        var goalJson1 = CreateGoalJson("Goal1");
        var goalJson2 = CreateGoalJson("Goal2", relativePrPath: ".build\\Goal2\\00. Goal.pr");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath1, prFilePath2 });
        _fileMock.Exists(prFilePath1).Returns(true);
        _fileMock.Exists(prFilePath2).Returns(true);
        _fileMock.ReadAllText(prFilePath1).Returns(goalJson1);
        _fileMock.ReadAllText(prFilePath2).Returns(goalJson2);

        var parser = CreateParser();

        // Act
        var goals = parser.LoadAllGoalsByPath(@"C:\app");

        // Assert
        await Assert.That(goals).HasCount().EqualTo(2);
    }

    #endregion

    #region GetPublicGoals Tests

    [Test]
    public async Task GetPublicGoals_ReturnsOnlyPublicGoals()
    {
        // Arrange
        var prFilePath1 = @"C:\app\.build\PublicGoal\00. Goal.pr";
        var prFilePath2 = @"C:\app\.build\PrivateGoal\00. Goal.pr";
        var publicGoalJson = CreateGoalJson("PublicGoal", visibility: Visibility.Public);
        var privateGoalJson = CreateGoalJson("PrivateGoal",
            relativePrPath: ".build\\PrivateGoal\\00. Goal.pr",
            visibility: Visibility.Private);

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath1, prFilePath2 });
        _fileMock.Exists(prFilePath1).Returns(true);
        _fileMock.Exists(prFilePath2).Returns(true);
        _fileMock.ReadAllText(prFilePath1).Returns(publicGoalJson);
        _fileMock.ReadAllText(prFilePath2).Returns(privateGoalJson);

        var parser = CreateParser();

        // Act
        var publicGoals = parser.GetPublicGoals();

        // Assert
        await Assert.That(publicGoals).HasCount().EqualTo(1);
        await Assert.That(publicGoals[0].GoalName).IsEqualTo("PublicGoal");
    }

    #endregion

    #region ForceLoadAllGoals Tests

    [Test]
    public async Task ForceLoadAllGoals_ReloadsGoals()
    {
        // Arrange
        var prFilePath = @"C:\app\.build\TestGoal\00. Goal.pr";
        var goalJson = CreateGoalJson("TestGoal");

        _directoryMock.Exists(@"C:\app\.build").Returns(true);
        _directoryMock.GetFiles(@"C:\app\.build", "00. Goal.pr", SearchOption.AllDirectories)
            .Returns(new[] { prFilePath });
        _fileMock.Exists(prFilePath).Returns(true);
        _fileMock.ReadAllText(prFilePath).Returns(goalJson);

        var parser = CreateParser();

        // Act
        var goals = parser.ForceLoadAllGoals();

        // Assert
        await Assert.That(goals).HasCount().EqualTo(1);
    }

    #endregion
}
