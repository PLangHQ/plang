using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Events;
using PLang.Building.Parsers;

using PLang.Repository;
using PLang.Runtime;
using PLang.Utils;
using PLangTests;
using PLangTests.Mocks;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PLangTests.Helpers;
using System.ComponentModel.Design;

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