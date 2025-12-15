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
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

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

			LoadOpenAI();

			builder = new Builder(programFactory);
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new Builder(programFactory);
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}
	

		[DataTestMethod]
		[DataRow("system: determine sentiment of user input. \nuser:This is awesome, scheme: {sentiment:negative|neutral|positive}")]
		public async Task AskLLM_JsonSchemeInReponse_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("AskLlm", gf.Name);
			Assert.AreEqual("promptMessages", gf.Parameters[0].Name);
			Assert.AreEqual("scheme", gf.Parameters[1].Name);
			Assert.AreEqual("{sentiment:negative|neutral|positive}", gf.Parameters[1].Value);

		}


		[DataTestMethod]
		[DataRow("system: get first name and last name from user request. \nuser:Andy Bernard, write to %firstName%, %lastName%")]
		public async Task AskLLM_VariableInReponse_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("AskLlm", gf.Name);
			Assert.AreEqual("promptMessages", gf.Parameters[0].Name);
			Assert.AreEqual("scheme", gf.Parameters[1].Name);
			Assert.AreEqual("firstName", gf.ReturnValues[0].VariableName);
			Assert.AreEqual("lastName", gf.ReturnValues[1].VariableName);
			

		}


	}
}