using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;

namespace PLang.Utils.Tests;

[TestClass]
public class PathHelperTests
{
    private IPLangFileSystem fileSystem;
    private Goal goal;

    [TestInitialize]
    public void Setup()
    {
        fileSystem = Substitute.For<IPLangFileSystem>();
        fileSystem.GoalsPath.Returns(@"c:\plang\apps\MyApp\");
    }


    [TestMethod]
    public void GetPathTests()
    {
        goal = new Goal();
        goal.AbsoluteGoalFolderPath = @"c:\plang\apps\MyApp\MyGoal";

        var path = PathHelper.GetPath("file.txt", fileSystem, goal);
        Assert.AreEqual(@"c:\plang\apps\MyApp\MyGoal\file.txt", path);


        path = PathHelper.GetPath("/file.txt", fileSystem, goal);
        Assert.AreEqual(@"c:\plang\apps\MyApp\file.txt", path);


        path = PathHelper.GetPath("//file.txt", fileSystem, goal);
        Assert.AreEqual(@"c:\file.txt", path);

        path = PathHelper.GetPath("///shared/file.txt", fileSystem, goal);
        Assert.AreEqual(@"\\shared\file.txt", path);


        goal = new Goal();
        goal.AbsoluteGoalFolderPath = @"c:\plang\apps\MyApp\";

        path = PathHelper.GetPath("template/file.txt", fileSystem, goal);
        Assert.AreEqual(@"c:\plang\apps\MyApp\template\file.txt", path);


        path = PathHelper.GetPath("/template/file.txt", fileSystem, goal);
        Assert.AreEqual(@"c:\plang\apps\MyApp\template\file.txt", path);

        path = PathHelper.GetPath("/template/file.txt", fileSystem, null);
        Assert.AreEqual(@"c:\plang\apps\MyApp\template\file.txt", path);
    }
}