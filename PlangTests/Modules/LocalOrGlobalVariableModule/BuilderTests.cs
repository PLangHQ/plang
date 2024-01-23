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

namespace PLang.Modules.LocalOrGlobalVariableModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.LocalOrGlobalVariableModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.LocalOrGlobalVariableModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper, logger);
		}



		[DataTestMethod]
		[DataRow("set 'name' as %name%")]
		public async Task SetVariable_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetVariable"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""key"", 
""Value"": ""name""}, 
{""Type"": ""Object"", 
""Name"": ""value"", 
""Value"": ""%name%""}], 
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetVariable", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%name%", gf.Parameters[1].Value);

		}



		[DataTestMethod]
		[DataRow("set static 'name' as %name%")]
		public async Task SetStaticVariable_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetStaticVariable"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""key"", 
""Value"": ""name""}, 
{""Type"": ""Object"", 
""Name"": ""value"", 
""Value"": ""%name%""}], 
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetStaticVariable", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%name%", gf.Parameters[1].Value);

		}


		[DataTestMethod]
		[DataRow("get 'name' var into %name%")]
		public async Task GetVariable_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetVariable"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""name""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""name""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetVariable", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("name", gf.ReturnValue[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("get static 'name' var into %name%")]
		public async Task GetStaticVariable_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetStaticVariable"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""name""}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""name""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetStaticVariable", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("name", gf.ReturnValue[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("remove variable 'name'")]
		public async Task RemoveVariable_Test(string text)
		{
			string response = @"{""FunctionName"": ""RemoveVariable"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""name""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("RemoveVariable", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("remove static variable 'name'")]
		public async Task RemoveStaticVariable_Test(string text)
		{
			string response = @"{""FunctionName"": ""RemoveStaticVariable"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""name""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("RemoveStaticVariable", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("listen to variable 'name', call !Process name=%full_name%, %key%")]
		public async Task OnAddVariableListener_Test(string text)
		{
			string response = @"{""FunctionName"": ""OnAddVariableListener"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""name""},
{""Type"": ""String"",
""Name"": ""goalName"",
""Value"": ""!Process""},
{""Type"": ""Dictionary`2"",
""Name"": ""parameters"",
""Value"": {""name"": ""%full_name%"", ""key"": ""%key%""}}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("OnAddVariableListener", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%key%", dict["key"]);

		}

		[DataTestMethod]
		[DataRow("listen to change on variable 'name', call !Process name=%full_name%, %key%")]
		public async Task OnChangeVariableListener_Test(string text)
		{
			string response = @"{""FunctionName"": ""OnChangeVariableListener"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""key"", ""Value"": ""name""},
    {""Type"": ""String"", ""Name"": ""goalName"", ""Value"": ""!Process""},
    {""Type"": ""Dictionary`2"", ""Name"": ""parameters"", ""Value"": {""name"": ""%full_name%"", ""key"": ""%key%""}},
    {""Type"": ""Boolean"", ""Name"": ""waitForResponse"", ""Value"": true},
    {""Type"": ""Int32"", ""Name"": ""delayWhenNotWaitingInMilliseconds"", ""Value"": 50}
],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("OnChangeVariableListener", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%key%", dict["key"]);

		}

		[DataTestMethod]
		[DataRow("listen to remove on variable 'name', call !Process name=%full_name%, %key%")]
		public async Task OnRemoveVariableListener_Test(string text)
		{
			string response = @"{""FunctionName"": ""OnRemoveVariableListener"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""key"", 
""Value"": ""name""}, 
{""Type"": ""String"", 
""Name"": ""goalName"", 
""Value"": ""!Process""}, 
{""Type"": ""Dictionary`2"", 
""Name"": ""parameters"", 
""Value"": {""name"": ""%full_name%"", ""key"": ""%key%""}}], 
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("OnRemoveVariableListener", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("name", gf.Parameters[0].Value);
			Assert.AreEqual("goalName", gf.Parameters[1].Name);
			Assert.AreEqual("!Process", gf.Parameters[1].Value);
			Assert.AreEqual("parameters", gf.Parameters[2].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(gf.Parameters[2].Value.ToString());
			Assert.AreEqual("%full_name%", dict["name"]);
			Assert.AreEqual("%key%", dict["key"]);

		}
	}
}