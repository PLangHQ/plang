using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.CultureInfoModule;
using System.Globalization;

namespace PLangTests.Modules.CultureInfoModule
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
			var p = new Program();
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
