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
using System.Runtime.CompilerServices;
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

			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var llmService = new OpenAiService(settings, logger, llmCaching, context);
			llmServiceFactory.CreateHandler().Returns(llmService);


			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.LoopModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger, "");

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.LoopModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger, "");
		}

		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.LoopModule";
			return step;
		}



		[DataTestMethod]
		[DataRow("loop through %list%, call !Process/File name=%full_name%, %key%")]
		public async Task RunLoop_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("RunLoop", gf.FunctionName);
			Assert.AreEqual("variableToLoopThrough", gf.Parameters[0].Name);
			Assert.AreEqual("%list%", gf.Parameters[0].Value);
			Assert.AreEqual("goalNameToCall", gf.Parameters[1].Name);
			Assert.AreEqual("!Process/File", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);
			
			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%key%", dict["key"]);

		}


	}
}