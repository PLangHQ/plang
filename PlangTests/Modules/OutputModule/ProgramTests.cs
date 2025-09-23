using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Modules.OutputModule;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Messages;
using static PLang.Modules.OutputModule.Program;

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
			throw new Exception("Needs fixing");
		//	outputStream.Ask(Arg.Any<string>()).Returns(new Task<(string, PLang.Errors.IError)>("good", null));
		/*
			var p = new Program(outputStreamFactory, outputSystemStreamFactory, variableHelper, programFactory);
			var result = await p.Ask(new AskOptions("Hello, how are your?"));

			Assert.AreEqual("good", result.Item1);*/
		}

		[TestMethod]
		public async Task Write_Test()
		{			
			var p = new Program(outputStreamFactory, outputSystemStreamFactory, variableHelper, programFactory);
			await p.Write(new PLang.Services.OutputStream.Messages.TextMessage("Hello, how are your?"));

			await outputStream.Received(1).SendAsync(Arg.Any<OutMessage>());
		}

	}
}
