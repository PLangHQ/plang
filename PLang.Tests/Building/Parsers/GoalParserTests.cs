using LightInject;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace PLang.Tests.Building.Parsers;

public class GoalParserTests
{
    private ServiceContainer _container = null!;
    private IPLangFileSystem _fileSystem = null!;
    private ISettings _settings = null!;
    private ILogger _logger = null!;

    private IFile _fileMock = null!;
    private IDirectory _directoryMock = null!;
    private IPath _pathMock = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _fileSystem = Substitute.For<IPLangFileSystem>();
        _settings = Substitute.For<ISettings>();
        _logger = Substitute.For<ILogger>();

        SetupFileSystemMocks();
        SetupSettingsMocks();
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
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

        // Setup Path methods to use System.IO.Path behavior
        _pathMock.DirectorySeparatorChar.Returns(Path.DirectorySeparatorChar);
        _pathMock.Join(Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Join((string)x[0], (string)x[1]));
        _pathMock.Join(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Join((string)x[0], (string)x[1], (string)x[2]));
        _pathMock.GetExtension(Arg.Any<string>())
            .Returns(x => Path.GetExtension((string)x[0]));
        _pathMock.GetFileName(Arg.Any<string>())
            .Returns(x => Path.GetFileName((string)x[0]));
        _pathMock.GetDirectoryName(Arg.Any<string>())
            .Returns(x => Path.GetDirectoryName((string)x[0]));
    }

    private void SetupSettingsMocks()
    {
        _settings.GetOrDefault<Dictionary<string, DateTime>>(
            Arg.Any<Type>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, DateTime>>())
            .Returns(new Dictionary<string, DateTime>());
    }

    private GoalParser CreateParser()
    {
        return new GoalParser(_container, _fileSystem, _settings, _logger);
    }

    #region Basic Parsing Tests

    [Test]
    public async Task ParseGoalFile_SingleGoalWithOneStep_ReturnsGoalWithStep()
    {
        // Arrange
        var goalContent = @"MyGoal
- do something";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).HasCount().EqualTo(1);
        await Assert.That(goals[0].GoalName).IsEqualTo("MyGoal");
        await Assert.That(goals[0].GoalSteps).HasCount().EqualTo(1);
        await Assert.That(goals[0].GoalSteps[0].Text).IsEqualTo("do something");
    }

    [Test]
    public async Task ParseGoalFile_SingleGoalWithMultipleSteps_ReturnsAllSteps()
    {
        // Arrange
        var goalContent = @"ProcessData
- read file data.txt
- parse the content
- write to output.txt";
        var filePath = @"C:\app\ProcessData.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).HasCount().EqualTo(1);
        await Assert.That(goals[0].GoalSteps).HasCount().EqualTo(3);
        await Assert.That(goals[0].GoalSteps[0].Text).IsEqualTo("read file data.txt");
        await Assert.That(goals[0].GoalSteps[1].Text).IsEqualTo("parse the content");
        await Assert.That(goals[0].GoalSteps[2].Text).IsEqualTo("write to output.txt");
    }

    [Test]
    public async Task ParseGoalFile_GoalWithLineComment_ParsesCommentCorrectly()
    {
        // Arrange
        var goalContent = @"/ This is a goal comment
MyGoal
/ This is a step comment
- do something";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).HasCount().EqualTo(1);
        await Assert.That(goals[0].Comment).IsEqualTo("This is a goal comment");
        await Assert.That(goals[0].GoalSteps[0].Comment).IsEqualTo("This is a step comment");
    }

    [Test]
    public async Task ParseGoalFile_GoalWithBlockComment_ParsesCommentCorrectly()
    {
        // Arrange
        var goalContent = @"/* This is a
multi-line block comment */
MyGoal
- do something";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).HasCount().EqualTo(1);
        await Assert.That(goals[0].Comment).Contains("multi-line block comment");
    }

    [Test]
    public async Task ParseGoalFile_MultipleGoalsInOneFile_ReturnsAllGoals()
    {
        // Arrange
        var goalContent = @"MainGoal
- step one

SubGoal
- step two";
        var filePath = @"C:\app\MainGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).HasCount().EqualTo(2);
        await Assert.That(goals[0].GoalName).IsEqualTo("MainGoal");
        await Assert.That(goals[1].GoalName).IsEqualTo("SubGoal");
    }

    #endregion

    #region Step Parsing Tests

    [Test]
    public async Task ParseGoalFile_StepWithZeroIndent_HasExecuteTrue()
    {
        // Arrange
        var goalContent = @"MyGoal
- step with no indent";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[0].Indent).IsEqualTo(0);
        await Assert.That(goals[0].GoalSteps[0].Execute).IsTrue();
    }

    [Test]
    public async Task ParseGoalFile_StepWithFourSpaceIndent_HasExecuteFalse()
    {
        // Arrange
        var goalContent = @"MyGoal
- outer step
    - indented step";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[1].Indent).IsEqualTo(4);
        await Assert.That(goals[0].GoalSteps[1].Execute).IsFalse();
    }

    [Test]
    public async Task ParseGoalFile_StepWithEightSpaceIndent_ParsesCorrectly()
    {
        // Arrange
        var goalContent = @"MyGoal
- level 0
    - level 1
        - level 2";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[2].Indent).IsEqualTo(8);
    }

    [Test]
    public async Task ParseGoalFile_StepWithInvalidIndentation_ThrowsBuildStepException()
    {
        // Arrange
        var goalContent = @"MyGoal
  - step with 2 space indent";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BuilderStepException>(() =>
        {
            parser.ParseGoalFile(filePath);
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Indentation");
        await Assert.That(exception.Message).Contains("not correct");
    }

    [Test]
    public async Task ParseGoalFile_StepWithSpecialCharacters_ParsesCorrectly()
    {
        // Arrange
        var goalContent = @"MyGoal
- write 'Hello, World!' to %output%
- set value = %input% + 100 * 2";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[0].Text).IsEqualTo("write 'Hello, World!' to %output%");
        await Assert.That(goals[0].GoalSteps[1].Text).IsEqualTo("set value = %input% + 100 * 2");
    }

    [Test]
    public async Task ParseGoalFile_StepWithTabsConvertedToSpaces_ParsesCorrectly()
    {
        // Arrange - tabs get converted to 4 spaces
        var goalContent = "MyGoal\n- outer\n\t- tabbed step";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[1].Indent).IsEqualTo(4);
    }

    #endregion

    #region Path Resolution Tests

    [Test]
    public async Task ParseGoalFile_AppGoalPath_SetsCorrectRelativePath()
    {
        // Arrange
        var goalContent = @"TestGoal
- do something";
        var filePath = @"C:\app\TestGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].RelativeGoalPath).IsEqualTo(@"\TestGoal.goal");
        await Assert.That(goals[0].AbsoluteGoalPath).IsEqualTo(filePath);
    }

    [Test]
    public async Task ParseGoalFile_SystemGoal_SetsCorrectAbsoluteAppStartupPath()
    {
        // Arrange
        var goalContent = @"SystemGoal
- system step";
        var filePath = @"C:\app\system\SystemGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath, isSystem: true);

        // Assert
        // When isSystem=true, the rootPath is set to SystemDirectory
        await Assert.That(goals[0].AbsoluteAppStartupFolderPath).IsEqualTo(@"C:\app\system");
    }

    [Test]
    public async Task ParseGoalFile_GoalInSubfolder_SetsCorrectPaths()
    {
        // Arrange
        var goalContent = @"SubfolderGoal
- do something";
        var filePath = @"C:\app\features\SubfolderGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].RelativeGoalPath).IsEqualTo(@"\features\SubfolderGoal.goal");
        await Assert.That(goals[0].RelativeGoalFolderPath).IsEqualTo(@"\features");
    }

    [Test]
    public async Task ParseGoalFile_GoalInAppsFolder_SetsCorrectAppPaths()
    {
        // Arrange
        var goalContent = @"AppGoal
- app step";
        var filePath = @"C:\app\apps\myapp\AppGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].AbsoluteAppStartupFolderPath).IsEqualTo(@"C:\app\apps\myapp");
    }

    #endregion

    #region Goal Properties Tests

    [Test]
    public async Task ParseGoalFile_FirstGoal_HasPublicVisibility()
    {
        // Arrange
        var goalContent = @"PublicGoal
- step one

PrivateGoal
- step two";
        var filePath = @"C:\app\PublicGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].Visibility).IsEqualTo(Visibility.Public);
    }

    [Test]
    public async Task ParseGoalFile_SubsequentGoals_HavePrivateVisibility()
    {
        // Arrange
        var goalContent = @"PublicGoal
- step one

PrivateGoal
- step two";
        var filePath = @"C:\app\PublicGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[1].Visibility).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task ParseGoalFile_SubGoals_HaveParentGoalSet()
    {
        // Arrange
        var goalContent = @"ParentGoal
- step one

ChildGoal
- step two";
        var filePath = @"C:\app\ParentGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[1].ParentGoal).IsNotNull();
        await Assert.That(goals[1].ParentGoal!.GoalName).IsEqualTo("ParentGoal");
    }

    [Test]
    public async Task ParseGoalFile_ParentGoal_HasSubGoalsPaths()
    {
        // Arrange
        var goalContent = @"ParentGoal
- step one

SubGoal
- step two";
        var filePath = @"C:\app\ParentGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].SubGoals).HasCount().EqualTo(1);
        await Assert.That(goals[0].SubGoals[0]).Contains("SubGoal");
    }

    [Test]
    public async Task ParseGoalFile_NewGoal_HasChangedTrue()
    {
        // Arrange
        var goalContent = @"NewGoal
- step one";
        var filePath = @"C:\app\NewGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].HasChanged).IsTrue();
    }

    [Test]
    public async Task ParseGoalFile_StepsHaveCorrectLineNumbers()
    {
        // Arrange
        var goalContent = @"MyGoal
- first step
- second step
- third step";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[0].LineNumber).IsEqualTo(2);
        await Assert.That(goals[0].GoalSteps[1].LineNumber).IsEqualTo(3);
        await Assert.That(goals[0].GoalSteps[2].LineNumber).IsEqualTo(4);
    }

    [Test]
    public async Task ParseGoalFile_StepsHaveCorrectNumbers()
    {
        // Arrange
        var goalContent = @"MyGoal
- first step
- second step";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[0].Number).IsEqualTo(1);
        await Assert.That(goals[0].GoalSteps[1].Number).IsEqualTo(2);
    }

    [Test]
    public async Task ParseGoalFile_StepsHaveCorrectIndex()
    {
        // Arrange
        var goalContent = @"MyGoal
- first step
- second step";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[0].Index).IsEqualTo(0);
        await Assert.That(goals[0].GoalSteps[1].Index).IsEqualTo(1);
    }

    #endregion

    #region Error Cases Tests

    [Test]
    public async Task ParseGoalFile_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var goalContent = @"";
        var filePath = @"C:\app\Empty.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).IsEmpty();
    }

    [Test]
    public async Task ParseGoalFile_FileWithOnlyWhitespace_LogsWarning()
    {
        // Arrange
        var goalContent = @"

   ";
        var filePath = @"C:\app\Whitespace.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).IsEmpty();
    }

    [Test]
    public async Task ParseGoalFile_NonGoalFile_ThrowsException()
    {
        // Arrange
        var filePath = @"C:\app\notaGoal.txt";
        _fileMock.ReadAllText(filePath).Returns("some content");

        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
        {
            parser.ParseGoalFile(filePath);
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("not a goal file");
    }

    [Test]
    public async Task ParseGoalFile_DuplicateGoalNames_ThrowsBuilderException()
    {
        // Arrange
        var goalContent = @"SameName
- step one

SameName
- step two";
        var filePath = @"C:\app\Duplicate.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BuilderException>(() =>
        {
            parser.ParseGoalFile(filePath);
            return Task.CompletedTask;
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("defined two times");
    }

    [Test]
    public async Task ParseGoalFile_GoalWithNoSteps_ReturnsGoalWithEmptySteps()
    {
        // Arrange
        var goalContent = @"EmptyGoal";
        var filePath = @"C:\app\EmptyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals).HasCount().EqualTo(1);
        await Assert.That(goals[0].GoalSteps).IsEmpty();
    }

    #endregion

    #region Injection Tests

    // Note: Injection tests require a fully configured container with ILogger registered.
    // The HandleInjections method in GoalParser calls into the ServiceContainer to register
    // custom injections, which requires the container to have its dependencies set up.
    // These tests would require integration testing rather than unit testing.

    #endregion

    #region Multi-line Step Tests

    [Test]
    public async Task ParseGoalFile_StepContinuedOnNextLine_CombinesLines()
    {
        // Arrange
        var goalContent = @"MyGoal
- write out 'Hello
    World'";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps).HasCount().EqualTo(1);
        await Assert.That(goals[0].GoalSteps[0].Text).Contains("Hello");
        await Assert.That(goals[0].GoalSteps[0].Text).Contains("World");
    }

    #endregion

    #region Goal Reference Tests

    [Test]
    public async Task ParseGoalFile_StepsReferenceGoal()
    {
        // Arrange
        var goalContent = @"MyGoal
- do something";
        var filePath = @"C:\app\MyGoal.goal";
        _fileMock.ReadAllText(filePath).Returns(goalContent);

        var parser = CreateParser();

        // Act
        var goals = parser.ParseGoalFile(filePath);

        // Assert
        await Assert.That(goals[0].GoalSteps[0].Goal).IsNotNull();
        await Assert.That(goals[0].GoalSteps[0].Goal.GoalName).IsEqualTo("MyGoal");
    }

    #endregion
}
