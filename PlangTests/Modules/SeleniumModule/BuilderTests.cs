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

namespace PLang.Modules.WebCrawlerModule.Tests
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
			var aiService = new PLangLlmService(cacheHelper, context, fileSystem, settingsRepository, outputStream);
			
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.SeleniumModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.SeleniumModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow(@"- open example.org, use user session")]
		public async Task NavigateUrl_Test(string text)
		{
			string response = @"{""FunctionName"": ""NavigateToUrl"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""url"",
""Value"": ""example.org""},
{""Type"": ""Boolean"",
""Name"": ""useUserSession"",
""Value"": true}]}";
			

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			
			
			Assert.AreEqual("NavigateToUrl", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("example.org", gf.Parameters[0].Value);
			Assert.AreEqual("useUserSession", gf.Parameters[1].Name);
			Assert.AreEqual(true, gf.Parameters[1].Value);
		}

		[DataTestMethod]
		[DataRow(@"- input %name% into #name")]
		public async Task Input_Test(string text)
		{
			string response = @"{""FunctionName"": ""Input"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""cssSelector"",
""Value"": ""#name""},
{""Type"": ""string"",
""Name"": ""value"",
""Value"": ""%name%""}]}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);


			Assert.AreEqual("Input", gf.FunctionName);
			Assert.AreEqual("cssSelector", gf.Parameters[0].Name);
			Assert.AreEqual("#name", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%name%", gf.Parameters[1].Value);
		}


	}
}