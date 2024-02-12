using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLangTests;
using PLangTests.Helpers;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;

namespace PLang.Runtime.Tests
{
	[TestClass()]
	public class EngineTests : BasePLangTest
	{
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
		}

		[TestMethod()]
		public void EngineTestNewInstance_ShouldNotShareSameMemory()
		{
			var context = new Dictionary<string, object>();
			context.Add("Test", 1);

			var fileSystem = new PLangMockFileSystem();
			var content = PrReaderHelper.GetPrFileRaw("Start.pr");
			fileSystem.AddFile(fileSystem.GoalsPath, new MockFileData(content));

			var serviceContainer = CreateServiceContainer();

			var engine = new Runtime.Engine();
			engine.Init(serviceContainer);
			engine.AddContext("Test", true);
			Assert.AreEqual(true, engine.GetContext()["Test"]);

			var serviceContainer2 = CreateServiceContainer();
			//Make sure that get instance doesnt give previous engine instance
			var engine2 = new Runtime.Engine();
			engine2.Init(serviceContainer2);
			Assert.IsFalse(engine2.GetContext().ContainsKey("Test"));

		}

		
	}
}