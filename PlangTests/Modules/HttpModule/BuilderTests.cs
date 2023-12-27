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

namespace PLang.Modules.HttpModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.HttpModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.HttpModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("get http://example.org, write to %json%")]
		public async Task Get_Test(string text)
		{
			string response = @"{""FunctionName"": ""Get"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Get", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}



		[DataTestMethod]
		[DataRow("POST http://example.org, write to %json%")]
		public async Task Post_Test(string text)
		{
			string response = @"{""FunctionName"": ""Post"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Post", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("Patch http://example.org, write to %json%")]
		public async Task Patch_Test(string text)
		{
			string response = @"{""FunctionName"": ""Patch"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Patch", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("delete http://example.org, write to %json%")]
		public async Task Delete_Test(string text)
		{
			string response = @"{""FunctionName"": ""Delete"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Delete", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("put http://example.org, write to %json%")]
		public async Task Put_Test(string text)
		{
			string response = @"{""FunctionName"": ""Put"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Put", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("Head http://example.org, write to %json%")]
		public async Task Head_Test(string text)
		{
			string response = @"{""FunctionName"": ""Head"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Head", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("Options http://example.org, write to %json%")]
		public async Task Options_Test(string text)
		{
			string response = @"{""FunctionName"": ""Options"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""http://example.org""},
{""Type"": ""Object"",
""Name"": ""data"",
""Value"": null},
{""Type"": ""Boolean"",
""Name"": ""signRequest"",
""Value"": false},
{""Type"": ""Dictionary`2"",
""Name"": ""headers"",
""Value"": null},
{""Type"": ""String"",
""Name"": ""encoding"",
""Value"": ""utf-8""},
{""Type"": ""String"",
""Name"": ""contentType"",
""Value"": ""application/json""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""json""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Options", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("json", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("post http://example.org, file=%@file%, write to %json%")]
		public async Task Post_Multipart_Test(string text)
		{
			string response = @"{""FunctionName"": ""PostMultipartFormData"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""url"", ""Value"": ""http://example.org""},
    {""Type"": ""FileStream"", ""Name"": ""data"", ""Value"": ""%@file%""}
],
""ReturnValue"": {""Type"": ""Object"", ""VariableName"": ""%json%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("PostMultipartFormData", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			Assert.AreEqual("%json%", gf.ReturnValue.VariableName);

		}

	}
}