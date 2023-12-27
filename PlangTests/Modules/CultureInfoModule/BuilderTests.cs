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

namespace PLang.Modules.CultureInfoModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.CultureInfoModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CultureInfoModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("set language to icelandic")]
		public async Task SetCultureLanguageCode_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetCultureLanguageCode"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""code"",
""Value"": ""is""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetCultureLanguageCode", gf.FunctionName);
			Assert.AreEqual("code", gf.Parameters[0].Name);
			Assert.AreEqual("is", gf.Parameters[0].Value);
		}

		[DataTestMethod]
		[DataRow("set ui language to english uk")]
		public async Task SetCultureUILanguageCode_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetCultureUILanguageCode"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""code"",
""Value"": ""en-GB""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetCultureUILanguageCode", gf.FunctionName);
			Assert.AreEqual("code", gf.Parameters[0].Name);
			Assert.AreEqual("en-GB", gf.Parameters[0].Value);
		}


	}
}