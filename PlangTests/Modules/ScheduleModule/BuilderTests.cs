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

namespace PLang.Modules.ScheduleModule.Tests
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

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}




		[DataTestMethod]
		[DataRow("wait for 1 sec")]
		public async Task Sleep_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("Sleep", gf.Name);
			Assert.AreEqual("sleepTimeInMilliseconds", gf.Parameters[0].Name);
			Assert.AreEqual((long) 1000, gf.Parameters[0].Value);

		}



		[DataTestMethod]
		[DataRow("run !Process.File on mondays at 11 am")]
		public async Task Schedule_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("Schedule", gf.Name);
			Assert.AreEqual("cronCommand", gf.Parameters[0].Name);
			Assert.AreEqual("0 11 * * 1", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process.File", gf.Parameters[1].Value);

		}

	}
}