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

			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context);

			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.LlmModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p =>
			{
				return JsonConvert.DeserializeObject(response, type);
			});

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.LlmModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("system: determine sentiment of user input. \nuser:This is awesome, scheme: {sentiment:negative|neutral|positive}")]
		public async Task AskLLM_JsonSchemeInReponse_Test(string text)
		{
			string response = @"{""FunctionName"": ""AskLlm"",
""Parameters"": [
    {""Type"": ""string"", ""Name"": ""system"", ""Value"": ""determine sentiment of user input.""},
    {""Type"": ""string"", ""Name"": ""assistant"", ""Value"": """"},
    {""Type"": ""string"", ""Name"": ""user"", ""Value"": ""This is awesome""},
    {""Type"": ""string"", ""Name"": ""scheme"", ""Value"": ""{sentiment:negative|neutral|positive}""}
]}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AskLlm", gf.FunctionName);
			Assert.AreEqual("system", gf.Parameters[0].Name);
			Assert.AreEqual("determine sentiment of user input.", gf.Parameters[0].Value);
			Assert.AreEqual("assistant", gf.Parameters[1].Name);
			Assert.AreEqual("", gf.Parameters[1].Value);
			Assert.AreEqual("user", gf.Parameters[2].Name);
			Assert.AreEqual("This is awesome", gf.Parameters[2].Value);
			Assert.AreEqual("scheme", gf.Parameters[3].Name);
			Assert.AreEqual("{sentiment:negative|neutral|positive}", gf.Parameters[3].Value);

		}


		[DataTestMethod]
		[DataRow("system: get first name and last name from user request. \nuser:Andy Bernard, write to %firstName%, %lastName%")]
		public async Task AskLLM_VariableInReponse_Test(string text)
		{
			string response = @"{""FunctionName"": ""AskLlm"",
""Parameters"": [
    {""Type"": ""string"", ""Name"": ""system"", ""Value"": ""get first name and last name from user request.""},
    {""Type"": ""string"", ""Name"": ""assistant"", ""Value"": """"},
    {""Type"": ""string"", ""Name"": ""user"", ""Value"": ""Andy Bernard""},
    {""Type"": ""string"", ""Name"": ""scheme"", ""Value"": ""{firstName:string, lastName:string}""}
],
""ReturnValue"": {""Type"": ""void"", ""VariableName"": null}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AskLlm", gf.FunctionName);
			Assert.AreEqual("system", gf.Parameters[0].Name);
			Assert.AreEqual("get first name and last name from user request.", gf.Parameters[0].Value);
			Assert.AreEqual("assistant", gf.Parameters[1].Name);
			Assert.AreEqual("", gf.Parameters[1].Value);
			Assert.AreEqual("user", gf.Parameters[2].Name);
			Assert.AreEqual("Andy Bernard", gf.Parameters[2].Value);
			Assert.AreEqual("scheme", gf.Parameters[3].Name);
			Assert.AreEqual("{firstName:string, lastName:string}", gf.Parameters[3].Value);

		}


	}
}