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
using PLangTests.Utils;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.MessageModule.Tests
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

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.MessageModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper, logger);

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.MessageModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper, logger);
		}

		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.MessageModule";
			return step;
		}



		[DataTestMethod]
		[DataRow("Get message public key, write to %publicKey%")]
		public async Task GetPublicKey_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("GetPublicKey", gf.FunctionName);
			Assert.AreEqual("publicKey", gf.ReturnValue[0].VariableName);


		}


		[DataTestMethod]
		[DataRow("Set current account to Default")]
		public async Task SetCurrentAccount_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("SetCurrentAccount", gf.FunctionName);
			Assert.AreEqual("publicKeyOrName", gf.Parameters[0].Name);
			Assert.AreEqual("Default", gf.Parameters[0].Value);
		}

		[DataTestMethod]
		[DataRow("Listen for new message, call !Process.Message %message%")]
		public async Task ListenToNewMessages_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Listen", gf.FunctionName);
			Assert.AreEqual("goalName", gf.Parameters[0].Name);
			Assert.AreEqual("!Process.Message", gf.Parameters[0].Value);
			Assert.AreEqual("contentVariableName", gf.Parameters[1].Name);
			AssertVar.AreEqual("%message%", gf.Parameters[1].Value);
		}


		[DataTestMethod]
		[DataRow("Send message to me, %message%")]
		public async Task SendMyselfMessage_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;
			
			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("SendPrivateMessageToMyself", gf.FunctionName);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("%message%", gf.Parameters[0].Value);
		}

		[DataTestMethod]
		[DataRow("Send message to %key%, %message%")]
		public async Task SendMessage_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("SendPrivateMessage", gf.FunctionName);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("%message%", gf.Parameters[0].Value);
			Assert.AreEqual("npubReceiverPublicKey", gf.Parameters[1].Name);
			Assert.AreEqual("%key%", gf.Parameters[1].Value);
		}
	}
}