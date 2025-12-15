using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethereum.Hex.HexConvertors;
using PLangTests;
using PLangTests.Helpers;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

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
			var fileSystem = new PLangMockFileSystem();
			var content = PrReaderHelper.GetPrFileRaw("Start.pr");
			fileSystem.AddFile(fileSystem.GoalsPath, new MockFileData(content));

			var serviceContainer = CreateServiceContainer();

			engine.Init(serviceContainer);
			engine.AddContext("Test", true);
			Assert.AreEqual(true, engine.GetAppContext()["Test"]);

			var serviceContainer2 = (ServiceContainer) CreateServiceContainer();
			//Make sure that get instance doesnt give previous engine instance
			var engine2 = serviceContainer2.GetInstance<IEngine>();
			engine2.Init(serviceContainer2);
			Assert.IsFalse(engine2.GetAppContext().ContainsKey("Test"));

		}

		
	}
}