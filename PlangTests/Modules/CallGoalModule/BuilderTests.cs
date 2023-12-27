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

namespace PLang.Modules.CallGoalModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		Builder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context);
			
			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.CallGoalModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new Builder();
			builder.InitBaseBuilder("PLang.Modules.CallGoalModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("call !Process.Image name=%full_name%, %address%")]
		public async Task RunGoal_Test(string text)
		{

			string response = @"{
  ""FunctionName"": ""RunGoal"",
  ""Parameters"": [
    {
      ""Type"": ""String"",
      ""Name"": ""goalName"",
      ""Value"": ""Process.Image""
    },
    {
      ""Type"": ""Dictionary`2"",
      ""Name"": ""parameters"",
      ""Value"": {
        ""name"": ""%full_name%"",
        ""address"": ""%address%""
      }
    },
    {
      ""Type"": ""Boolean"",
      ""Name"": ""waitForExecution"",
      ""Value"": true
    },
    {
      ""Type"": ""Int32"",
      ""Name"": ""delayWhenNotWaitingInMilliseconds"",
      ""Value"": 0
    }
  ],
  ""ReturnValue"": null
}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("RunGoal", gf.FunctionName);
			Assert.AreEqual("goalName", gf.Parameters[0].Name);
			Assert.AreEqual("Process.Image", gf.Parameters[0].Value);
			Assert.AreEqual("parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%address%", dict["address"]);

			Assert.AreEqual("waitForExecution", gf.Parameters[2].Name);
			Assert.AreEqual(true, gf.Parameters[2].Value);
			Assert.AreEqual("delayWhenNotWaitingInMilliseconds", gf.Parameters[3].Name);
			Assert.AreEqual((long) 0, gf.Parameters[3].Value);

		}


		[DataTestMethod]
		[DataRow("call !RunReporting, dont wait, delay for 3 sec")]
		public async Task RunGoal2_Test(string text)
		{

			string response = @"{
  ""FunctionName"": ""RunGoal"",
  ""Parameters"": [
    {
      ""Type"": ""String"",
      ""Name"": ""goalName"",
      ""Value"": ""RunReporting""
    },
    {
      ""Type"": ""Boolean"",
      ""Name"": ""waitForExecution"",
      ""Value"": false
    },
    {
      ""Type"": ""Int32"",
      ""Name"": ""delayWhenNotWaitingInMilliseconds"",
      ""Value"": 3000
    }
  ],
  ""ReturnValue"": null
}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("RunGoal", gf.FunctionName);
			Assert.AreEqual("goalName", gf.Parameters[0].Name);
			Assert.AreEqual("RunReporting", gf.Parameters[0].Value);
		
			Assert.AreEqual("waitForExecution", gf.Parameters[1].Name);
			Assert.AreEqual(false, gf.Parameters[1].Value);
			Assert.AreEqual("delayWhenNotWaitingInMilliseconds", gf.Parameters[2].Name);
			Assert.AreEqual((long) 3*1000, gf.Parameters[2].Value);

		}


	}
}