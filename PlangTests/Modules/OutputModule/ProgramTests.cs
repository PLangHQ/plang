using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.OutputModule;
using NSubstitute;
using PLang.Services.OutputStream;

namespace PLangTests.Modules.OutputModule
{
    [TestClass]
	public class ProgramTests : BasePLangTest
	{
		[TestInitialize]
		public void Init() {
			base.Initialize();

		}

		[TestMethod]
		public async Task Ask_Test()
		{
			outputStream.Ask(Arg.Any<string>()).Returns("good");
			var p = new Program(outputStreamFactory);
			var result = await p.Ask("Hello, how are your?");

			Assert.AreEqual("good", result);
		}

		[TestMethod]
		public async Task Write_Test()
		{			
			var p = new Program(outputStreamFactory);
			await p.Write("Hello, how are your?");

			await outputStream.Received(1).Write(Arg.Any<object>());
		}

	}
}
