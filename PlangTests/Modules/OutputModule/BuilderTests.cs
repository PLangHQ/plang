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

namespace PLang.Modules.OutputModule.Tests
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
			var aiService = new PLangLlmService(cacheHelper, outputStream, signingService, logger);
			
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.OutputModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.OutputModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper, logger);
		}



		[DataTestMethod]
		[DataRow("ask, what should the settings be? write to %settings%")]
		public async Task Ask_Test(string text)
		{
			string response = @"{""FunctionName"": ""Ask"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""text"",
""Value"": ""what should the settings be?""}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""settings""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Ask", gf.FunctionName);
			Assert.AreEqual("text", gf.Parameters[0].Name);
			Assert.AreEqual("what should the settings be?", gf.Parameters[0].Value);
			Assert.AreEqual("settings", gf.ReturnValue[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("write out 'Hello PLang world'")]
		public async Task Write_Test(string text)
		{
			string response = @"{""FunctionName"": ""Write"",
""Parameters"": [{""Type"": ""string"",
""Name"": ""text"",
""Value"": ""Hello PLang world""},
{""Type"": ""Boolean"",
""Name"": ""writeToBuffer"",
""Value"": false}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Write", gf.FunctionName);
			Assert.AreEqual("text", gf.Parameters[0].Name);
			Assert.AreEqual("Hello PLang world", gf.Parameters[0].Value);
			Assert.AreEqual("writeToBuffer", gf.Parameters[1].Name);
			Assert.AreEqual(false, gf.Parameters[1].Value);


		}

	}
}