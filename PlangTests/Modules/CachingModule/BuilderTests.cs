using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using PLangTests.Utils;
using System.Runtime.CompilerServices;
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

			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new OpenAiService(settings, logger, cacheHelper, context);

			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CachingModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string stepText, [CallerMemberName] string caller = "", Type? type = null)
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CachingModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("get 'BigData' from cache, write to %obj%", "BigData")]
		[DataRow("get %cacheKey% from cache, write to %obj%", "%cacheKey%")]
		public async Task Get_Test(string text, string key)
		{
			SetupResponse(text);

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;
			
			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("Get", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual(key, gf.Parameters[0].Value);
			
			AssertVar.AreEqual("%obj%", gf.ReturnValue[0].VariableName);
		}

		[DataTestMethod]
		[DataRow("set %obj% to cache, 'ObjCache', cache for 10 minutes from last usage")]
		[DataRow("set %obj% to cache, 'ObjCache', cache for 1 minutes from last usage")]
		public async Task SetWithSliding_Test(string text)
		{

			SetupResponse(text);

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);

			Assert.AreEqual("SetForSlidingExpiration", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("ObjCache", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%obj%", gf.Parameters[1].Value);
			Assert.AreEqual("timeInSeconds", gf.Parameters[2].Name);
			if (text.Contains("cache for 10 minutes"))
			{
				Assert.AreEqual((long)60 * 10, gf.Parameters[2].Value);
			} else
			{
				Assert.AreEqual((long)60, gf.Parameters[2].Value);
			}
		}

		[DataTestMethod]
		[DataRow("set %obj% to cache, 'ObjCache', cache for 10 minutes from now")]
		public async Task SetWithAbsolute_Test(string text)
		{

			SetupResponse(text);

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("SetForAbsoluteExpiration", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("ObjCache", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%obj%", gf.Parameters[1].Value);
			Assert.AreEqual("timeInSeconds", gf.Parameters[2].Name);
			Assert.AreEqual((long)60*10, gf.Parameters[2].Value);
		}

	}
}