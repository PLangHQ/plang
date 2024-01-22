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

namespace PLang.Modules.WebserverModule.Tests
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
			var aiService = new PLangLlmService(cacheHelper, outputStream, signingService);
			
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.WebserverModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.WebserverModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("start webserver, 8080, [api, user]")]
		public async Task StartWebserver_Test(string text)
		{
			string response = @"{""FunctionName"": ""StartWebserver"",
""Parameters"": [{""Type"": ""Int32"", ""Name"": ""port"", ""Value"": 8080},
               {""Type"": ""List`1"", ""Name"": ""publicPaths"", ""Value"": [""api"", ""user""]}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("StartWebserver", gf.FunctionName);
			Assert.AreEqual("port", gf.Parameters[0].Name);
			Assert.AreEqual((long) 8080, gf.Parameters[0].Value);

			Assert.AreEqual("publicPaths", gf.Parameters[1].Name);

			var paths = JsonConvert.DeserializeObject<string[]>(gf.Parameters[1].Value.ToString());

			Assert.AreEqual("api", paths[0]);
			Assert.AreEqual("user", paths[1]);

		}



		[DataTestMethod]
		[DataRow("get user ip, write to %ip%")]
		public async Task GetUserIp_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetUserIp"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""headerKey"", 
""Value"": null}], 
""ReturnValue"": {""Type"": ""String"", 
""VariableName"": ""ip""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetUserIp", gf.FunctionName);
			Assert.AreEqual("headerKey", gf.Parameters[0].Name);
			Assert.AreEqual(null, gf.Parameters[0].Value);

			Assert.AreEqual("ip", gf.ReturnValue[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("write header 'X-Set-Data' as value 123")]
		public async Task SetHeader_Test(string text)
		{
			string response = @"{""FunctionName"": ""WriteToHeader"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""X-Set-Data""},
{""Type"": ""String"",
""Name"": ""value"",
""Value"": ""123""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("WriteToHeader", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("X-Set-Data", gf.Parameters[0].Value);

			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("123", gf.Parameters[1].Value);

		}



		[DataTestMethod]
		[DataRow("get cache-control header, write to %cacheControl%")]
		public async Task GetRequestHeader_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetRequestHeader"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""key"",
""Value"": ""cache-control""}],
""ReturnValue"": {""Type"": ""string"",
""VariableName"": ""cacheControl""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetRequestHeader", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("cache-control", gf.Parameters[0].Value);

			Assert.AreEqual("cacheControl", gf.ReturnValue[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("get cookie 'TOS', write to %cookieValue%")]
		public async Task GetCookie_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetCookie"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""name"",
""Value"": ""TOS""}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""%cookieValue%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetCookie", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("TOS", gf.Parameters[0].Value);

			Assert.AreEqual("%cookieValue%", gf.ReturnValue[0].VariableName);

		}





		[DataTestMethod]
		[DataRow("set cookie 'service' to 1")]
		public async Task SetCookie_Test(string text)
		{
			string response = @"{""FunctionName"": ""WriteCookie"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""name"",
""Value"": ""service""},
{""Type"": ""String"",
""Name"": ""value"",
""Value"": ""1""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("WriteCookie", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("service", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("1", gf.Parameters[1].Value);

		}



		[DataTestMethod]
		[DataRow("delete cookie 'service'")]
		public async Task DeleteCookie_Test(string text)
		{
			string response = @"{""FunctionName"": ""DeleteCookie"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""name"",
""Value"": ""service""},
{""Type"": ""String"",
""Name"": ""value"",
""Value"": """"}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("DeleteCookie", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("service", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("", gf.Parameters[1].Value);

		}

	}
}