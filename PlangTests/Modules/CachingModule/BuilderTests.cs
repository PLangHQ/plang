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

namespace PLang.Modules.CachingModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.CachingModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CachingModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("get 'BigData' from cache, write to %obj%", "BigData")]
		[DataRow("get %cacheKey% from cache, write to %obj%", "%cacheKey%")]
		public async Task Get_Test(string text, string key)
		{

			string response = @"{""FunctionName"": ""Get"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": """ + key + @"""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""%obj%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Get", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual(key, gf.Parameters[0].Value);
			
			Assert.AreEqual("%obj%", gf.ReturnValue.VariableName);
		}

		[DataTestMethod]
		[DataRow("set %obj% to cache, 'ObjCache', cache for 10 minutes from last usage")]
		public async Task SetWithSliding_Test(string text)
		{

			string response = @"{""FunctionName"": ""Set"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""key"", ""Value"": ""ObjCache""},
    {""Type"": ""Object"", ""Name"": ""value"", ""Value"": ""%obj%""},
    {""Type"": ""TimeSpan"", ""Name"": ""slidingExpiration"", ""Value"": ""00:10:00""}
],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Set", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("ObjCache", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%obj%", gf.Parameters[1].Value);
			Assert.AreEqual("slidingExpiration", gf.Parameters[2].Name);
		}

		[DataTestMethod]
		[DataRow("set %obj% to cache, 'ObjCache', cache for 10 minutes from now")]
		public async Task SetWithAbsolute_Test(string text)
		{

			string response = @"{""FunctionName"": ""Set"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""key"", ""Value"": ""ObjCache""},
    {""Type"": ""Object"", ""Name"": ""value"", ""Value"": ""%obj%""},
    {""Type"": ""TimeSpan"", ""Name"": ""absoluteExpiration"", ""Value"": ""00:10:00""}
],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Set", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("ObjCache", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%obj%", gf.Parameters[1].Value);
			Assert.AreEqual("absoluteExpiration", gf.Parameters[2].Name);
		}

	}
}