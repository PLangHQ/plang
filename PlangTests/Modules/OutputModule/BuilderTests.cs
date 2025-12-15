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

namespace PLang.Modules.OutputModule.Tests
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
		[DataRow("ask, what should the settings be? write to %settings%")]
		public async Task Ask_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("Ask", gf.Name);
			Assert.AreEqual("text", gf.Parameters[0].Name);
			Assert.AreEqual("what should the settings be?", gf.Parameters[0].Value);
			Assert.AreEqual("settings", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("write out 'Hello PLang world'")]
		public async Task Write_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			Assert.AreEqual("Write", gf.Name);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("Hello PLang world", gf.Parameters[0].Value);
			Assert.AreEqual("writeToBuffer", gf.Parameters[1].Name);
			Assert.AreEqual(false, gf.Parameters[1].Value);


		}

	}
}