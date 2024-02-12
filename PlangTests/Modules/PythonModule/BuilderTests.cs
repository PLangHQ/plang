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

namespace PLang.Modules.PythonModule.Tests
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
			var llmService = new OpenAiService(settings, logger, cacheHelper, context);

			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.PythonModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper, logger);

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.PythonModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper, logger);
		}

		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.PythonModule";
			return step;
		}



		[DataTestMethod]
		[DataRow("run main.py, name=%full_name%, %zip%, use named args, write to %result%")]
		public async Task RunPython_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("RunPythonScript", gf.FunctionName);
			Assert.AreEqual("fileName", gf.Parameters[0].Name);
			Assert.AreEqual("main.py", gf.Parameters[0].Value);
			Assert.AreEqual("parameterValues", gf.Parameters[1].Name);

			var paramValues = JsonConvert.DeserializeObject<string[]>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%full_name%", paramValues[0]);
			Assert.AreEqual("%zip%", paramValues[1]);

			Assert.AreEqual("parameterNames", gf.Parameters[2].Name);
			var paramNames = JsonConvert.DeserializeObject<string[]>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("name", paramNames[0]);
			Assert.AreEqual("zip", paramNames[1]);


			Assert.AreEqual("useNamedArguments", gf.Parameters[3].Name);
			Assert.AreEqual(true, gf.Parameters[3].Value);
			Assert.AreEqual("result", gf.ReturnValue[0].VariableName);

		}


	}
}