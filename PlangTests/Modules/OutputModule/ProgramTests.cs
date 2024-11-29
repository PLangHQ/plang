using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.OutputModule;
using NSubstitute;
using PLang.Services.Channels;
using PLang.Services.Channels.Formatters;
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
			
			Dictionary<MessageType, IChannel> channels = new();
			
			var p = new Program(outputStreamFactory, outputSystemStreamFactory, new ChannelManager(channels));
			var result = await p.Ask(new AskProperties()
			{
				Question ="Hello, how are your?"
			});

			Assert.AreEqual("good", result.Answer);
		}

		[TestMethod]
		public async Task Write_Test()
		{			
			Dictionary<MessageType, IChannel> channels = new();
			var p = new Program(outputStreamFactory, outputSystemStreamFactory, new ChannelManager(channels));
			await p.Write("ble", new WriteProperties()
			{
				Data = "Hello, how are your?"
			});

			await outputStream.Received(1).Write(Arg.Any<object>());
		}

	}
}
