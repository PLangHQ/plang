using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Modules.LlmModule;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLangTests.Modules.LlmModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		Program p;
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			
		}

		private void RealOpenAIService()
		{
			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context, fileSystem, settingsRepository, outputStream);

			typeHelper = new TypeHelper(fileSystem, settings);
			p = new Program(aiService, memoryStack, variableHelper);

		}


		private void SetupResponse(string response)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query<string>(Arg.Any<LlmQuestion>()).Returns(p => {
				return response;
			});

			p = new Program(aiService, memoryStack, variableHelper);
		}

		[TestMethod]
		public async Task AskLlm()
		{
			//RealOpenAIService();
			SetupResponse("{\"sentiment\":\"positive\"}");
			/*
			await p.AskLlm2("determine sentiment of user input. ", "", "This is awesome", "{sentiment:negative|neutral|positive}");
			Assert.AreEqual("positive", memoryStack.Get("sentiment"));
			*/

		}

		[TestMethod]
		public async Task AskLlm_WriteToVariables()
		{
			//RealOpenAIService();
			SetupResponse(@"{""firstName"":""Darryl"", ""lastName"":""Philbin"" }");
			/*
			await p.AskLlm("Find first and last name", "", "Darryl Philbin", "{firstName:string, lastName:string}");
			Assert.AreEqual("Darryl", memoryStack.Get("firstName"));
			Assert.AreEqual("Philbin", memoryStack.Get("lastName"));
			*/

		}

	}
}
