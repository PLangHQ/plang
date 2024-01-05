using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.LlmModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		BaseBuilder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var llmService = new OpenAiService(settings, logger, cacheHelper, context);

			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.LlmModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper);

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.LlmModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("system: determine sentiment of user input. \nuser:This is awesome, scheme: {sentiment:negative|neutral|positive}")]
		public async Task AskLLM_JsonSchemeInReponse_Test(string text)
		{
			SetupResponse(text);

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);

			Assert.AreEqual("AskLlm", gf.FunctionName);
			Assert.AreEqual("system", gf.Parameters[0].Name);
			Assert.AreEqual("determine sentiment of user input.", gf.Parameters[0].Value);
			Assert.AreEqual("user", gf.Parameters[1].Name);
			Assert.AreEqual("This is awesome", gf.Parameters[1].Value);
			Assert.AreEqual("scheme", gf.Parameters[2].Name);
			Assert.AreEqual("{sentiment:negative|neutral|positive}", gf.Parameters[2].Value);

		}


		[DataTestMethod]
		[DataRow("system: get first name and last name from user request. \nuser:Andy Bernard, write to %firstName%, %lastName%")]
		public async Task AskLLM_VariableInReponse_Test(string text)
		{
			SetupResponse(text);

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);

			Assert.AreEqual("AskLlm", gf.FunctionName);
			Assert.AreEqual("system", gf.Parameters[0].Name);
			Assert.AreEqual("get first name and last name from user request.", gf.Parameters[0].Value);
			Assert.AreEqual("user", gf.Parameters[1].Name);
			Assert.AreEqual("Andy Bernard", gf.Parameters[1].Value);
			Assert.AreEqual("scheme", gf.Parameters[3].Name);
			Assert.AreEqual("{firstName:string, lastName:string}", gf.Parameters[3].Value);

		}


	}
}