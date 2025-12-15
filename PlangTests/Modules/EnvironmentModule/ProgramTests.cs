using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.EnvironmentModule;
using System.Globalization;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLangTests.Modules.EnvironmentModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{

		[TestInitialize] 
		public void Init() {
			base.Initialize();
		}

		[TestMethod]
		public async Task Test_SetCultureLanguageCode()
		{
			var p = new Program(settings, fileSystem, settingsRepositoryFactory, engine);
			await p.SetCultureLanguageCode("is-IS");

			Assert.AreEqual("is-IS", Thread.CurrentThread.CurrentCulture.Name);
			Assert.AreEqual("is-IS", Thread.CurrentThread.CurrentUICulture.Name);


			await p.SetCultureLanguageCode("fr");
			Assert.AreEqual("fr", Thread.CurrentThread.CurrentCulture.Name);
			Assert.AreEqual("fr", Thread.CurrentThread.CurrentUICulture.Name);

			await p.SetCultureUILanguageCode("en-GB");
			Assert.AreEqual("en-GB", Thread.CurrentThread.CurrentUICulture.Name);
		}



	}
}
