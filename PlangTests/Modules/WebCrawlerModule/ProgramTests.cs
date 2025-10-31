using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebCrawlerModule;

namespace PLangTests.Modules.WebCrawlerModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		[TestInitialize]
		public void Init() {
			base.Initialize();
		}

		[TestMethod]
		public async Task GetOpenBrowser()
		{
			var p = new Program(fileSystem, logger, engine, pseudoRuntime, programFactory);
			await p.StartBrowser(headless: true);

			await Task.Delay(1000);

			
		}
		[TestMethod]
		public async Task GetOpenHeadlessBrowser()
		{
			var p = new Program(fileSystem, logger, engine, pseudoRuntime, programFactory);
			await p.StartBrowser(headless: true);

			await Task.Delay(1000);

		}
		[TestMethod]
		public async Task GetOpenBrowserUseUserSession()
		{
			var p = new Program(fileSystem, logger, engine, pseudoRuntime, programFactory);
			await p.NavigateToUrl("https://example.org/", headless: true);

			await Task.Delay(1000);

		}
	}
}
