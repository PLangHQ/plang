using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Utils;
using PLang.Modules.OutputModule;
using NSubstitute;

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
			var outputStream = NSubstitute.Substitute.For<IOutputStream>();
			outputStream.Ask(Arg.Any<string>()).Returns("good");
			var p = new Program(outputStream);
			var result = await p.Ask("Hello, how are your?");

			Assert.AreEqual("good", result);
		}

		[TestMethod]
		public async Task Write_Test()
		{
			var outputStream = NSubstitute.Substitute.For<IOutputStream>();
			
			var p = new Program(outputStream);
			await p.Write("Hello, how are your?");

			await outputStream.Received(1).Write(Arg.Any<object>());
		}

	}
}
