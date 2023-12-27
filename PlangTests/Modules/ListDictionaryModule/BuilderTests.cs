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

namespace PLang.Modules.ListDictionaryModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.ListDictionaryModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.ListDictionaryModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("get 'productList' from list, write to %productList%")]
		public async Task GetList_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetList"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""productList""},
{""Type"": ""Boolean"",
""Name"": ""staticVariable"",
""Value"": false}],
""ReturnValue"": {""Type"": ""Object"",
""VariableName"": ""productList""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetList", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("productList", gf.Parameters[0].Value);
			Assert.AreEqual("staticVariable", gf.Parameters[1].Name);
			Assert.AreEqual(false, gf.Parameters[1].Value);
			Assert.AreEqual("productList", gf.ReturnValue.VariableName);

		}



		[DataTestMethod]
		[DataRow("get 'productList' from dict, write to %productDict%")]
		public async Task GetDictionary_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetDictionary"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""key"", 
""Value"": ""productList""},
{""Type"": ""Boolean"", 
""Name"": ""staticVariable"", 
""Value"": false}], 
""ReturnValue"": {""Type"": ""Object"", 
""VariableName"": ""productDict""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			
			 
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetDictionary", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("productList", gf.Parameters[0].Value);
			Assert.AreEqual("staticVariable", gf.Parameters[1].Name);
			Assert.AreEqual(false, gf.Parameters[1].Value);
			Assert.AreEqual("productDict", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("remove %item% from 'productList' dictionay")]
		public async Task DeleteObjectFromDictionary_Test(string text)
		{
			string response = @"{""FunctionName"": ""DeleteObjectFromDictionary"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""productList""},
{""Type"": ""Object"",
""Name"": ""value"",
""Value"": ""%item%""},
{""Type"": ""Boolean"",
""Name"": ""staticVariable"",
""Value"": false}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("DeleteObjectFromDictionary", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("productList", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%item%", gf.Parameters[1].Value);
			Assert.AreEqual("staticVariable", gf.Parameters[2].Name);
			Assert.AreEqual(false, gf.Parameters[2].Value);

		}

		[DataTestMethod]
		[DataRow("add %item% to 'productList'")]
		public async Task AddToList_Test(string text)
		{
			string response = @"{""FunctionName"": ""AddToList"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""productList""},
{""Type"": ""Object"",
""Name"": ""value"",
""Value"": ""%item%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AddToList", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("productList", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%item%", gf.Parameters[1].Value);

		}

		[DataTestMethod]
		[DataRow("add %item% to static 'productList'")]
		public async Task AddToStaticList_Test(string text)
		{
			string response = @"{""FunctionName"": ""AddToStaticList"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""productList""},
{""Type"": ""Object"",
""Name"": ""value"",
""Value"": ""%item%""},
{""Type"": ""String"",
""Name"": ""condition"",
""Value"": """"}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AddToStaticList", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("productList", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%item%", gf.Parameters[1].Value);

		}

		[DataTestMethod]
		[DataRow("add %item% to dictionary, 'products'")]
		public async Task AddToDictionary_Test(string text)
		{
			string response = @"{""FunctionName"": ""AddToDictionary"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""products""},
{""Type"": ""Object"",
""Name"": ""value"",
""Value"": ""%item%""},
{""Type"": ""Boolean"",
""Name"": ""updateIfExists"",
""Value"": false}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AddToDictionary", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("products", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%item%", gf.Parameters[1].Value);

		}

		[DataTestMethod]
		[DataRow("add %item% to static 'product' dictionary")]
		public async Task AddToStaticDictonary_Test(string text)
		{
			string response = @"{""FunctionName"": ""AddToStaticDictionary"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""key"",
""Value"": ""product""},
{""Type"": ""Object"",
""Name"": ""value"",
""Value"": ""%item%""},
{""Type"": ""Boolean"",
""Name"": ""updateIfExists"",
""Value"": false}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AddToStaticDictionary", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("product", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("%item%", gf.Parameters[1].Value);

		}
	}
}