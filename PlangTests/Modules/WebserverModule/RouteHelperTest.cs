using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebserverModule;
using PLang.Utils;
using static PLang.Modules.WebserverModule.Program;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLangTests.Modules.WebserverModule
{
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
			/*List<Routing> routings = new();
			routings.Add(new Routing("/api/*", new Route(".*", new(), null));

			string folderPath = Path.Join(fileSystem.BuildPath, "api/GetUser".AdjustPathToOs());
			fileSystem.AddDirectory(folderPath);
			string url = "/api/GetUser";
			var goalName = RouteHelper.GetGoalPath(fileSystem, memoryStack, logger, url, routings);

			
			Assert.AreEqual(folderPath, goalName);*/
		}

		[TestMethod]
		public async Task TestRoutHelper_WithVariable()
		{
			//string Path, GoalToCall GoalToCall, string? Method = null, string ContentType = "text/html", 
			// Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null
			List<Routing> routings = new();
			//routings.Add(new Routing("/category/%name%", "/category/default"));
			throw new Exception("testing removed");

			string folderPath = Path.Join(fileSystem.BuildPath, "/category/default".AdjustPathToOs());
			fileSystem.AddDirectory(folderPath);
			string url = "/category/Sports";
			var goalName = RouteHelper.GetGoalPath(fileSystem, memoryStack, logger, url, routings);


			Assert.AreEqual(folderPath, goalName);
		}

	}
}
