using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Utils;
using PLangTests;
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

			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context);
			
			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder(settings, aiService);
			builder.InitBaseBuilder("PLang.Modules.MessageModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new Builder(settings, aiService);
			builder.InitBaseBuilder("PLang.Modules.MessageModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("Get message public key, write to %publicKey%")]
		public async Task GetPublicKey_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetPublicKey"",
""Parameters"": [],
""ReturnValue"": {""Type"": ""string"",
""VariableName"": ""publicKey""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetPublicKey", gf.FunctionName);
			Assert.AreEqual("publicKey", gf.ReturnValue.VariableName);


		}


		[DataTestMethod]
		[DataRow("Set current account to Default")]
		public async Task SetCurrentAccount_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetCurrentAccount"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""publicKeyOrName"",
""Value"": ""Default""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetCurrentAccount", gf.FunctionName);
			Assert.AreEqual("publicKeyOrName", gf.Parameters[0].Name);
			Assert.AreEqual("Default", gf.Parameters[0].Value);
		}

		[DataTestMethod]
		[DataRow("Listen for new message, call !Process.Message %message%")]
		public async Task ListenToNewMessages_Test(string text)
		{
			string response = @"{""FunctionName"": ""Listen"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""goalName"",
""Value"": ""!Process.Message""},
{""Type"": ""string"",
""Name"": ""variableName"",
""Value"": ""%message%""},
{""Type"": ""Nullable`1"",
""Name"": ""listenFromDateTime"",
""Value"": null}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Listen", gf.FunctionName);
			Assert.AreEqual("goalName", gf.Parameters[0].Name);
			Assert.AreEqual("!Process.Message", gf.Parameters[0].Value);
			Assert.AreEqual("variableName", gf.Parameters[1].Name);
			Assert.AreEqual("%message%", gf.Parameters[1].Value);
			Assert.AreEqual("listenFromDateTime", gf.Parameters[2].Name);
			Assert.AreEqual(null, gf.Parameters[2].Value);
		}


		[DataTestMethod]
		[DataRow("Send message to me, %message%")]
		public async Task SendMyselfMessage_Test(string text)
		{
			string response = @"{""FunctionName"": ""SendPrivateMessageToMyself"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""content"",
""Value"": ""%message%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SendPrivateMessageToMyself", gf.FunctionName);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("%message%", gf.Parameters[0].Value);
		}

		[DataTestMethod]
		[DataRow("Send message to %key%, %message%")]
		public async Task SendMessage_Test(string text)
		{
			string response = @"{""FunctionName"": ""SendPrivateMessage"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""content"",
""Value"": ""%message%""},
{""Type"": ""string"",
""Name"": ""npubReceiverPublicKey"",
""Value"": ""%key%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SendPrivateMessage", gf.FunctionName);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("%message%", gf.Parameters[0].Value);
			Assert.AreEqual("npubReceiverPublicKey", gf.Parameters[1].Name);
			Assert.AreEqual("%key%", gf.Parameters[1].Value);
		}
	}
}