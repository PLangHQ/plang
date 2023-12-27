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

namespace PLang.Modules.LoopModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.LoopModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.LoopModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("loop through %list%, call !Process.File name=%full_name%, %key%")]
		public async Task RunLoop_Test(string text)
		{
			string response = @"{""FunctionName"": ""RunLoop"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""VariableToLoopThrough"", ""Value"": ""%list%""},
    {""Type"": ""String"", ""Name"": ""GoalNameToCall"", ""Value"": ""!Process.File""},
    {""Type"": ""Dictionary`2"", ""Name"": ""parameters"", ""Value"": {""name"": ""%full_name%"", ""key"": ""%key%""}}
],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("RunLoop", gf.FunctionName);
			Assert.AreEqual("VariableToLoopThrough", gf.Parameters[0].Name);
			Assert.AreEqual("%list%", gf.Parameters[0].Value);
			Assert.AreEqual("GoalNameToCall", gf.Parameters[1].Name);
			Assert.AreEqual("!Process.File", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);
			
			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%key%", dict["key"]);

		}


	}
}