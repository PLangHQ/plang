using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using System.Data;
using System.Data.SQLite;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Modules.DbModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		Builder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var llmService = new OpenAiService(settings, logger, cacheHelper, context);

			var datasources =new  List<DataSource>();
			datasources.Add(new DataSource("local", "", "", "", "", ""));
			datasources.Add(new DataSource("MainDb", "", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(datasources);

			typeHelper = new TypeHelper(fileSystem, settings);

			var db = new SQLiteConnection("DataSource=In memory;Version=3");

			builder = new Builder(fileSystem, db, settings, context, llmService, typeHelper, logger, memoryStack, variableHelper);
			builder.InitBaseBuilder("PLang.Modules.DbModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string stepText, string functionName, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			var db = new SQLiteConnection("DataSource=In memory;Version=3");
			var aiService = Substitute.For<ILlmService>();

			llmService.Query(Arg.Any<LlmQuestion>(), typeof(FunctionInfo)).Returns(p => {
				return JsonConvert.DeserializeObject(@$"{{""FunctionName"": ""{functionName}""}}", typeof(FunctionInfo));
			});


			var dataSources = new List<DataSource>();
			dataSources.Add(new DataSource("Main", "System.Data.SQLite.SQLiteConnection", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(dataSources);

			builder = new Builder(fileSystem, db, settings, context, llmService, typeHelper, logger, memoryStack, variableHelper);
			builder.InitBaseBuilder("PLang.Modules.DbModule", fileSystem, llmService, typeHelper, memoryStack, context, variableHelper, logger);
		}

		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.DbModule";
			return step;
		}

		[DataTestMethod]
		[DataRow("set datasource as 'MainDb'")]
		public async Task SetDataSouceName_Test(string text)
		{
		
			SetupResponse(text, "SetDataSouceName");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);

			Assert.AreEqual("SetDataSouceName", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("MainDb", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("begin transaction")]
		public async Task BeginTransaction_Test(string text)
		{

			SetupResponse(text, "BeginTransaction");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("BeginTransaction", gf.FunctionName);
			Assert.AreEqual(0, gf.Parameters.Count);

		}

		[DataTestMethod]
		[DataRow("end transaction")]
		public async Task EndTransaction_Test(string text)
		{

			SetupResponse(text, "EndTransaction");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("EndTransaction", gf.FunctionName);
			Assert.AreEqual(0, gf.Parameters.Count);

		}



		[DataTestMethod]
		[DataRow("select name from users where id=%id%, write to %result%")]
		public async Task Select_Test(string text)
		{

			SetupResponse(text, "Select");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("Select", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("select name from users where id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%id%", dict["@id"]);

			Assert.AreEqual("result", gf.ReturnValue[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("update users, set name=%name% where id=%id%")]
		public async Task Update_Test(string text)
		{

			SetupResponse(text, "Update");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as DbGenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("Update", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("update users set name=@name where id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

		}


		[DataTestMethod]
		[DataRow("insert into users (name) values (%name%)")]
		public async Task Insert_Test(string text)
		{
			SetupResponse(text, "Insert");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("Insert", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("insert into users (id, name) values (@id, @name)", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

		}



		[DataTestMethod]
		[DataRow("insert into users (name) values (%name%), write into %id%")]
		public async Task InsertAndSelectId_Test(string text)
		{
			SetupResponse(text, "Insert");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("InsertAndSelectIdOfInsertedRow", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("insert into users (id, name) values (@id, @name)", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

			Assert.AreEqual("id", gf.ReturnValue[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("delete users where id=%id%")]
		public async Task Delete_Test(string text)
		{
			SetupResponse(text, "Delete");

			var step = GetStep(text);

			var instruction = await builder.Build(step);
			var gf = instruction.Action as DbGenericFunction;

			Store(text, instruction.LlmQuestion.RawResponse);
			
			Assert.AreEqual("Delete", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("DELETE FROM users WHERE id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%id%", dict["@id"]);

		}

	}
}