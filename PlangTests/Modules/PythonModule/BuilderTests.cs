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

			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, outputStream, signingService);
			
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.PythonModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.PythonModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper, logger);
		}



		[DataTestMethod]
		[DataRow("run main.py, name=%full_name%, %zip%, use named args")]
		public async Task RunLoop_Test(string text)
		{
			string response = @"{""FunctionName"": ""RunPythonScript"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""fileName"",
""Value"": ""main.py""},
{""Type"": ""String[]"",
""Name"": ""parameterValues"",
""Value"": [""%full_name%"", ""%zip%""]},
{""Type"": ""String[]"",
""Name"": ""parameterNames"",
""Value"": [""name"", ""zip""]},
{""Type"": ""Boolean"",
""Name"": ""useNamedArguments"",
""Value"": true}],
""ReturnValue"": {""Type"": ""Dictionary`2"",
""VariableName"": ""result""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
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