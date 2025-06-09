using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using System.Data;
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
			var llmService = new OpenAiService(settings, logger, llmCaching, context);

			var datasources =new  List<DataSource>();
			datasources.Add(new DataSource("local", "", "", "", "", ""));
			datasources.Add(new DataSource("MainDb", "", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(datasources);


			var db = new SqliteConnection("DataSource=In memory;Version=3");

			builder = new Builder(fileSystem, dbFactory, settings, context, llmServiceFactory, typeHelper, logger, memoryStack, variableHelper, null, prParser, programFactory);
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string stepText, string functionName, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			var db = new SqliteConnection("DataSource=In memory;Version=3");
			var aiService = Substitute.For<ILlmService>();

			llmService.Query(Arg.Any<LlmRequest>(), typeof(DbGenericFunction)).Returns(p => {
				return JsonConvert.DeserializeObject(@$"{{""FunctionName"": ""{functionName}""}}", typeof(DbGenericFunction));
			});


			var dataSources = new List<DataSource>();
			dataSources.Add(new DataSource("data", "Microsoft.Data.Sqlite.SqliteConnection", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(dataSources);

			builder = new Builder(fileSystem, dbFactory, settings, context, llmServiceFactory, typeHelper, logger, memoryStack, variableHelper, null, prParser, programFactory);
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}

	

		[DataTestMethod]
		[DataRow("set datasource as 'MainDb'")]
		public async Task SetDataSouceName_Test(string text)
		{
		
			SetupResponse(text, "SetDataSouceName");

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("SetDataSouceName", gf.Name);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("MainDb", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("begin transaction")]
		public async Task BeginTransaction_Test(string text)
		{

			SetupResponse(text, "BeginTransaction");

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("BeginTransaction", gf.Name);
			Assert.AreEqual(0, gf.Parameters.Count);

		}

		[DataTestMethod]
		[DataRow("end transaction")]
		public async Task EndTransaction_Test(string text)
		{

			SetupResponse(text, "EndTransaction");

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("EndTransaction", gf.Name);
			Assert.AreEqual(0, gf.Parameters.Count);

		}



		[DataTestMethod]
		[DataRow("select name from users where id=%id%, write to %result%")]
		public async Task Select_Test(string text)
		{

			SetupResponse(text, "Select");

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("Select", gf.Name);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("select name from users where id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%id%", dict["@id"]);

			Assert.AreEqual("result", gf.ReturnValues[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("update users, set name=%name% where id=%id%")]
		public async Task Update_Test(string text)
		{

			SetupResponse(text, "Update");

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as DbGenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("Update", gf.Name);
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

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("Insert", gf.Name);
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

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("InsertAndSelectIdOfInsertedRow", gf.Name);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("insert into users (id, name) values (@id, @name)", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

			Assert.AreEqual("id", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("delete users where id=%id%")]
		public async Task Delete_Test(string text)
		{
			SetupResponse(text, "Delete");

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as DbGenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("Delete", gf.Name);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("DELETE FROM users WHERE id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%id%", dict["@id"]);

		}

	}
}