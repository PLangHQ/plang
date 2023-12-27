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

namespace PLang.Modules.CryptographicModule.Tests
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

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CryptographicModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CryptographicModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("hash %password%, write to %password%")]
		public async Task HashUsingBCrypt_Test(string text)
		{
			string response = @"{""FunctionName"": ""HashUsingBCrypt"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""input"",
""Value"": ""%password%""}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""%password%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("HashUsingBCrypt", gf.FunctionName);
			Assert.AreEqual("input", gf.Parameters[0].Name);
			Assert.AreEqual("%password%", gf.Parameters[0].Value);
			Assert.AreEqual("%password%", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("verify %password% matches %HashedPassword%, write to %isPasswordMatch%")]
		public async Task VerifyHashedBCrypt_Test(string text)
		{
			string response = @"{""FunctionName"": ""VerifyHashedBCrypt"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""password"",
""Value"": ""%password%""},
{""Type"": ""String"",
""Name"": ""passwordHash"",
""Value"": ""%HashedPassword%""}],
""ReturnValue"": {""Type"": ""Boolean"",
""VariableName"": ""isPasswordMatch""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("VerifyHashedBCrypt", gf.FunctionName);
			Assert.AreEqual("password", gf.Parameters[0].Name);
			Assert.AreEqual("%password%", gf.Parameters[0].Value);
			Assert.AreEqual("passwordHash", gf.Parameters[1].Name);
			Assert.AreEqual("%HashedPassword%", gf.Parameters[1].Value);
			Assert.AreEqual("isPasswordMatch", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("validate bearer %token%, write to %isValidToken%")]
		public async Task ValidateBearerToken_Test(string text)
		{
			string response = @"{""FunctionName"": ""ValidateBearerToken"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""token"",
""Value"": ""%token%""}],
""ReturnValue"": {""Type"": ""Boolean"",
""VariableName"": ""isValidToken""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("ValidateBearerToken", gf.FunctionName);
			Assert.AreEqual("token", gf.Parameters[0].Name);
			Assert.AreEqual("%token%", gf.Parameters[0].Value);
			Assert.AreEqual("isValidToken", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("generate bearer, %email%, valid for 15 minutes, write to %token%")]
		public async Task GenerateBearerToken_Test(string text)
		{
			string response = @"{""FunctionName"": ""GenerateBearerToken"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""uniqueString"",
""Value"": ""%email%""},
{""Type"": ""Int32"",
""Name"": ""expireTimeInSeconds"",
""Value"": 900}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""%token%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GenerateBearerToken", gf.FunctionName);
			Assert.AreEqual("uniqueString", gf.Parameters[0].Name);
			Assert.AreEqual("%email%", gf.Parameters[0].Value);
			Assert.AreEqual("expireTimeInSeconds", gf.Parameters[1].Name);
			Assert.AreEqual((long) 900, gf.Parameters[1].Value);
			Assert.AreEqual("%token%", gf.ReturnValue.VariableName);

		}
	}
}