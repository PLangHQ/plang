using System.IO.Abstractions.TestingHelpers;
using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils;
using PLangTests;
using PLangTests.Helpers;
using PLangTests.Mocks;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Building.Parsers.Tests;

[TestClass]
public class PrParserTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }

    [TestMethod]
    public void LoadAllGoalsTest()
    {
        var settings = container.GetInstance<ISettings>();
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();

        var start = PrReaderHelper.GetPrFileRaw("Start.pr");
        var helloWorld = PrReaderHelper.GetPrFileRaw("HelloWorld.pr");
        var someApp = PrReaderHelper.GetPrFileRaw("SomeApp.pr");

        // this is build file belonging to the app user is creating
        fileSystem.AddFile(Path.Join(fileSystem.BuildPath, ISettings.GoalFileName), new MockFileData(start));

        // apps that he has installed
        fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "SomeApp", ".build", ISettings.GoalFileName),
            new MockFileData(someApp));
        fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", ISettings.GoalFileName),
            new MockFileData(helloWorld));

        prParser.ForceLoadAllGoals();
        var goals = prParser.GetAllGoals();

        Assert.AreEqual(@"\", goals[0].AppName);
        Assert.AreEqual(@"\", goals[0].RelativeAppStartupFolderPath);
        Assert.AreEqual(@"\", goals[0].RelativeGoalFolderPath);
        Assert.AreEqual(@"\Start.goal", goals[0].RelativeGoalPath);
        Assert.AreEqual(Path.Join(fileSystem.GoalsPath, ".build", "Start", ISettings.GoalFileName),
            goals[0].AbsolutePrFilePath);


        Assert.AreEqual("HelloWorld", goals[1].AppName);
        Assert.AreEqual(@"\apps\HelloWorld", goals[1].RelativeAppStartupFolderPath);
        Assert.AreEqual(@"\apps\HelloWorld", goals[1].RelativeGoalFolderPath);
        Assert.AreEqual(@"\apps\HelloWorld\HelloWorld.goal", goals[1].RelativeGoalPath);
        Assert.AreEqual(
            Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "HelloWorld", ISettings.GoalFileName),
            goals[1].AbsolutePrFilePath);
    }


    [TestMethod]
    public void GetGoalByAppAndGoalNameTest()
    {
        var settings = container.GetInstance<ISettings>();
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();

        var start = PrReaderHelper.GetPrFileRaw("Start.pr");
        var helloWorld = PrReaderHelper.GetPrFileRaw("HelloWorld.pr");
        var someApp = PrReaderHelper.GetPrFileRaw("SomeApp.pr");
        var todos = PrReaderHelper.GetPrFileRaw("ui/Todos.pr");

        // this is build file belonging to the app user is creating
        fileSystem.AddFile(Path.Join(fileSystem.BuildPath, ISettings.GoalFileName), new MockFileData(start));
        fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "ui", "todos", ISettings.GoalFileName),
            new MockFileData(todos));

        // apps that he has installed
        fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "SomeApp", ".build", ISettings.GoalFileName),
            new MockFileData(someApp));
        fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", ISettings.GoalFileName),
            new MockFileData(helloWorld));

        prParser.ForceLoadAllGoals();

        var goal = prParser.GetGoalByAppAndGoalName(Path.DirectorySeparatorChar.ToString(), "Start.goal");
        Assert.IsNotNull(goal);

        goal = prParser.GetGoalByAppAndGoalName(Path.DirectorySeparatorChar.ToString(), "Start");
        Assert.IsNotNull(goal);

        var todoGoal = prParser.GetGoalByAppAndGoalName(Path.DirectorySeparatorChar.ToString(), "Todos");
        Assert.IsNotNull(todoGoal);

        todoGoal = prParser.GetGoalByAppAndGoalName(Path.DirectorySeparatorChar.ToString(), "ui/todos");
        Assert.IsNotNull(todoGoal);

        var goals = prParser.GetAllGoals();

        var goal2 = prParser.GetGoalByAppAndGoalName(Path.DirectorySeparatorChar.ToString(), "apps/SomeApp/SomeApp",
            goal);
        Assert.IsNotNull(goal2);

        var goal3 = prParser.GetGoalByAppAndGoalName("/apps/SomeApp".AdjustPathToOs(), "apps/SomeApp/SomeApp", goal);
        Assert.IsNotNull(goal3);
    }


    [TestMethod]
    public void ParsePrFile_NullGoalTest()
    {
        // Arrange
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();
        var appAbsoluteStartupPath = "mockPath";
        var absolutePrFilePath = "mockPrFilePath";

        // In our mock file system, the provided absolutePrFilePath contains an invalid or empty JSON, 
        // so that the ParseFilePath method would return null.
        fileSystem.AddFile(absolutePrFilePath, new MockFileData("Invalid JSON"));

        // Act
        var result = prParser.ParsePrFile(absolutePrFilePath);

        // Assert
        Assert.IsNull(result, "Expected null when parsing an invalid goal file.");
    }


    [TestMethod]
    public void ParseInstructionFile_NewInstructionTest()
    {
        // Arrange
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();
        var separator = Path.DirectorySeparatorChar.ToString();
        var absolutePrFilePath = $"{separator}rootDirectory{separator}instruction.pr";

        // Mock a GoalStep which will be used as input to the method.
        var step = new GoalStep
        {
            AbsolutePrFilePath = absolutePrFilePath
        };
        fileSystem.AddStep(step);

        object actionMock = new { Hello = true };
        fileSystem.AddInstruction(step.AbsolutePrFilePath, new Instruction(actionMock));

        // Mock an Instruction.

        var existingInstruction = new Instruction(actionMock);

        // Act
        var result = prParser.ParseInstructionFile(step);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(((dynamic)existingInstruction.Action).Hello,
            ((JValue)((dynamic)result.Action).Hello).Value<bool>());
    }


    [TestMethod]
    public void LoadAllGoals_LoadAndFilterTest()
    {
        // Arrange
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();
        var settings = container.GetInstance<ISettings>();

        // Mock goal files in the .build folder with both public and private goals.
        var publicGoalData = "{ \"Visibility\": 0 }"; // 0 for public visibility.
        var privateGoalData = "{ \"Visibility\": 1 }"; // 1 for private visibility.

        var publicGoalPath =
            $"{fileSystem.GoalsPath}{Path.DirectorySeparatorChar}.build{Path.DirectorySeparatorChar}AnalyzeFile{Path.DirectorySeparatorChar}00. Goal.pr";
        var privateGoalPath =
            $"{fileSystem.GoalsPath}{Path.DirectorySeparatorChar}.build{Path.DirectorySeparatorChar}AnalyzeFile{Path.DirectorySeparatorChar}ProcessFile{Path.DirectorySeparatorChar}00. Goal.pr";

        fileSystem.AddFile(publicGoalPath, new MockFileData(publicGoalData));
        fileSystem.AddFile(privateGoalPath, new MockFileData(privateGoalData));

        // Act
        prParser.ForceLoadAllGoals();
        var allGoals = prParser.GetAllGoals();
        var publicGoals = prParser.GetPublicGoals();

        // Assert
        Assert.AreEqual(2, allGoals.Count); // There should be 2 goals in total.
        Assert.AreEqual(1, publicGoals.Count); // Only 1 of the goals is public (based on its visibility property).

        // Additional check to ensure the public goal in the `publicGoals` list has the correct visibility.
        Assert.AreEqual(Visibility.Public, publicGoals.First().Visibility);
    }


    [TestMethod]
    public void GetAllGoals_ExistingGoalsTest()
    {
        // Arrange
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();
        var settings = container.GetInstance<ISettings>();

        var path1 = Path.Join(fileSystem.BuildPath, "start", ISettings.GoalFileName);
        var path2 = Path.Join(fileSystem.BuildPath, "main", ISettings.GoalFileName);
        // Create some mock goals.
        var mockGoal1 = new Goal
            { AbsolutePrFilePath = path1, RelativePrPath = "\\.build\\start\\" + ISettings.GoalFileName };
        var mockGoal2 = new Goal
            { AbsolutePrFilePath = path2, RelativePrPath = "\\.build\\main\\" + ISettings.GoalFileName };
        fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "start", ISettings.GoalFileName),
            new MockFileData(JsonConvert.SerializeObject(mockGoal1)));
        fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "main", ISettings.GoalFileName),
            new MockFileData(JsonConvert.SerializeObject(mockGoal2)));

        var prParser = container.GetInstance<PrParser>();
        prParser.ForceLoadAllGoals();
        // Act
        var retrievedGoals = prParser.GetAllGoals();

        // Assert
        Assert.AreEqual(2, retrievedGoals.Count);
        Assert.AreEqual(mockGoal1.AbsolutePrFilePath, retrievedGoals[0].AbsolutePrFilePath);
        Assert.AreEqual(mockGoal2.AbsolutePrFilePath, retrievedGoals[1].AbsolutePrFilePath);
    }

    [TestMethod]
    public async Task GetApps_Test()
    {
        var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();
        var settings = container.GetInstance<ISettings>();

        var relativeApp1 = Path.Join("apps", "App1", ".build", "Process1", ISettings.GoalFileName);
        var relativeApp2 = Path.Join("apps", "App2", ".build", "Process2", ISettings.GoalFileName);
        var relativeApp3 = Path.Join("apps", "App2", ".build", "Process2", "ProcessFile", ISettings.GoalFileName);
        var relativeGoal = Path.Join(".build", "Hello", ISettings.GoalFileName);
        var path1 = Path.Join(fileSystem.GoalsPath, relativeApp1);
        var path2 = Path.Join(fileSystem.GoalsPath, relativeApp2);
        var path3 = Path.Join(fileSystem.GoalsPath, relativeApp3);
        var path4 = Path.Join(fileSystem.GoalsPath, relativeGoal);

        var mockGoal1 = new Goal
            { AppName = "Process1", AbsolutePrFilePath = path1, RelativeAppStartupFolderPath = relativeApp1 };
        var mockGoal2 = new Goal { AppName = "Process2", AbsolutePrFilePath = path2, RelativePrPath = relativeApp2 };
        var mockGoal3 = new Goal { AppName = "ProcessFile", AbsolutePrFilePath = path3, RelativePrPath = relativeApp3 };
        var mockGoal4 = new Goal { AppName = "Hello", AbsolutePrFilePath = path4, RelativePrPath = relativeGoal };

        fileSystem.AddFile(path1, new MockFileData(JsonConvert.SerializeObject(mockGoal1)));
        fileSystem.AddFile(path2, new MockFileData(JsonConvert.SerializeObject(mockGoal2)));
        fileSystem.AddFile(path3, new MockFileData(JsonConvert.SerializeObject(mockGoal3)));
        fileSystem.AddFile(path4, new MockFileData(JsonConvert.SerializeObject(mockGoal4)));

        var prParser = container.GetInstance<PrParser>();
        prParser.ForceLoadAllGoals();

        var retrievedGoals = prParser.GetApps();

        Assert.AreEqual(2, retrievedGoals.Count);
    }
}