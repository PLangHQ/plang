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

namespace PLang.Modules.ScheduleModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.ScheduleModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.ScheduleModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("wait for 1 sec")]
		public async Task Sleep_Test(string text)
		{
			string response = @"{""FunctionName"": ""Sleep"",
""Parameters"": [{""Type"": ""Int32"",
""Name"": ""sleepTimeInMilliseconds"",
""Value"": 1000}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Sleep", gf.FunctionName);
			Assert.AreEqual("sleepTimeInMilliseconds", gf.Parameters[0].Name);
			Assert.AreEqual((long) 1000, gf.Parameters[0].Value);

		}



		[DataTestMethod]
		[DataRow("run !Process.File on mondays at 11 am")]
		public async Task Schedule_Test(string text)
		{
			string response = @"{""FunctionName"": ""Schedule"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""cronCommand"", ""Value"": ""0 11 * * 1""},
    {""Type"": ""String"", ""Name"": ""goalName"", ""Value"": ""!Process.File""},
    {""Type"": ""Nullable`1"", ""Name"": ""lastRun"", ""Value"": null}
],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Schedule", gf.FunctionName);
			Assert.AreEqual("cronCommand", gf.Parameters[0].Name);
			Assert.AreEqual("0 11 * * 1", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process.File", gf.Parameters[1].Value);

		}

	}
}