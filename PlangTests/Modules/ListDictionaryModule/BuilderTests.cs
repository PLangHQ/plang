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

namespace PLang.Modules.ListDictionaryModule.Tests
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
		[DataRow("remove %item% from '%producDict%' dictionay")]
		public async Task DeleteKeyFromDictionaryy_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;
			
			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("DeleteKeyFromDictionary", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("%item%", gf.Parameters[0].Value);
			Assert.AreEqual("dictionary", gf.Parameters[1].Name);
			Assert.AreEqual("%producDict%", gf.Parameters[1].Value);

		}

		[DataTestMethod]
		[DataRow("add %item% to %productList%")]
		public async Task AddToList_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("AddToList", gf.Name);
			Assert.AreEqual("value", gf.Parameters[0].Name);
			Assert.AreEqual("%item%", gf.Parameters[0].Value);
			Assert.AreEqual("listInstance", gf.Parameters[1].Name);
			Assert.AreEqual("%productList%", gf.Parameters[1].Value);

		}

		

		[DataTestMethod]
		[DataRow("add %productId%, %item% to dictionary %products%")]
		public async Task AddToDictionary_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("AddToDictionary", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("%productId%", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%item%", gf.Parameters[1].Value);
			Assert.AreEqual("dictionaryInstance", gf.Parameters[2].Name);
			Assert.AreEqual("%products%", gf.Parameters[2].Value);

		}

	}
}