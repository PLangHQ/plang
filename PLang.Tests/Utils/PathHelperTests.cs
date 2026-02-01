using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.IO.Abstractions;

namespace PLang.Tests.Utils;

public class PathHelperTests
{
    private IPLangFileSystem _fileSystem = null!;
    private IPath _mockPath = null!;

    [Before(Test)]
    public void Setup()
    {
        _fileSystem = Substitute.For<IPLangFileSystem>();
        _mockPath = Substitute.For<IPath>();
        _fileSystem.Path.Returns(_mockPath);
        _fileSystem.RootDirectory.Returns("C:\\test");
        _fileSystem.GoalsPath.Returns("C:\\test");
        _mockPath.DirectorySeparatorChar.Returns('\\');
    }

    #region IsTemplateFile tests

    [Test]
    public async Task IsTemplateFile_ValidPath_ReturnsTrue()
    {
        var result = PathHelper.IsTemplateFile("folder/file.txt");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsTemplateFile_NullInput_ReturnsFalse()
    {
        var result = PathHelper.IsTemplateFile(null!);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_EmptyString_ReturnsFalse()
    {
        var result = PathHelper.IsTemplateFile("");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_WhitespaceOnly_ReturnsFalse()
    {
        var result = PathHelper.IsTemplateFile("   ");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_WithNewlines_ReturnsFalse()
    {
        var result = PathHelper.IsTemplateFile("file\nname.txt");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_TooLong_ReturnsFalse()
    {
        var longPath = new string('a', 300) + ".txt";
        var result = PathHelper.IsTemplateFile(longPath);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_NoExtension_ReturnsFalse()
    {
        var result = PathHelper.IsTemplateFile("filenoextension");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_ExtensionTooLong_ReturnsFalse()
    {
        var result = PathHelper.IsTemplateFile("file.verylongextension");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsTemplateFile_ValidHtmlPath_ReturnsTrue()
    {
        var result = PathHelper.IsTemplateFile("templates/page.html");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsTemplateFile_ValidCsPath_ReturnsTrue()
    {
        var result = PathHelper.IsTemplateFile("src/Program.cs");
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region GetPath tests

    [Test]
    public async Task GetPath_NullPath_ReturnsGoalsPath()
    {
        _mockPath.GetFullPath(Arg.Any<string>()).Returns(x => x.Arg<string>());

        var result = PathHelper.GetPath(null, _fileSystem, null);

        await Assert.That(result).IsEqualTo("C:\\test");
    }

    [Test]
    public async Task GetPath_EmptyPath_ReturnsGoalsPath()
    {
        _mockPath.GetFullPath(Arg.Any<string>()).Returns(x => x.Arg<string>());

        var result = PathHelper.GetPath("", _fileSystem, null);

        await Assert.That(result).IsEqualTo("C:\\test");
    }

    [Test]
    public async Task GetPath_NullGoal_UsesRootDirectory()
    {
        _mockPath.GetFullPath(Arg.Any<string>()).Returns("C:\\test\\file.txt");
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>()).Returns("C:\\test\\file.txt");

        var result = PathHelper.GetPath("file.txt", _fileSystem, null);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task GetPath_WithGoal_UsesGoalFolder()
    {
        var goal = CreateTestGoal();
        goal.AbsoluteGoalFolderPath = "C:\\test\\mygoal";

        _mockPath.GetFullPath(Arg.Any<string>()).Returns("C:\\test\\mygoal\\file.txt");
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>()).Returns("C:\\test\\mygoal\\file.txt");

        var result = PathHelper.GetPath("file.txt", _fileSystem, goal);

        await Assert.That(result).Contains("mygoal");
    }

    [Test]
    public async Task GetPath_GoalWithNullAbsolutePath_UsesRootDirectory()
    {
        var goal = CreateTestGoal();
        goal.AbsoluteGoalFolderPath = null!;

        _mockPath.GetFullPath(Arg.Any<string>()).Returns("C:\\test\\file.txt");
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>()).Returns("C:\\test\\file.txt");

        var result = PathHelper.GetPath("file.txt", _fileSystem, goal);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task GetPath_AbsoluteWindowsPath_ReturnsAsIs()
    {
        _mockPath.IsPathRooted("C:\\absolute\\path.txt").Returns(true);

        var result = PathHelper.GetPath("C:\\absolute\\path.txt", _fileSystem, null);

        await Assert.That(result).IsEqualTo("C:\\absolute\\path.txt");
    }

    [Test]
    public async Task GetPath_PathStartingWithSeparator_JoinsWithRoot()
    {
        _mockPath.GetFullPath(Arg.Any<string>()).Returns("C:\\test\\subfolder\\file.txt");
        _mockPath.Join(Arg.Any<string>(), Arg.Any<string>()).Returns("C:\\test\\subfolder\\file.txt");

        var result = PathHelper.GetPath("\\subfolder\\file.txt", _fileSystem, null);

        await Assert.That(result).IsNotNull();
    }

    #endregion

    #region JoinRootWithPath tests

    [Test]
    public async Task JoinRootWithPath_PathAlreadyStartsWithRoot_ReturnsPath()
    {
        var result = PathHelper.JoinRootWithPath(_fileSystem, "C:\\test", "C:\\test\\file.txt");

        await Assert.That(result).IsEqualTo("C:\\test\\file.txt");
    }

    [Test]
    public async Task JoinRootWithPath_PathDoesNotStartWithRoot_JoinsPaths()
    {
        _mockPath.Join("C:\\test", "file.txt").Returns("C:\\test\\file.txt");

        var result = PathHelper.JoinRootWithPath(_fileSystem, "C:\\test", "file.txt");

        await Assert.That(result).IsEqualTo("C:\\test\\file.txt");
    }

    #endregion

    private Goal CreateTestGoal()
    {
        return new Goal
        {
            GoalName = "TestGoal",
            GoalSteps = new List<GoalStep>(),
            AbsoluteGoalFolderPath = "C:\\test\\TestGoal",
            RelativeGoalFolderPath = "\\TestGoal",
            Injections = new List<Injections>()
        };
    }
}
