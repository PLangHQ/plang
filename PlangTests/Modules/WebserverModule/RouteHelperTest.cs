using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebserverModule;
using PLang.Utils;

namespace PLangTests.Modules.WebserverModule;

[TestClass]
public class RouteHelperTest : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }

    [TestMethod]
    public async Task TestRoutHelper_Star()
    {
        //string Path, GoalToCall GoalToCall, string? Method = null, string ContentType = "text/html", 
        // Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null
        List<GoalRouting> routings = new();
        routings.Add(new GoalRouting("/api/*"));

        var folderPath = Path.Join(fileSystem.BuildPath, "api/GetUser".AdjustPathToOs());
        fileSystem.AddDirectory(folderPath);
        var url = "/api/GetUser";
        var goalName = RouteHelper.GetGoalPath(fileSystem, memoryStack, logger, url, routings);


        Assert.AreEqual(folderPath, goalName);
    }

    [TestMethod]
    public async Task TestRoutHelper_WithVariable()
    {
        //string Path, GoalToCall GoalToCall, string? Method = null, string ContentType = "text/html", 
        // Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null
        List<GoalRouting> routings = new();
        routings.Add(new GoalRouting("/category/%name%", "/category/default"));

        var folderPath = Path.Join(fileSystem.BuildPath, "/category/default".AdjustPathToOs());
        fileSystem.AddDirectory(folderPath);
        var url = "/category/Sports";
        var goalName = RouteHelper.GetGoalPath(fileSystem, memoryStack, logger, url, routings);


        Assert.AreEqual(folderPath, goalName);
    }
}