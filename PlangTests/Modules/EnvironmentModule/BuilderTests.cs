using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.EnvironmentModule.Tests
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
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.EnvironmentModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.EnvironmentModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}
		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.EnvironmentModule";
			return step;
		}

		[DataTestMethod]
		[DataRow("set language to icelandic")]
		public async Task SetCultureLanguageCode_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);
			
			Assert.AreEqual("SetCultureLanguageCode", gf.FunctionName);
			Assert.AreEqual("code", gf.Parameters[0].Name);
			Assert.AreEqual("is-IS", gf.Parameters[0].Value);
		}

		[DataTestMethod]
		[DataRow("set ui language to english uk")]
		public async Task SetCultureUILanguageCode_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("SetCultureUILanguageCode", gf.FunctionName);
			Assert.AreEqual("code", gf.Parameters[0].Name);
			Assert.AreEqual("en-GB", gf.Parameters[0].Value);
		}


	}
}