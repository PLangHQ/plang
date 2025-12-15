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
using PLang.Modules.LocalOrGlobalVariableModule;
using static PLang.Modules.BaseBuilder;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLang.Modules.LocalOrGlobalVariableModule.Tests
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
		[DataRow("set 'name' as %name%")]
		public async Task SetVariable_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);		
			 
			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("SetVariable", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%name%", gf.Parameters[1].Value);

		}



		[DataTestMethod]
		[DataRow("set static 'name' as %name%")]
		public async Task SetStaticVariable_Test(string text)
		{

			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("SetStaticVariable", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%name%", gf.Parameters[1].Value);

		}


		[DataTestMethod]
		[DataRow("get 'name' var into %name%")]
		public async Task GetVariable_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GetVariable", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("name", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("get static 'name' var into %name%")]
		public async Task GetStaticVariable_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GetStaticVariable", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("name", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("remove variable 'name'")]
		public async Task RemoveVariable_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("RemoveVariable", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("remove static variable 'name'")]
		public async Task RemoveStaticVariable_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("RemoveStaticVariable", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("listen to variable 'name', call !Process name=%full_name%, %zip%")]
		public async Task OnCreateVariableListener_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("OnCreateVariableListener", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%zip%", dict["zip"]);

		}

		[DataTestMethod]
		[DataRow("listen to change on variable 'name', call !Process name=%full_name%, %phone%")]
		public async Task OnChangeVariableListener_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("OnChangeVariableListener", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[3].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[3].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%phone%", dict["phone"]);

		}

		[DataTestMethod]
		[DataRow("listen to remove on variable 'name', call !Process name=%full_name%, %key%")]
		public async Task OnRemoveVariableListener_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("OnRemoveVariableListener", gf.Name);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%key%", dict["key"]);

		}
	}
}